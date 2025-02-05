using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tas1945_mon
{
    public partial class Tas1945_DPCImageForm : Form
    {
        private MainForm g_fMainForm ;
        private Tas1945_Uc_RawImage g_ucRawImage;

        public Tas1945_DPCImageForm(MainForm mf, Tas1945_Uc_RawImage ucRawImage)
        {
            InitializeComponent();

            this.g_fMainForm = mf;
            g_ucRawImage = ucRawImage;
        }

        public void DrawZoomedImage(float[] asArrayData, int centerX, int centerY)
        {
            if (asArrayData.Length > 4860) return;

            try
            {
                if (g_ucRawImage == null || g_ucRawImage.g_bmBitmap == null)
                    throw new InvalidOperationException("원본 이미지가 로드되지 않았습니다.");

                Bitmap g_bmBitmap = g_ucRawImage.g_bmBitmap;

                // 원본 이미지에서 5x5 영역 추출
                int zoomSize = 5; // 5x5 영역
                int halfSize = zoomSize / 2;

                int startX = centerX - halfSize;
                int startY = centerY - halfSize;

                // 경계 체크: 이미지를 초과하지 않도록 보정
                if (startX < 0) startX = 0;
                if (startY < 0) startY = 0;
                if (startX + zoomSize > g_bmBitmap.Width) startX = g_bmBitmap.Width - zoomSize;
                if (startY + zoomSize > g_bmBitmap.Height) startY = g_bmBitmap.Height - zoomSize;

                // 확대 비율 계산
                int scaleFactorX = pbZoomedImage.Width / zoomSize;
                int scaleFactorY = pbZoomedImage.Height / zoomSize;
                int scaleFactor = Math.Min(scaleFactorX, scaleFactorY);

                // 확대 이미지 크기
                Bitmap zoomedBitmap = new Bitmap(zoomSize * scaleFactor, zoomSize * scaleFactor);

                using (Graphics graphics = Graphics.FromImage(zoomedBitmap))
                {
                    for (int y = 0; y < zoomSize; y++)
                    {
                        for (int x = 0; x < zoomSize; x++)
                        {
                            int pixelX = startX + x;
                            int pixelY = startY + y;

                            // 이미지 범위를 초과하지 않도록 보호 코드 추가
                            if (pixelX >= g_bmBitmap.Width || pixelY >= g_bmBitmap.Height)
                                continue;

                            Color pixelColor = g_bmBitmap.GetPixel(pixelX* g_ucRawImage.g_iZoom, pixelY* g_ucRawImage.g_iZoom);

                            // 확대된 크기로 픽셀 그리기
                            using (Brush brush = new SolidBrush(pixelColor))
                            {
                                graphics.FillRectangle(brush, x * scaleFactor, y * scaleFactor, scaleFactor, scaleFactor);
                            }
                        }
                    }
                }

                // PictureBox에 확대된 이미지 표시
                pbZoomedImage.SizeMode = PictureBoxSizeMode.Normal;
                pbZoomedImage.Image = zoomedBitmap;

                display_pixelParameter_(centerX, centerY, asArrayData);
            }
            catch (Exception ex)
            {
                g_fMainForm.ERR($"이미지 추출 중 오류 발생: {ex.Message}");
            }
        }

        public void DrawZoomed_QuadImage(float[] asArrayData, int centerX, int centerY)
        {
            if (asArrayData.Length > 19440) return;

            try
            {
                if (g_ucRawImage == null || g_ucRawImage.g_bmBitmap == null)
                    throw new InvalidOperationException("원본 이미지가 로드되지 않았습니다.");

                Bitmap g_bmBitmap = g_ucRawImage.g_bmBitmap;

                // 원본 이미지에서 10x10 영역 추출
                int zoomSize = 10; // 5x5 영역
                int halfSize = zoomSize / 2;

                int startX = centerX - halfSize + 1;
                int startY = centerY - halfSize + 1;

                // 경계 체크: 이미지를 초과하지 않도록 보정
                if (startX < 0) startX = 0;
                if (startY < 0) startY = 0;
                if (startX + zoomSize > g_bmBitmap.Width) startX = g_bmBitmap.Width - zoomSize;
                if (startY + zoomSize > g_bmBitmap.Height) startY = g_bmBitmap.Height - zoomSize;

                // 확대 비율 계산
                int scaleFactorX = pbZoomedImage.Width / zoomSize;
                int scaleFactorY = pbZoomedImage.Height / zoomSize;
                int scaleFactor = Math.Min(scaleFactorX, scaleFactorY);

                // 확대 이미지 크기
                Bitmap zoomedBitmap = new Bitmap(zoomSize * scaleFactor, zoomSize * scaleFactor);

                using (Graphics graphics = Graphics.FromImage(zoomedBitmap))
                {
                    for (int y = 0; y < zoomSize; y++)
                    {
                        for (int x = 0; x < zoomSize; x++)
                        {
                            int pixelX = startX + x;
                            int pixelY = startY + y;

                            // 이미지 범위를 초과하지 않도록 보호 코드 추가
                            if (pixelX >= g_bmBitmap.Width || pixelY >= g_bmBitmap.Height)
                                continue;

                            Color pixelColor = g_bmBitmap.GetPixel(pixelX * g_ucRawImage.g_iZoom, pixelY * g_ucRawImage.g_iZoom);

                            // 확대된 크기로 픽셀 그리기
                            using (Brush brush = new SolidBrush(pixelColor))
                            {
                                graphics.FillRectangle(brush, x * scaleFactor, y * scaleFactor, scaleFactor, scaleFactor);
                            }
                        }
                    }
                }

                // PictureBox에 확대된 이미지 표시
                pbZoomedImage.SizeMode = PictureBoxSizeMode.Normal;
                pbZoomedImage.Image = zoomedBitmap;

                //display_pixelParameter_(centerX, centerY, asArrayData);
            }
            catch (Exception ex)
            {
                g_fMainForm.ERR($"이미지 추출 중 오류 발생: {ex.Message}");
            }
        }

        private void display_pixelParameter_(int x, int y, float[] asArrayData)
        {
            for (int i = 1; i <= 25; i++)
            {
                var label = this.Controls.Find($"lbDPCpx{i}", true).FirstOrDefault() as Label;
                if (label != null)
                {
                    if (!g_fMainForm.cal_mode)
                    {
                        g_fMainForm.LBSet(label, "");
                    }
                    else
                    {
                        // 라벨의 row, col 계산
                        int row = (i - 1) / 5; // 행 계산
                        int col = (i - 1) % 5; // 열 계산

                        // 배열 좌표 계산
                        int arrayRow = y + row - 2; // 기준점에서 상대 위치로 이동
                        int arrayCol = x + col - 2; // 기준점에서 상대 위치로 이동

                        // 경계 체크 (배열 범위 초과 방지)
                        if (arrayRow < 0 || arrayRow >= g_fMainForm.sensitivity_buf.GetLength(0) ||
                            arrayCol < 0 || arrayCol >= g_fMainForm.sensitivity_buf.GetLength(1))
                        {
                            g_fMainForm.LBSet(label, "Out of Range");
                            continue;
                        }

                        // 배열 값 가져오기
                        float colorValue = g_ucRawImage.g_s16ImageValue[(arrayRow * 81) + arrayCol];
                        float measuredValue = (float)g_fMainForm.g_asPixelData_Cal[(arrayRow * 81) + arrayCol];
                        float Cal_data_45C = g_fMainForm.Image_buf_45C[arrayRow, arrayCol];
                        float Cal_data_25C = g_fMainForm.Image_buf_25C[arrayRow, arrayCol];
                        float offset = g_fMainForm.Image_buf_offset[arrayRow, arrayCol];
                        float noise = g_fMainForm.Image_buf_35C[arrayRow, arrayCol];

                        // 수식 포맷 정의
                        string labelText = $"({colorValue:F2}\r\n" +
                            $"{measuredValue:F0} - {offset:F0})\r\n/ ({Cal_data_45C:F0} - {Cal_data_25C:F0})\r\nNoise = {noise:F0}";
                        g_fMainForm.LBSet(label, labelText);
                    }
                }
            }
        }

    public void cbDPC_CheckedChanged(object sender, EventArgs e)
        {
            bool isVisible = g_fMainForm.cbDPC.Checked; // 체크 상태 가져오기

            // 1~25번 라벨의 Visible 속성 조정
            for (int i = 1; i <= 25; i++)
            {
                var label = this.Controls.Find($"lbDPCpx{i}", true).FirstOrDefault() as Label;
                if (label != null)
                {
                    label.Visible = isVisible;
                }
            }
        }

        private void Tas1945_DPCImageForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            g_fMainForm.g_fDPCImageForm = null;

            g_fMainForm.btnDPCimage_show.Enabled = true;
        }
    }
}
