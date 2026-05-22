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
        private Label _lblSensitivity;
        private TrackBar _trkSensitivity;
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
        private float _sensitivityMultiplier = 1.0f;

        // Dynamic labels for resizing
        private Label _lblDeviceText, _lblSensText, _lblSensMin, _lblSensMax, _lblWaveText, _lblVolumeText, _lblInstructions;

        public MicrophoneTestForm()
        {
            Theme.StyleForm(this, "🎤 Teste de Microfone", 850, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(600), Theme.S(500));
            BuildUI();
            LoadDevices();

            // Execute initial layout placement
            OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_cmbDevices == null) return;

            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            // Stretch ComboBox and trackbar
            _cmbDevices.Width = clientW - _cmbDevices.Left - Theme.S(30);
            _trkSensitivity.Width = clientW - _trkSensitivity.Left - Theme.S(90);
            _lblSensitivity.Left = _trkSensitivity.Right + Theme.S(10);

            if (_lblSensMin != null)
                _lblSensMin.Location = new Point(_trkSensitivity.Left, _trkSensitivity.Bottom - Theme.S(5));
            if (_lblSensMax != null)
                _lblSensMax.Location = new Point(_trkSensitivity.Right - _lblSensMax.Width, _trkSensitivity.Bottom - Theme.S(5));

            // Buttons row
            int btnY = Theme.S(195);
            _btnStart.Location = new Point(Theme.S(30), btnY);
            _btnRecord.Location = new Point(_btnStart.Right + Theme.S(10), btnY);
            _btnStop.Location = new Point(_btnRecord.Right + Theme.S(10), btnY);
            _lblStatus.Location = new Point(_btnStop.Right + Theme.S(20), btnY + Theme.S(8));
            _btnOpenFolder.Location = new Point(clientW - Theme.S(30) - _btnOpenFolder.Width, btnY);

            // Panels Y-position and sizing
            int waveY = Theme.S(265);
            int bottomMargin = Theme.S(90);
            int panelH = clientH - waveY - bottomMargin;
            if (panelH < Theme.S(100)) panelH = Theme.S(100);

            int availW = clientW - Theme.S(60);
            int volW = Theme.S(60);
            int waveW = availW - volW - Theme.S(20);

            if (_lblWaveText != null)
                _lblWaveText.Location = new Point(Theme.S(30), waveY - Theme.S(22));

            _waveformPanel.Location = new Point(Theme.S(30), waveY);
            _waveformPanel.Size = new Size(waveW, panelH);

            if (_lblVolumeText != null)
                _lblVolumeText.Location = new Point(_waveformPanel.Right + Theme.S(20), waveY - Theme.S(22));

            _volumePanel.Location = new Point(_waveformPanel.Right + Theme.S(20), waveY);
            _volumePanel.Size = new Size(volW, panelH);

            if (_lblVolume != null)
                _lblVolume.Location = new Point(_volumePanel.Left + (volW - _lblVolume.Width) / 2, _volumePanel.Bottom + Theme.S(4));

            // Footer instructions
            if (_lblInstructions != null)
            {
                _lblInstructions.Location = new Point(Theme.S(30), _volumePanel.Bottom + Theme.S(25));
                _lblInstructions.MaximumSize = new Size(clientW - Theme.S(60), 0);
            }
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
            _lblDeviceText = Theme.CreateLabel("Dispositivo:", 30, 100, Theme.FontBody);
            Controls.Add(_lblDeviceText);
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

            // Sensitivity slider
            _lblSensText = Theme.CreateLabel("Sensibilidade:", 30, 140, Theme.FontBody);
            Controls.Add(_lblSensText);
            _trkSensitivity = new TrackBar
            {
                Location = new Point(Theme.S(150), Theme.S(135)),
                Size = new Size(Theme.S(300), Theme.S(35)),
                Minimum = 1,   // 0.5x (value / 2.0)
                Maximum = 10,  // 5.0x
                Value = 2,     // 1.0x default
                TickFrequency = 1,
                SmallChange = 1,
                LargeChange = 2,
                BackColor = Theme.BgPrimary
            };
            _trkSensitivity.ValueChanged += (s, e) =>
            {
                _sensitivityMultiplier = _trkSensitivity.Value / 2.0f;
                _lblSensitivity.Text = $"{_sensitivityMultiplier:F1}x";
            };
            Controls.Add(_trkSensitivity);

            _lblSensitivity = Theme.CreateLabel("1.0x", 470, 140, Theme.FontButton, Theme.Accent);
            Controls.Add(_lblSensitivity);

            // Sensitivity hint labels
            _lblSensMin = Theme.CreateLabel("0.5x", 150, 165, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblSensMin);
            _lblSensMax = Theme.CreateLabel("5.0x", 430, 165, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblSensMax);

            // Buttons
            _btnStart = Theme.CreateButton("▶ Iniciar Captura", 30, 195, 180, 40);
            _btnStart.Click += BtnStart_Click;
            Controls.Add(_btnStart);

            _btnRecord = Theme.CreateButton("⏺ Gravar 5s", 220, 195, 160, 40);
            _btnRecord.Click += BtnRecord_Click;
            _btnRecord.Enabled = false;
            Controls.Add(_btnRecord);

            _btnStop = Theme.CreateSecondaryButton("⏹ Parar", 390, 195, 120, 40);
            _btnStop.Click += BtnStop_Click;
            _btnStop.Enabled = false;
            Controls.Add(_btnStop);

            _btnOpenFolder = Theme.CreateSecondaryButton("📂 Abrir Pasta", 670, 195, 140, 40);
            _btnOpenFolder.Click += (s, e) =>
            {
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string techTestDir = Path.Combine(docsPath, "TechTest");
                if (!Directory.Exists(techTestDir)) Directory.CreateDirectory(techTestDir);
                Process.Start("explorer.exe", techTestDir);
            };
            Controls.Add(_btnOpenFolder);

            // Status
            _lblStatus = Theme.CreateLabel("⏸ Aguardando...", 540, 203, Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblStatus);

            // Waveform panel
            _lblWaveText = Theme.CreateLabel("Forma de Onda:", 30, 250, Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblWaveText);
            _waveformPanel = new Panel
            {
                BackColor = Theme.BgDark,
                BorderStyle = BorderStyle.None
            };
            _waveformPanel.Paint += WaveformPanel_Paint;
            _waveformPanel.Resize += (s, e) => _waveformPanel.Invalidate();
            Controls.Add(_waveformPanel);

            // Volume meter
            _lblVolumeText = Theme.CreateLabel("Volume", 740, 250, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblVolumeText);
            _volumePanel = new Panel
            {
                BackColor = Theme.BgDark,
                BorderStyle = BorderStyle.None
            };
            _volumePanel.Paint += VolumePanel_Paint;
            _volumePanel.Resize += (s, e) => _volumePanel.Invalidate();
            Controls.Add(_volumePanel);

            _lblVolume = Theme.CreateLabel("0%", 750, 430, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblVolume);

            // Instructions
            _lblInstructions = Theme.CreateLabel(
                "💡 Dica: Fale ou faça um som próximo ao microfone para testar. " +
                "Use 'Gravar 5s' para gravar e reproduzir uma amostra. Arquivo salvo em Documentos/TechTest/. " +
                "Ajuste a sensibilidade para amplificar sons baixos.",
                30, 470, Theme.FontSmall, Theme.TextMuted);
            _lblInstructions.MaximumSize = new Size(Theme.S(770), 0);
            Controls.Add(_lblInstructions);

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
            // Apply sensitivity multiplier to the raw PCM buffer
            byte[] processedBuffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, processedBuffer, e.BytesRecorded);

            if (Math.Abs(_sensitivityMultiplier - 1.0f) > 0.01f)
            {
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    short sample = (short)(processedBuffer[i] | (processedBuffer[i + 1] << 8));
                    float amplified = sample * _sensitivityMultiplier;
                    // Clamp to 16-bit PCM range
                    if (amplified > 32767f) amplified = 32767f;
                    if (amplified < -32768f) amplified = -32768f;
                    short result = (short)amplified;
                    processedBuffer[i] = (byte)(result & 0xFF);
                    processedBuffer[i + 1] = (byte)((result >> 8) & 0xFF);
                }
            }

            _currentPeak = AudioHelper.CalculatePeak(processedBuffer, e.BytesRecorded);
            _waveformSamples = AudioHelper.ExtractWaveformSamples(processedBuffer, e.BytesRecorded, 340);

            if (_isRecording && _writer != null)
            {
                try { _writer.Write(processedBuffer, 0, e.BytesRecorded); } catch { }
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
