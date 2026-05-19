using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using NAudio.Wave;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class MicrophoneTestForm : Form
    {
        private ComboBox _cmbDevices;
        private Panel _waveformPanel;
        private Panel _volumePanel;
        private Label _lblStatus;
        private Label _lblVolume;
        private Button _btnStart, _btnRecord, _btnStop, _btnOpenFolder;
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private System.Windows.Forms.Timer _uiTimer;
        private float _currentPeak;
        private float[] _waveformSamples = new float[0];
        private bool _isCapturing;
        private bool _isRecording;
        private string _lastSavedPath;
        private string _currentRecordPath;

        public MicrophoneTestForm()
        {
            Theme.StyleForm(this, "🎤 Teste de Microfone", 850, 550);
            BuildUI();
            LoadDevices();
        }

        private void BuildUI()
        {
            // Header
            var lblTitle = Theme.CreateLabel("Teste de Microfone", 30, 20, Theme.FontHeader);
            Controls.Add(lblTitle);

            var lblDesc = Theme.CreateLabel("Verifique se o microfone integrado está captando áudio corretamente.",
                30, 55, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(lblDesc);

            // Device selection
            Controls.Add(Theme.CreateLabel("Dispositivo:", 30, 100, Theme.FontBody));
            _cmbDevices = new ComboBox
            {
                Location = new Point(Theme.S(130), Theme.S(97)),
                Size = new Size(Theme.S(400), Theme.S(30)),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody
            };
            Controls.Add(_cmbDevices);

            // Buttons
            _btnStart = Theme.CreateButton("▶ Iniciar Captura", 30, 145, 180, 40);
            _btnStart.Click += BtnStart_Click;
            Controls.Add(_btnStart);

            _btnRecord = Theme.CreateButton("⏺ Gravar 5s", 220, 145, 160, 40);
            _btnRecord.Click += BtnRecord_Click;
            _btnRecord.Enabled = false;
            Controls.Add(_btnRecord);

            _btnStop = Theme.CreateSecondaryButton("⏹ Parar", 390, 145, 120, 40);
            _btnStop.Click += BtnStop_Click;
            _btnStop.Enabled = false;
            Controls.Add(_btnStop);

            _btnOpenFolder = Theme.CreateSecondaryButton("📂 Abrir Pasta", 670, 145, 140, 40);
            _btnOpenFolder.Click += (s, e) =>
            {
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string techTestDir = Path.Combine(docsPath, "TechTest");
                if (!Directory.Exists(techTestDir)) Directory.CreateDirectory(techTestDir);
                Process.Start("explorer.exe", techTestDir);
            };
            Controls.Add(_btnOpenFolder);

            // Status
            _lblStatus = Theme.CreateLabel("⏸ Aguardando...", 540, 153, Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblStatus);

            // Waveform panel
            Controls.Add(Theme.CreateLabel("Forma de Onda:", 30, 200, Theme.FontBody, Theme.TextSecondary));
            _waveformPanel = new Panel
            {
                Location = new Point(Theme.S(30), Theme.S(225)),
                Size = new Size(Theme.S(680), Theme.S(150)),
                BackColor = Theme.BgDark,
                BorderStyle = BorderStyle.None
            };
            _waveformPanel.Paint += WaveformPanel_Paint;
            Controls.Add(_waveformPanel);

            // Volume meter
            Controls.Add(Theme.CreateLabel("Volume", 740, 200, Theme.FontSmall, Theme.TextSecondary));
            _volumePanel = new Panel
            {
                Location = new Point(Theme.S(740), Theme.S(225)),
                Size = new Size(Theme.S(60), Theme.S(150)),
                BackColor = Theme.BgDark,
                BorderStyle = BorderStyle.None
            };
            _volumePanel.Paint += VolumePanel_Paint;
            Controls.Add(_volumePanel);

            _lblVolume = Theme.CreateLabel("0%", 750, 380, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblVolume);

            // Instructions
            var lblInstructions = Theme.CreateLabel(
                "💡 Dica: Fale ou faça um som próximo ao microfone para testar. " +
                "Use 'Gravar 5s' para gravar e reproduzir uma amostra. Arquivo salvo em Documentos/TechTest/.",
                30, 420, Theme.FontSmall, Theme.TextMuted);
            lblInstructions.MaximumSize = new Size(Theme.S(770), 0);
            Controls.Add(lblInstructions);

            // UI timer
            _uiTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _uiTimer.Tick += (s, e) =>
            {
                _waveformPanel.Invalidate();
                _volumePanel.Invalidate();
                _lblVolume.Text = $"{(int)(_currentPeak * 100)}%";
            };
        }

        private void LoadDevices()
        {
            _cmbDevices.Items.Clear();
            int count = WaveIn.DeviceCount;
            for (int i = 0; i < count; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                _cmbDevices.Items.Add(caps.ProductName);
            }
            if (_cmbDevices.Items.Count > 0)
                _cmbDevices.SelectedIndex = 0;
            else
            {
                _lblStatus.Text = "❌ Nenhum microfone encontrado";
                _lblStatus.ForeColor = Theme.Error;
                _btnStart.Enabled = false;
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (_isCapturing) return;
            StartCapture();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            StopCapture();
        }

        private void BtnRecord_Click(object sender, EventArgs e)
        {
            if (!_isCapturing || _isRecording) return;
            StartRecording();
        }

        private void StartCapture()
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = _cmbDevices.SelectedIndex,
                    WaveFormat = new WaveFormat(44100, 16, 1),
                    BufferMilliseconds = 50
                };
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.RecordingStopped += (s, e) => { };
                _waveIn.StartRecording();
                _isCapturing = true;
                _uiTimer.Start();

                _lblStatus.Text = "✅ Captando áudio...";
                _lblStatus.ForeColor = Theme.Success;
                _btnStart.Enabled = false;
                _btnRecord.Enabled = true;
                _btnStop.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao iniciar captura:\n{ex.Message}", "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopCapture()
        {
            _isCapturing = false;
            _isRecording = false;
            _uiTimer.Stop();

            try { _waveIn?.StopRecording(); } catch { }
            try { _waveIn?.Dispose(); } catch { }
            _waveIn = null;

            try { _writer?.Dispose(); } catch { }
            _writer = null;

            _lblStatus.Text = "⏸ Parado";
            _lblStatus.ForeColor = Theme.TextSecondary;
            _btnStart.Enabled = true;
            _btnRecord.Enabled = false;
            _btnStop.Enabled = false;
            _currentPeak = 0;
            _waveformPanel.Invalidate();
            _volumePanel.Invalidate();
        }

        private void StartRecording()
        {
            _isRecording = true;

            // Create output directory and file path
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string techTestDir = Path.Combine(docsPath, "TechTest");
            Directory.CreateDirectory(techTestDir);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _currentRecordPath = Path.Combine(techTestDir, $"Mic_Gravacao_{timestamp}.wav");

            // Write directly to file
            _writer = new WaveFileWriter(_currentRecordPath, _waveIn.WaveFormat);

            _lblStatus.Text = "⏺ Gravando...";
            _lblStatus.ForeColor = Theme.Error;
            _btnRecord.Enabled = false;

            var timer = new System.Windows.Forms.Timer { Interval = 5000 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                StopRecordingAndPlay();
            };
            timer.Start();
        }

        private void StopRecordingAndPlay()
        {
            _isRecording = false;
            _lblStatus.Text = "▶ Reproduzindo gravação...";
            _lblStatus.ForeColor = Theme.Accent;

            try
            {
                // Close the writer to finalize the WAV file
                _writer?.Dispose();
                _writer = null;

                if (_currentRecordPath != null && File.Exists(_currentRecordPath))
                {
                    _lastSavedPath = _currentRecordPath;

                    // Play back from the saved file
                    using (var reader = new AudioFileReader(_lastSavedPath))
                    using (var waveOut = new WaveOutEvent())
                    {
                        waveOut.Init(reader);
                        waveOut.Play();
                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            Application.DoEvents();
                            System.Threading.Thread.Sleep(50);
                        }
                    }
                }
            }
            catch { }

            if (_isCapturing)
            {
                string savedInfo = _lastSavedPath != null ? $" | 💾 Salvo: {Path.GetFileName(_lastSavedPath)}" : "";
                _lblStatus.Text = $"✅ Captando áudio...{savedInfo}";
                _lblStatus.ForeColor = Theme.Success;
                _btnRecord.Enabled = true;
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            _currentPeak = AudioHelper.CalculatePeak(e.Buffer, e.BytesRecorded);
            _waveformSamples = AudioHelper.ExtractWaveformSamples(e.Buffer, e.BytesRecorded, 340);

            if (_isRecording && _writer != null)
            {
                try { _writer.Write(e.Buffer, 0, e.BytesRecorded); } catch { }
            }
        }

        private void WaveformPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = _waveformPanel.ClientRectangle;

            using (var bgBrush = new SolidBrush(Theme.BgDark))
                g.FillRectangle(bgBrush, rect);

            // Draw border
            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);

            // Center line
            int centerY = rect.Height / 2;
            using (var pen = new Pen(Color.FromArgb(40, Theme.Accent)))
                g.DrawLine(pen, 0, centerY, rect.Width, centerY);

            if (_waveformSamples.Length < 2) return;

            // Draw waveform
            using (var pen = new Pen(Theme.Accent, 1.5f))
            {
                var points = new PointF[_waveformSamples.Length];
                for (int i = 0; i < _waveformSamples.Length; i++)
                {
                    float x = (float)i / _waveformSamples.Length * rect.Width;
                    float y = centerY - _waveformSamples[i] * (rect.Height / 2 - 5);
                    points[i] = new PointF(x, y);
                }
                if (points.Length >= 2)
                    g.DrawLines(pen, points);
            }
        }

        private void VolumePanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = _volumePanel.ClientRectangle;

            using (var bgBrush = new SolidBrush(Theme.BgDark))
                g.FillRectangle(bgBrush, rect);

            using (var pen = new Pen(Theme.Border))
                g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);

            // Volume bar
            int barH = (int)(rect.Height * Math.Min(_currentPeak * 2, 1.0f));
            int barY = rect.Height - barH;

            Color barColor = _currentPeak > 0.7f ? Theme.Error :
                             _currentPeak > 0.3f ? Theme.Warning : Theme.Success;

            using (var brush = new LinearGradientBrush(
                new Rectangle(5, barY, rect.Width - 10, Math.Max(barH, 1)),
                barColor, Color.FromArgb(180, barColor), 90f))
            {
                g.FillRectangle(brush, 5, barY, rect.Width - 10, barH);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCapture();
            base.OnFormClosing(e);
        }
    }
}
