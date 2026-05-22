using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TechTest.Helpers;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace TechTest.Forms
{
    public class CameraTestForm : Form
    {
        private PictureBox _picPreview;
        private Label _lblStatus, _lblInfo;
        private Button _btnStart, _btnCapture;
        private CheckBox _chkMirror;
        private VideoCapture _capture;
        private CancellationTokenSource _cts;
        private bool _isMirrored;
        private bool _isRunning;

        // Fields for responsive layout positioning
        private Panel _previewBorder;
        private Label _lblTitle, _lblDesc;

        public CameraTestForm()
        {
            Theme.StyleForm(this, "📷 Teste de Webcam", 850, 580);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(650), Theme.S(450));
            BuildUI();

            // Set initial layout placement
            OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_picPreview == null) return;

            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            // Header labels layout
            if (_lblTitle != null)
                _lblTitle.Location = new Point(Theme.S(30), Theme.S(20));

            if (_lblDesc != null)
                _lblDesc.Location = new Point(Theme.S(30), Theme.S(55));

            // Camera preview panel (middle-left stretching area)
            int rightPanelW = Theme.S(160);
            int previewLeft = Theme.S(30);
            int previewTop = Theme.S(95);
            int previewBottomMargin = Theme.S(90);
            int previewW = clientW - previewLeft - rightPanelW - Theme.S(20);
            int previewH = clientH - previewTop - previewBottomMargin;

            if (previewW < Theme.S(100)) previewW = Theme.S(100);
            if (previewH < Theme.S(100)) previewH = Theme.S(100);

            _picPreview.Location = new Point(previewLeft, previewTop);
            _picPreview.Size = new Size(previewW, previewH);

            if (_previewBorder != null)
            {
                _previewBorder.Location = new Point(previewLeft - 1, previewTop - 1);
                _previewBorder.Size = new Size(previewW + 2, previewH + 2);
            }

            // Info panel on the right side, anchored to the right of preview
            int rightX = _picPreview.Right + Theme.S(20);
            if (_lblStatus != null)
            {
                _lblStatus.Location = new Point(rightX, previewTop);
                _lblStatus.MaximumSize = new Size(clientW - rightX - Theme.S(15), 0);
            }

            if (_lblInfo != null)
            {
                _lblInfo.Location = new Point(rightX, previewTop + Theme.S(40));
                _lblInfo.MaximumSize = new Size(clientW - rightX - Theme.S(15), 0);
            }

            // Bottom Buttons
            int btnY = clientH - Theme.S(70);
            if (_btnStart != null)
                _btnStart.Location = new Point(Theme.S(30), btnY);
            if (_btnCapture != null)
                _btnCapture.Location = new Point(_btnStart.Right + Theme.S(10), btnY);
            if (_chkMirror != null)
                _chkMirror.Location = new Point(_btnCapture.Right + Theme.S(20), btnY + Theme.S(10));
        }

        private void BuildUI()
        {
            _lblTitle = Theme.CreateLabel("Teste de Webcam", 30, 20, Theme.FontHeader);
            Controls.Add(_lblTitle);

            _lblDesc = Theme.CreateLabel(
                "Verifique se a câmera integrada do notebook está funcionando corretamente.",
                30, 55, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblDesc);

            // Preview
            _picPreview = new PictureBox
            {
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
            _picPreview.Resize += (s, e) => _picPreview.Invalidate();
            Controls.Add(_picPreview);

            // Border around preview
            _previewBorder = new Panel
            {
                BackColor = Color.Transparent
            };
            _previewBorder.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, _previewBorder.Width - 1, _previewBorder.Height - 1);
            };
            Controls.Add(_previewBorder);
            _previewBorder.SendToBack();

            // Info panel on the right
            _lblStatus = Theme.CreateLabel("⏸ Câmera desligada", 700, 95, Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblStatus);

            _lblInfo = Theme.CreateLabel("Resolução: —\nFPS: —", 700, 135, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblInfo);

            // Buttons
            _btnStart = Theme.CreateButton("▶ Iniciar", 30, 470, 150, 42);
            _btnStart.Click += BtnStart_Click;
            Controls.Add(_btnStart);

            _btnCapture = Theme.CreateButton("📸 Capturar Foto", 190, 470, 170, 42);
            _btnCapture.Click += BtnCapture_Click;
            _btnCapture.Enabled = false;
            Controls.Add(_btnCapture);

            _chkMirror = new CheckBox
            {
                Text = "Espelhar",
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
