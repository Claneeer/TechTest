using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NAudio.Wave;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class AudioTestForm : Form
    {
        private Button _btnLeft, _btnRight, _btnBoth, _btnStop;
        private Panel _speakerPanel;
        private Label _lblStatus;
        private WaveOutEvent _waveOut;
        private AudioChannel _activeChannel = AudioChannel.Both;
        private bool _isPlaying;
        private System.Windows.Forms.Timer _animTimer;
        private float _animPhase;

        public AudioTestForm()
        {
            Theme.StyleForm(this, "🔊 Teste de Alto-falantes", 800, 520);
            BuildUI();
        }

        private void BuildUI()
        {
            var lblTitle = Theme.CreateLabel("Teste de Alto-falantes", 30, 20, Theme.FontHeader);
            Controls.Add(lblTitle);

            var lblDesc = Theme.CreateLabel(
                "Reproduza um tom de teste em cada canal para verificar se os alto-falantes funcionam corretamente.",
                30, 55, Theme.FontSmall, Theme.TextSecondary);
            lblDesc.MaximumSize = new Size(Theme.S(740), 0);
            Controls.Add(lblDesc);

            // Speaker visualization
            _speakerPanel = new Panel
            {
                Location = new Point(Theme.S(30), Theme.S(100)),
                Size = new Size(Theme.S(740), Theme.S(250)),
                BackColor = Theme.BgDark,
                BorderStyle = BorderStyle.None
            };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_speakerPanel, true);
            _speakerPanel.Paint += SpeakerPanel_Paint;
            Controls.Add(_speakerPanel);

            // Buttons
            int btnY = 370;
            _btnLeft = Theme.CreateButton("🔈 Esquerdo", 30, btnY, 170, 45);
            _btnLeft.Click += (s, e) => PlayTone(AudioChannel.Left);
            Controls.Add(_btnLeft);

            _btnRight = Theme.CreateButton("🔈 Direito", 210, btnY, 170, 45);
            _btnRight.Click += (s, e) => PlayTone(AudioChannel.Right);
            Controls.Add(_btnRight);

            _btnBoth = Theme.CreateButton("🔊 Ambos", 390, btnY, 170, 45);
            _btnBoth.Click += (s, e) => PlayTone(AudioChannel.Both);
            Controls.Add(_btnBoth);

            _btnStop = Theme.CreateSecondaryButton("⏹ Parar", 570, btnY, 130, 45);
            _btnStop.Click += (s, e) => StopTone();
            Controls.Add(_btnStop);

            _lblStatus = Theme.CreateLabel("⏸ Aguardando...", 30, 435, Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblStatus);

            _animTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _animTimer.Tick += (s, e) =>
            {
                _animPhase += 0.15f;
                _speakerPanel.Invalidate();
            };
        }

        private void PlayTone(AudioChannel channel)
        {
            StopTone();
            try
            {
                _activeChannel = channel;
                var tone = AudioHelper.CreateTone(150, channel);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(tone);
                _waveOut.Play();
                _isPlaying = true;
                _animTimer.Start();

                string chName = channel == AudioChannel.Left ? "ESQUERDO" :
                                channel == AudioChannel.Right ? "DIREITO" : "AMBOS";
                _lblStatus.Text = $"▶ Reproduzindo 150Hz (grave) — Canal {chName}";
                _lblStatus.ForeColor = Theme.Success;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"❌ Erro: {ex.Message}";
                _lblStatus.ForeColor = Theme.Error;
            }
        }

        private void StopTone()
        {
            _isPlaying = false;
            _animTimer.Stop();
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
            }
            catch { }
            _waveOut = null;
            _lblStatus.Text = "⏸ Parado";
            _lblStatus.ForeColor = Theme.TextSecondary;
            _speakerPanel.Invalidate();
        }

        private void SpeakerPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var rect = _speakerPanel.ClientRectangle;

            // Background
            using (var brush = new SolidBrush(Theme.BgDark))
                g.FillRectangle(brush, rect);
            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);

            // Draw notebook body
            int nbW = 500, nbH = 140;
            int nbX = (rect.Width - nbW) / 2;
            int nbY = (rect.Height - nbH) / 2;
            var nbRect = new Rectangle(nbX, nbY, nbW, nbH);

            using (var path = Theme.RoundedRect(nbRect, 12))
            using (var brush = new SolidBrush(Color.FromArgb(40, 40, 70)))
                g.FillPath(brush, path);
            using (var path = Theme.RoundedRect(nbRect, 12))
            using (var pen = new Pen(Theme.Border, 1.5f))
                g.DrawPath(pen, path);

            // "Screen" inside notebook
            var screenRect = new Rectangle(nbX + 30, nbY + 15, nbW - 60, nbH - 50);
            using (var path = Theme.RoundedRect(screenRect, 6))
            using (var brush = new SolidBrush(Color.FromArgb(20, 20, 40)))
                g.FillPath(brush, path);

            // Notebook label
            using (var font = Theme.FontSmall)
            {
                var sz = g.MeasureString("NOTEBOOK", font);
                g.DrawString("NOTEBOOK", font, new SolidBrush(Theme.TextMuted),
                    nbX + (nbW - sz.Width) / 2, nbY + nbH - 30);
            }

            // Left speaker
            bool leftActive = _isPlaying && (_activeChannel == AudioChannel.Left || _activeChannel == AudioChannel.Both);
            DrawSpeaker(g, nbX - 80, nbY + nbH / 2 - 40, 60, 80, leftActive, "L");

            // Right speaker
            bool rightActive = _isPlaying && (_activeChannel == AudioChannel.Right || _activeChannel == AudioChannel.Both);
            DrawSpeaker(g, nbX + nbW + 20, nbY + nbH / 2 - 40, 60, 80, rightActive, "R");
        }

        private void DrawSpeaker(Graphics g, int x, int y, int w, int h, bool active, string label)
        {
            var rect = new Rectangle(x, y, w, h);
            Color bgColor = active ? Color.FromArgb(30, Theme.Accent) : Color.FromArgb(30, 30, 55);
            Color borderColor = active ? Theme.Accent : Theme.Border;

            using (var path = Theme.RoundedRect(rect, 10))
            {
                using (var brush = new SolidBrush(bgColor))
                    g.FillPath(brush, path);
                using (var pen = new Pen(borderColor, active ? 2f : 1f))
                    g.DrawPath(pen, path);
            }

            // Speaker icon (concentric circles)
            int cx = x + w / 2, cy = y + h / 2 - 5;
            for (int r = 8; r <= 22; r += 7)
            {
                Color circleColor = active ?
                    Color.FromArgb((int)(150 + 80 * Math.Sin(_animPhase + r * 0.3)), Theme.Accent) :
                    Color.FromArgb(60, Theme.TextMuted);
                using (var pen = new Pen(circleColor, active ? 2f : 1f))
                    g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
            }

            // Sound waves animation
            if (active)
            {
                for (int i = 1; i <= 3; i++)
                {
                    float offset = (float)Math.Sin(_animPhase + i * 1.5) * 0.5f + 0.5f;
                    int alpha = (int)(120 * offset);
                    int waveR = 28 + i * 10;
                    using (var pen = new Pen(Color.FromArgb(alpha, Theme.Accent), 1.5f))
                    {
                        if (label == "L")
                            g.DrawArc(pen, cx - waveR - 15, cy - waveR, waveR * 2, waveR * 2, -60, 120);
                        else
                            g.DrawArc(pen, cx - waveR + 15, cy - waveR, waveR * 2, waveR * 2, 120, 120);
                    }
                }
            }

            // Label
            using (var font = Theme.FontButton)
            {
                var sz = g.MeasureString(label, font);
                Color lColor = active ? Theme.Accent : Theme.TextMuted;
                g.DrawString(label, font, new SolidBrush(lColor), x + (w - sz.Width) / 2, y + h - 22);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopTone();
            base.OnFormClosing(e);
        }
    }
}
