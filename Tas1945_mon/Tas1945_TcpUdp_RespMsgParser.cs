using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tas1945_mon
{
	public partial class MainForm : Form
	{
		public byte[] g_abyRcvData = new byte[9600 + 512];
		public int g_iRcvSize = 0, g_iRespSize = 0, g_iRespWaitTick = 0;

		int data_status = 0;
		public bool Algorithm_Flag = false;  // Data 연산처리 중에는 새로운 데이터를 요청하지 않기 위해 만든 Flag

		public bool g_bFoundHeader = false;
		public uint g_uiResCode, g_uiStatus;
		byte[] g_abyPixelData = new byte[9720];
		int g_iPixelLen;

		public int cntFrames = 0; // frame을 세기 위한 변수 - 1초마다 초기화 시킨다
		public int maxFrame = 0; // max frame을 나타내기 위한 변수 - 가장 최근 1초의 frame 갯수를 담는다

		public int ISP_cnt = 0;
		public bool ISP_cnt_flag = false;

		public int Pulse_cnt = 0;
		public bool Pulse_cnt_flag = false;

		public double kalman_ME = 0.2;  // Kalman filter 의 Measurement Error값
		public double[,] Kalman_ME_Each = new double[ROW, COL];

		/// <summary>
		/// 
		/// </summary>
		enum REQ : uint
		{
			DEV_RST				= 0x0001,
			SET_IP				= 0x0002,
			SET_SPI_READ		= 0x0003,
			SET_SPI_CLOCK		= 0x0004,
			SET_AVR_COUNT		= 0x0005,
			GET_SETUP_INFO		= 0x0009,
			REG_RD				= 0x1001,
			REG_WR				= 0x2001,
			GET_SNG_RD_81x60	= 0x3002,
			GET_AVR_RD_81x60	= 0x3020,
			GET_SNG_PUSH_81x60	= 0x3012,
			GET_AVR_PUSH_81x60	= 0x3021,
			SET_PIXEL_READ		= 0x3030,
			RESEND_REQ			= 0x4000,
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="abyData"></param>
		/// <param name="iLength"></param>
		public void Tas1945_RespParser (byte[] abyData, int iLength)
		{
			try
			{
				g_bSendRead = true;
				if (Environment.TickCount - g_iRespWaitTick > 500)
				{
					g_iRespWaitTick = Environment.TickCount;

					if (g_bFoundHeader == true)
					{	
						//ERR ("Time Out Error !!! : " + HexArrToAscStr (g_abyRcvData, 0, g_iRcvSize, true));
						ERR ("Time Out Error !!!");
					}

					Array.Clear (g_abyRcvData, 0, g_abyRcvData.Length);
					g_iRcvSize = 0;
				
					g_bFoundHeader = false;
					g_uiResCode	= 0;
					g_uiStatus	= 0;
				}

				Array.Copy(abyData, 0, g_abyRcvData, g_iRcvSize, iLength);

				g_iRcvSize += iLength;

				g_iRespWaitTick = Environment.TickCount;

				if (iLength >= 8 && g_bFoundHeader == false)
				{
					if (g_abyRcvData[0] == 'T' && g_abyRcvData[1] == 'P')
					{
						g_bFoundHeader = true;

						g_uiResCode  = (uint)g_abyRcvData[2] << 0;
						g_uiResCode |= (uint)g_abyRcvData[3] << 8;
						g_uiResCode ^= 0x0100;
					
						g_iRespSize  = (int)g_abyRcvData[4] << 0;
						g_iRespSize |= (int)g_abyRcvData[5] << 8;
						g_iRespSize |= (int)g_abyRcvData[6] << 16;
						g_iRespSize |= (int)g_abyRcvData[7] << 24;

						g_uiStatus  = (uint)g_abyRcvData[8] << 0;
						g_uiStatus |= (uint)g_abyRcvData[9] << 8;
					}
				}
			
				if ((g_iRcvSize >= g_iRespSize) && (g_bFoundHeader == true))
				{
					//	CRC16 검사
					ushort usCalCrc16 = CalCrc16 (g_abyRcvData, (int)g_iRcvSize - 2);
					ushort usCrc16 = (ushort)(g_abyRcvData[g_iRcvSize - 1] << 8 | g_abyRcvData[g_iRcvSize - 2]);
					
					if (usCalCrc16 != usCrc16)
					{
//d						ERR ("CRC16 Error !!!");

						if (TGSGet (tgsDebugLog) == true)
						{
							LOG ("RES : " + HexArrToAscStr (g_abyRcvData, 0, g_iRcvSize, true));
						}	

						//	NAK;
						g_bCommComplete = true;
					
						Array.Clear (g_abyRcvData, 0, g_abyRcvData.Length);
						g_iRcvSize = 0;
					
						g_bFoundHeader = false;
						g_uiResCode	= 0;
						g_uiStatus	= 0;

						//Tas1945_ResendRequest ();
					
						return;
					}

					if (TGSGet (tgsDebugLog) == true)
					{
						LOG ("RES : " + HexArrToAscStr (g_abyRcvData, 0, g_iRcvSize, true));
					}

					switch (g_uiResCode)
					{
						case (uint)REQ.DEV_RST :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 12)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								LOG ("Reset Success !!!", Color.Blue);
							}
							else
							{
								ERR ("Reset Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.SET_IP :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 12)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								LOG ("Set IP Success !!!", Color.Blue);
							}
							else
							{
								ERR ("Set IP Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.SET_SPI_READ :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 12)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								LOG ("Set SPI Read Success !!!", Color.Blue);
							}
							else
							{
								ERR ("Set SPI Read Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.SET_SPI_CLOCK :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 12)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								LOG ("Set SPI Clock Success !!!", Color.Blue);
							}
							else
							{
								ERR ("Set SPI Clock Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.SET_AVR_COUNT :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 12)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								LOG ("Set Average Count Success !!!", Color.Blue);
							}
							else
							{
								ERR ("Set Average Count Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.GET_SETUP_INFO :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize > 38)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								LOG ("== Get Setup Info ==", Color.Blue);
								LOG ("RD CLK Delay : " + g_abyRcvData[10].ToString (), Color.Blue);
								LOG ("RD Edge      : " + ((g_abyRcvData[11] == 0x00) ? "Rising Edge" : "Falling Edge"), Color.Blue);
								LOG ("SPI Clock    : " + HexArrToUlong (g_abyRcvData, 12, 1, eEndian.Big).ToString () + " Mhz", Color.Blue);
								LOG ("Average Count: " + HexArrToUlong (g_abyRcvData, 13, 1, eEndian.Big).ToString (), Color.Blue);
								LOG ("NET Type     : " + ((g_abyRcvData[14] == 0x01) ? "SERVER" : "CLIENT"), Color.Blue);
								LOG ("IP/PORT      : " + HexArrToStr (g_abyRcvData, 15, g_iRespSize - 17), Color.Blue);
							}
							else
							{
								ERR ("Get Setup Info Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.REG_RD :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 14)
								{
									ERR ("Response Size Error !!!");
									break;
								}

								if (g_byReadDspMode == 1)	TBSet (tbRegData, HexArrToAscStr (g_abyRcvData, 11, 1, false));

								g_abyTas1945Register[StringToInt (g_abyRcvData[10].ToString ())] = (byte)StringToInt (g_abyRcvData[11].ToString ());
							
								if (g_byReadDspMode == 0)	g_fTas1945RegForm.Set_RegisterDataTextBox (g_abyRcvData[10], g_abyRcvData[11]);

								if (g_byReadDspMode == 4)
                                {

                                }

									g_byReadData = g_abyRcvData[11];

								if (g_byReadDspMode != 3)	LOG ("REG RD : " + g_abyRcvData[10].ToString () + ", " + g_abyRcvData[11].ToString () + "[0x" + HexToAscStr (g_abyRcvData[11], false) + "]" , Color.Blue);
							}
							else
							{
								ERR ("REG RD Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						case (uint)REQ.REG_WR :
							if (g_uiStatus != 0x00)		ERR ("REG WR Error !!! : " + g_uiStatus.ToString ());

							if (g_iRcvSize == 14)
							{
								LOG ("ECHO   : " + g_abyRcvData[10].ToString () + ", " + g_abyRcvData[11].ToString () + "[0x" + HexToAscStr (g_abyRcvData[11], false) + "]" , Color.Green);
							}
							else
							{
								ERR ("Response Size Error !!!");
							}
							break;

						case (uint)REQ.GET_SNG_RD_81x60 :
						case (uint)REQ.GET_AVR_RD_81x60 :
						case (uint)REQ.GET_SNG_PUSH_81x60 :
						case (uint)REQ.GET_AVR_PUSH_81x60 :
							
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 9733)
								{
									ERR ("Pixel Size Error !!!");
									Algorithm_Flag = false;
									break;
								}

								if (Check_PixelSize(9720) == false)
								{
									Algorithm_Flag = false;
									return;
								}

								if (g_bFistImage == false)							//	첫 번재 이미지는 버린다.
								{
									g_bFistImage = true;
									Algorithm_Flag = false;
									break;
								}

								if ((g_uiResCode == (uint)REQ.GET_SNG_RD_81x60) || (g_uiResCode == (uint)REQ.GET_SNG_PUSH_81x60))
								{
									if (RBGet (rbSingleOdd) == true)				//	Odd read
									{
										if (g_abyRcvData[10] == 0x02)
										{
											Algorithm_Flag = false;
											break;      //	Skip Even Image display
										}
									}
									else if (RBGet (rbSingleEven) == true)			//	Even read
									{
										if (g_abyRcvData[10] == 0x01)
										{
											Algorithm_Flag = false;
											break;      //	Skip Odd Image display
										}
									}
								}
								else
								{
									if (TGSGet (tgsPushSkip) == true && BTNGet (btnReadPushMode) == "Push Stop")
									{
										if (g_bPushSkipFlag == true)
										{
											g_bPushSkipFlag = false;
											Algorithm_Flag = false;
											break;
										}
										else
										{
											g_bPushSkipFlag = true;
										}
									}
								}

								if (BTNGet (btnReadPushMode) == "Push Read")
								{
									if (g_abyRcvData[10] == 0x01)
									{
										lbOdd.Color = Color.Green;
										lbEven.Color = Color.Black;
									}
									else if (g_abyRcvData[10] == 0x02)
									{
										lbOdd.Color = Color.Black;
										lbEven.Color = Color.Red;
									}
									else
									{
										lbOdd.Color = Color.Black;
										lbEven.Color = Color.Black;
										//break;
									}
								}
								else
								{
									lbOdd.Color = Color.Black;
									lbEven.Color = Color.Black;
								}

								if (TGSGet (tgsPixels) == false)					//	Pixel display
								{
									g_ucRawImage.SetImageSize (tgsImageX10.Checked);
								}

								Array.Clear (g_abyPixelData, 0, g_abyPixelData.Length);
								g_iPixelLen = 9720;

								//	UDP Push mode 시 Data 가 겹쳐 오류가 발생하는 경우가 있어 Data 를 추가 검사
								if (g_iRcvSize == 0)
								{
									//ERR ("SIZE = 0x00");
									Algorithm_Flag = false;
									break;
								}

								Array.Copy(g_abyRcvData, 11, g_abyPixelData, 0, g_iPixelLen);

								Convert_PixelData(g_abyPixelData, g_iPixelLen, ref g_asPixelData);

								if (DarkAvg_flag) DarkAvg_Apply(g_asPixelData, g_asPixelData.Length);

								if (Dataverify_flag)
								{
									if (!DataVerify(g_asPixelData))
									{
										Algorithm_Flag = false;
										break;
									}
									Dataverify_flag = false;
								}

								Array.Copy(g_asPixelData, g_asPixelData_Cal, g_asPixelData.Length);

								// Calibration 처리
								if (cal25_flag || cal35_flag || cal45_flag || WhiteCal_Save_flag)
								{
									if (cal25_flag && !moveAverage_cal(ref g_asPixelData_Cal, 8))
									{
										Algorithm_Flag = false;
										break;
									}
									if (cal25_flag) cal25_calibration(g_asPixelData_Cal);
									else if (cal35_flag) cal35_calibration(g_asPixelData_Cal);
									else if (cal45_flag)
									{
										if (!moveAverage_cal(ref g_asPixelData_Cal, 8))
										{
											Algorithm_Flag = false;
											break;
										}
										cal45_calibration(g_asPixelData_Cal);
									}
									else if (WhiteCal_Save_flag) Calibration_White_Cal(g_asPixelData_Cal);
									Algorithm_Flag = false;
									break;
								}

								// BookMark #2 : ISP 관련 함수들 시작위치
								if (cal_mode == true  && WhiteCal_Apply_flag == false)  // 열영상 Calibration 적용 분기
								{
									LBSet(lbGain, GAIN.ToString() + " )");
									LBSet(lbKalmanError, kalman_ME.ToString() + " )");
									
									//  감도 보정
									if (sensitivity_cal_flag) Sensitivity_Cal(ref g_asPixelData, g_asPixelData.Length);

									// 노이즈필터 적용
									if (Noise_filter_flag) Kalman_20240207(g_asPixelData, g_asPixelData.Length, Kalman_ME_Each);

									// Cal data를 이용하여 Dead Pixel 선정
									if (DP_apply_flag)
									{
										DeadPixel_Init_Apply(g_asPixelData, g_asPixelData.Length);
										DeadPixel_Apply(g_asPixelData, g_asPixelData.Length);
										if (cbRT_Apply.Checked) DeadPixel_RT_Apply(g_asPixelData, g_asPixelData.Length);
										DeadPixel_Count(g_asPixelData, g_asPixelData.Length);
									}
									
									// DPC 알고리즘 반복적용
									if (DPC_apply_flag)
									{
										// BookMark #8 : DPC 함수들 실행위치
										if(cbDPC33_Apply.Checked)
                                        {
											DPC_Apply_3by3_EdgeInterpolation1(g_asPixelData, g_asPixelData.Length);
											DPC_Apply_3by3_EdgeInterpolation2(g_asPixelData, g_asPixelData.Length);
										}
                                        if (cbDPC55_Apply.Checked)
                                        {
											DPC_Apply_5by5_EdgeInterpolation1(g_asPixelData, g_asPixelData.Length);
											DPC_Apply_5by5_EdgeInterpolation2(g_asPixelData, g_asPixelData.Length);
                                        }
										interpolationPixel_Count(g_asPixelData.Length);
									}

									if(ISP_offset_flag) ISP_Offset_Cal(g_asPixelData, g_asPixelData.Length);

									// Offset 체크박스 체크 시 버퍼를 이용하여 offset 적용
									if (cbISPoffset.Checked)
									{
										Parallel.For(0, 4860, i =>
										{
											g_asPixelData[i] -= (float)Image_buf_ISP_offset[i];
										});
									}
								}
								else if(cal_mode == true && WhiteCal_Apply_flag == true) // SWIR영상을 볼 수 있는 White Cal mode 적용 분기
								{
									if (Offset_Apply(ref g_asPixelData, g_asPixelData.Length) == false)
									{
										Algorithm_Flag = false;
										break;      //	Offset 적용
									}

									//  감도 보정
									if (sensitivity_cal_flag) 
										Calculate_WhiteCal_Data(ref g_asPixelData, g_asPixelData.Length, Image_buf_WhiteSignal, GAIN);

									// 노이즈필터 적용
									if (Noise_filter_flag) Kalman_20240207(g_asPixelData, g_asPixelData.Length, Kalman_ME_Each);

									// Cal data를 이용하여 Dead Pixel 선정
									if (DP_apply_flag)
									{
										DeadPixel_Init_Apply(g_asPixelData, g_asPixelData.Length);
										DeadPixel_Apply(g_asPixelData, g_asPixelData.Length);
										if (cbRT_Apply.Checked) DeadPixel_RT_Apply(g_asPixelData, g_asPixelData.Length);
										DeadPixel_Count(g_asPixelData, g_asPixelData.Length);
									}

									// DPC 알고리즘 반복적용
									if (DPC_apply_flag)
									{
										// BookMark #8 : DPC 함수들 실행위치
										if (cbDPC33_Apply.Checked)
										{
											DPC_Apply_3by3_EdgeInterpolation1(g_asPixelData, g_asPixelData.Length);
											DPC_Apply_3by3_EdgeInterpolation2(g_asPixelData, g_asPixelData.Length);
										}
										if (cbDPC55_Apply.Checked)
										{
											DPC_Apply_5by5_EdgeInterpolation1(g_asPixelData, g_asPixelData.Length);
											DPC_Apply_5by5_EdgeInterpolation2(g_asPixelData, g_asPixelData.Length);
										}
										interpolationPixel_Count(g_asPixelData.Length);
									}

									if (ISP_offset_flag) ISP_Offset_Cal(g_asPixelData, g_asPixelData.Length);

									// Offset 체크박스 체크 시 버퍼를 이용하여 offset 적용
									if (cbISPoffset.Checked)
									{
										Parallel.For(0, 4860, i =>
										{
											g_asPixelData[i] -= (float)Image_buf_ISP_offset[i];
										});
									}
								}
								else   // 기본 Offset만 적용한 영상 분기
								{
									if (Offset_Apply(ref g_asPixelData, g_asPixelData.Length) == false)
									{
										Algorithm_Flag = false;
										break;      //	Offset 적용
									}

									if (TGSGet(tgsContinue) == true)                                                    //	Continue mode
									{
										if (Average_Apply(ref g_asPixelData, g_asPixelData.Length) == false)
										{
											Algorithm_Flag = false;
											break; //	GUI Average or Move8 Average 적용
										}
									}

									if (TGSGet(tgsKalman) == true)
									{
										if (Kalman_Filter_Apply(ref g_asPixelData, g_asPixelData.Length) == false)
										{
											Algorithm_Flag = false;
											break;
										}
									}

									if (TGSGet(tgsLpfIir) == true)
									{
										if (LPF_IIR_Filter_Apply(ref g_asPixelData, g_asPixelData.Length, (double)NUDGet(nudLpfSensitive)) == false)
										{
											Algorithm_Flag = false;
											break;
										}
									}
								}

								if (g_bCsvOn == true)
								{
									if (g_iCsvSaveMode == 1)
									{
										Save_Csv ();																		//	CSV File save
									}
									else
									{
										//g_asTestPixelLog[g_iTestPixelLogCnt, 0] = Get_PixelData(g_asPixelData, 80, 0);
										//g_asTestPixelLog[g_iTestPixelLogCnt, 1] = Get_PixelData(g_asPixelData, 80, 2);
										//g_asTestPixelLog[g_iTestPixelLogCnt, 2] = Get_PixelData(g_asPixelData, 80, 59);
										g_asTestPixelLog[g_iTestPixelLogCnt, 0] = g_asPixelData.Average();
										g_asTestPixelLog[g_iTestPixelLogCnt, 1] = g_asPixelData.Max();
										g_asTestPixelLog[g_iTestPixelLogCnt, 2] = g_asPixelData.Min();
										//g_asTestPixelLog[g_iTestPixelLogCnt, 0] = Get_PixelData(g_asPixelData, 40, 30);
										//g_asTestPixelLog[g_iTestPixelLogCnt, 1] = Get_PixelData(g_asPixelData, 44, 29);
										//g_asTestPixelLog[g_iTestPixelLogCnt, 2] = Get_PixelData(g_asPixelData, 39, 35);
										g_iTestPixelLogCnt++;

										if (g_iTestPixelLogCnt >= NUDGet (nudCsvSaveCnt))
										{
											Save_TestPixelCsv ();
											//Save_Test5PixelCsv();
										}
									}
								}


								//for (int i = 0; i < 4860; i++)
								//	g_asPixelData[i] = i;

								if (cbDCT_Apply.Checked) apply_DCT(ref g_asPixelData);

								if (flip_flag == true) FlipHorizontally(ref g_asPixelData, ROW, COL);

								if (g_queImage.Count < 20)				//	Timer 에서 Display 할 시
								{
									if (cbQuad_Apply.Checked)
									{
										asPixelData_quad = making_quadArray(g_asPixelData);
										g_queImage.Enqueue(asPixelData_quad);
									}
									else
										g_queImage.Enqueue (g_asPixelData);
								}
							}
							else
							{
								//ERR ("GET SNG RD Error !!! : " + g_uiStatus.ToString ());
							}

							//if (data_status != 4) LOG("비상!!", Color.Red);
							//data_status = 5;
							//LOG($"Data Status = {data_status} → Break직전", Color.Green);
							Algorithm_Flag = false;
							break;

						case (uint)REQ.SET_PIXEL_READ :
							if (g_uiStatus == 0x00)
							{
								if (g_iRespSize != 12)
								{
									ERR ("Response Size Error !!!");
									break;
								}
								
								LOG ("Set Pixel Read Start / Stop Success !!!", Color.Blue);
							}
							else
							{
								ERR ("Set Pixel Read Error !!! : " + g_uiStatus.ToString ());
							}
							break;

						default :
							//ERR ("Unknown Code !!! : " + HexArrToAscStr (abyData, 0, iLength, true));
//d							ERR ("Unknown Code !!!");
							break;
					}

					g_bCommComplete = true;

					Array.Clear (g_abyRcvData, 0, g_abyRcvData.Length);
					g_iRcvSize = 0;
					g_iRespSize = 0;

					g_bFoundHeader = false;
					g_uiResCode	= 0;
					g_uiStatus	= 0;
				}
			}
			//catch (Exception ex)
			catch (Exception)
			{
				Array.Clear (g_abyRcvData, 0, g_abyRcvData.Length);
				g_iRcvSize = 0;
				g_iRespSize = 0;

				g_bFoundHeader = false;
				g_uiResCode	= 0;
				g_uiStatus	= 0;
								
				g_bCommComplete = true;

				//ERR (ex.Message);
			}
		}
	}
}
