using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class CameraTestForm : Form
    {
        private PictureBox _picPreview;
        private Label _lblStatus, _lblInfo;
        private Button _btnStart, _btnCapture, _btnMirror;
        private CheckBox _chkMirror;
        private VideoCapture _capture;
        private CancellationTokenSource _cts;
        private bool _isMirrored;
        private bool _isRunning;

        public CameraTestForm()
        {
            Theme.StyleForm(this, "📷 Teste de Webcam", 850, 580);
            BuildUI();
        }

        private void BuildUI()
        {
            var lblTitle = Theme.CreateLabel("Teste de Webcam", 30, 20, Theme.FontHeader);
            Controls.Add(lblTitle);

            var lblDesc = Theme.CreateLabel(
                "Verifique se a câmera integrada do notebook está funcionando corretamente.",
                30, 55, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(lblDesc);

            // Preview
            _picPreview = new PictureBox
            {
                Location = new System.Drawing.Point(Theme.S(30), Theme.S(95)),
                Size = new System.Drawing.Size(Theme.S(640), Theme.S(360)),
                BackColor = Theme.BgDark,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.None
            };
            _picPreview.Paint += (s, e) =>
            {
                if (_picPreview.Image == null)
                {
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    string msg = "📷 Clique em 'Iniciar' para ativar a webcam";
                    using (var font = Theme.FontBody)
                    {
                        var sz = e.Graphics.MeasureString(msg, font);
                        e.Graphics.DrawString(msg, font, new SolidBrush(Theme.TextMuted),
                            (_picPreview.Width - sz.Width) / 2, (_picPreview.Height - sz.Height) / 2);
                    }
                }
            };
            Controls.Add(_picPreview);

            // Border around preview
            var previewBorder = new Panel
            {
                Location = new System.Drawing.Point(Theme.S(29), Theme.S(94)),
                Size = new System.Drawing.Size(Theme.S(642), Theme.S(362)),
                BackColor = Color.Transparent
            };
            previewBorder.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, previewBorder.Width - 1, previewBorder.Height - 1);
            };
            Controls.Add(previewBorder);
            previewBorder.SendToBack();

            // Info panel on the right
            int rightX = Theme.S(700);
            _lblStatus = Theme.CreateLabel("⏸ Câmera desligada", rightX, Theme.S(95), Theme.FontBody, Theme.TextSecondary);
            _lblStatus.MaximumSize = new System.Drawing.Size(Theme.S(130), 0);
            Controls.Add(_lblStatus);

            _lblInfo = Theme.CreateLabel("Resolução: —\nFPS: —", rightX, Theme.S(135), Theme.FontSmall, Theme.TextMuted);
            _lblInfo.MaximumSize = new System.Drawing.Size(Theme.S(130), 0);
            Controls.Add(_lblInfo);

            // Buttons
            int btnY = 470;
            _btnStart = Theme.CreateButton("▶ Iniciar", 30, btnY, 150, 42);
            _btnStart.Click += BtnStart_Click;
            Controls.Add(_btnStart);

            _btnCapture = Theme.CreateButton("📸 Capturar Foto", 190, btnY, 170, 42);
            _btnCapture.Click += BtnCapture_Click;
            _btnCapture.Enabled = false;
            Controls.Add(_btnCapture);

            _chkMirror = new CheckBox
            {
                Text = "Espelhar",
                Location = new System.Drawing.Point(Theme.S(380), Theme.S(btnY + 10)),
                AutoSize = true,
                ForeColor = Theme.TextSecondary,
                Font = Theme.FontBody,
                BackColor = Color.Transparent
            };
            _chkMirror.CheckedChanged += (s, e) => _isMirrored = _chkMirror.Checked;
            Controls.Add(_chkMirror);
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                StopCamera();
                _btnStart.Text = "▶ Iniciar";
                return;
            }
            StartCamera();
            _btnStart.Text = "⏹ Parar";
        }

        private void StartCamera()
        {
            try
            {
                _capture = new VideoCapture(0);
                if (!_capture.IsOpened())
                {
                    _lblStatus.Text = "❌ Webcam não encontrada";
                    _lblStatus.ForeColor = Theme.Error;
                    return;
                }

                _capture.Set(VideoCaptureProperties.FrameWidth, 640);
                _capture.Set(VideoCaptureProperties.FrameHeight, 480);

                _isRunning = true;
                _btnCapture.Enabled = true;
                _lblStatus.Text = "📷 Webcam ativa";
                _lblStatus.ForeColor = Theme.Success;
                _cts = new CancellationTokenSource();

                Task.Run(() => CaptureLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"❌ Erro: {ex.Message}";
                _lblStatus.ForeColor = Theme.Error;
            }
        }

        private void CaptureLoop(CancellationToken token)
        {
            using var frame = new Mat();
            int fpsCount = 0;
            var fpsTimer = DateTime.Now;

            while (!token.IsCancellationRequested && _capture != null && _capture.IsOpened())
            {
                _capture.Read(frame);
                if (frame.Empty()) continue;

                fpsCount++;
                var elapsed = (DateTime.Now - fpsTimer).TotalSeconds;

                try
                {
                    var mat = _isMirrored ? frame.Flip(FlipMode.Y) : frame;
                    var bitmap = BitmapConverter.ToBitmap(mat);
                    if (mat != frame) mat.Dispose();

                    _picPreview.BeginInvoke(new Action(() =>
                    {
                        var old = _picPreview.Image;
                        _picPreview.Image = bitmap;
                        old?.Dispose();
                    }));

                    if (elapsed >= 1.0)
                    {
                        int fps = fpsCount;
                        fpsCount = 0;
                        fpsTimer = DateTime.Now;
                        int w = frame.Width, h = frame.Height;
                        _lblInfo.BeginInvoke(new Action(() =>
                        {
                            _lblInfo.Text = $"Resolução: {w}x{h}\nFPS: {fps}";
                        }));
                    }
                }
                catch { break; }

                Thread.Sleep(16);
            }
        }

        private void StopCamera()
        {
            _isRunning = false;
            _cts?.Cancel();
            Thread.Sleep(100);
            try { _capture?.Release(); } catch { }
            try { _capture?.Dispose(); } catch { }
            _capture = null;
            _btnCapture.Enabled = false;
            _lblStatus.Text = "⏸ Câmera desligada";
            _lblStatus.ForeColor = Theme.TextSecondary;
            _lblInfo.Text = "Resolução: —\nFPS: —";
        }

        private void BtnCapture_Click(object sender, EventArgs e)
        {
            if (_picPreview.Image == null) return;
            using (var sfd = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                FileName = $"webcam_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    _picPreview.Image.Save(sfd.FileName);
                    _lblStatus.Text = "✅ Foto salva!";
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCamera();
            base.OnFormClosing(e);
        }
    }
}
