using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class AudioTestForm : Form
    {
        private Button _btnLeft, _btnRight, _btnBoth, _btnStop;
        private ComboBox _cmbSoundType;
        private TrackBar _trkFrequency;
        private TrackBar _trkVolume;
        private Label _lblFreqValue, _lblVolValue;
        private Panel _speakerPanel;
        private Label _lblStatus;
        private WaveOutEvent _waveOut;
        private SignalGenerator _signalGen;
        private AudioChannel _activeChannel = AudioChannel.Both;
        private SoundType _activeSoundType = SoundType.Sine;
        private bool _isPlaying;
        private System.Windows.Forms.Timer _animTimer;
        private System.Windows.Forms.Timer _beepTimer;
        private float _animPhase;
        private float _currentFrequency = 440f;
        private float _currentGain = 0.5f;
        private bool _beepMuted = false;

        // Labels mapped to fields for responsive layout
        private Label _lblTitle, _lblDesc, _lblSoundTypeText;
        private Label _lblFreqText, _lblFreqMin, _lblFreqMax;
        private Label _lblVolText, _lblVolMin, _lblVol100, _lblVolMax;

        // Fields for custom audio (USB Pendrive / Manual Selection)
        private ComboBox _cmbCustomAudio;
        private Button _btnBrowseCustom;
        private Button _btnRefreshCustom;
        private Label _lblCustomAudioText;
        private Label _lblCustomAudioInfo;
        private List<string> _customAudioFiles = new List<string>();
        private AudioFileReader _customAudioReader;
        private VolumeSampleProvider _volumeProvider;

        public AudioTestForm()
        {
            Theme.StyleForm(this, "🔊 Teste de Alto-falantes", 830, 620);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(720), Theme.S(550));
            BuildUI();

            // Scan custom audio files initially
            ScanCustomAudio();

            // Set initial layout placement
            OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_cmbSoundType == null) return;

            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            // Header labels layout
            if (_lblTitle != null)
                _lblTitle.Location = new Point(Theme.S(30), Theme.S(20));

            if (_lblDesc != null)
            {
                _lblDesc.Location = new Point(Theme.S(30), Theme.S(55));
                _lblDesc.MaximumSize = new Size(clientW - Theme.S(60), 0);
            }

            int labelLeft = Theme.S(30);
            int inputLeft = Theme.S(150);
            int sliderWidth = clientW - inputLeft - Theme.S(120);

            // ComboBox positioning
            if (_lblSoundTypeText != null)
                _lblSoundTypeText.Location = new Point(labelLeft, Theme.S(100));
            _cmbSoundType.Location = new Point(inputLeft, Theme.S(97));

            // Frequency controls positioning
            if (_lblFreqText != null)
                _lblFreqText.Location = new Point(labelLeft, Theme.S(140));
            _trkFrequency.Location = new Point(inputLeft, Theme.S(135));
            _trkFrequency.Width = sliderWidth;
            _lblFreqValue.Location = new Point(_trkFrequency.Right + Theme.S(10), Theme.S(140));

            if (_lblFreqMin != null)
                _lblFreqMin.Location = new Point(_trkFrequency.Left, _trkFrequency.Bottom - Theme.S(5));
            if (_lblFreqMax != null)
                _lblFreqMax.Location = new Point(_trkFrequency.Right - _lblFreqMax.Width, _trkFrequency.Bottom - Theme.S(5));

            // Volume controls positioning
            if (_lblVolText != null)
                _lblVolText.Location = new Point(labelLeft, Theme.S(190));
            _trkVolume.Location = new Point(inputLeft, Theme.S(185));
            _trkVolume.Width = sliderWidth;
            _lblVolValue.Location = new Point(_trkVolume.Right + Theme.S(10), Theme.S(190));

            if (_lblVolMin != null)
                _lblVolMin.Location = new Point(_trkVolume.Left, _trkVolume.Bottom - Theme.S(5));
            if (_lblVol100 != null)
                _lblVol100.Location = new Point(_trkVolume.Left + (_trkVolume.Width / 2) - (_lblVol100.Width / 2), _trkVolume.Bottom - Theme.S(5));
            if (_lblVolMax != null)
                _lblVolMax.Location = new Point(_trkVolume.Right - _lblVolMax.Width, _trkVolume.Bottom - Theme.S(5));

            // Custom Audio controls positioning
            bool customVisible = _cmbSoundType.SelectedIndex == 5;
            if (_lblCustomAudioText != null)
                _lblCustomAudioText.Location = new Point(labelLeft, Theme.S(240));

            if (_cmbCustomAudio != null)
            {
                _cmbCustomAudio.Location = new Point(inputLeft, Theme.S(237));
                _cmbCustomAudio.Width = sliderWidth - Theme.S(220); // Shortened for two buttons next to it
            }

            if (_btnBrowseCustom != null && _cmbCustomAudio != null)
            {
                _btnBrowseCustom.Location = new Point(_cmbCustomAudio.Right + Theme.S(10), Theme.S(237));
                _btnBrowseCustom.Size = new Size(Theme.S(100), Theme.S(28));
            }

            if (_btnRefreshCustom != null && _btnBrowseCustom != null)
            {
                _btnRefreshCustom.Location = new Point(_btnBrowseCustom.Right + Theme.S(10), Theme.S(237));
                _btnRefreshCustom.Size = new Size(Theme.S(100), Theme.S(28));
            }

            if (_lblCustomAudioInfo != null)
            {
                _lblCustomAudioInfo.Location = new Point(inputLeft, Theme.S(272));
                _lblCustomAudioInfo.Width = sliderWidth;
            }

            // Speaker Panel sizing
            int panelY = customVisible ? Theme.S(305) : Theme.S(245);
            int footerSpace = Theme.S(125);
            int panelH = clientH - panelY - footerSpace;
            if (panelH < Theme.S(100)) panelH = Theme.S(100);

            _speakerPanel.Location = new Point(Theme.S(30), panelY);
            _speakerPanel.Size = new Size(clientW - Theme.S(60), panelH);

            // Action Buttons row
            int btnY = _speakerPanel.Bottom + Theme.S(15);
            int availW = clientW - Theme.S(60);
            int gap = Theme.S(10);
            int btnW = (availW - gap * 3) / 4;
            if (btnW < Theme.S(80)) btnW = Theme.S(80);

            _btnLeft.Location = new Point(Theme.S(30), btnY);
            _btnLeft.Width = btnW;

            _btnRight.Location = new Point(_btnLeft.Right + gap, btnY);
            _btnRight.Width = btnW;

            _btnBoth.Location = new Point(_btnRight.Right + gap, btnY);
            _btnBoth.Width = btnW;

            _btnStop.Location = new Point(_btnBoth.Right + gap, btnY);
            _btnStop.Width = btnW;

            // Status label alignment
            if (_lblStatus != null)
            {
                _lblStatus.Location = new Point(Theme.S(30), _btnLeft.Bottom + Theme.S(12));
                _lblStatus.MaximumSize = new Size(clientW - Theme.S(60), 0);
            }
        }

        private void BuildUI()
        {
            _lblTitle = Theme.CreateLabel("Teste de Alto-falantes", 30, 20, Theme.FontHeader);
            Controls.Add(_lblTitle);

            _lblDesc = Theme.CreateLabel(
                "Reproduza tons de teste em cada canal para verificar se os alto-falantes funcionam. " +
                "Ajuste frequência e volume para testar a resposta completa dos alto-falantes.",
                30, 55, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblDesc);

            // === Sound Type Selector ===
            _lblSoundTypeText = Theme.CreateLabel("Tipo de Som:", 30, 100, Theme.FontBody);
            Controls.Add(_lblSoundTypeText);
            _cmbSoundType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody
            };
            _cmbSoundType.Items.AddRange(new object[]
            {
                "🎵 Senoidal (Sine)",
                "🔊 Quadrada (Square)",
                "📻 Ruído Branco",
                "📈 Varredura (Sweep)",
                "🔔 Bip (Beep)",
                "💾 Áudio do Pendrive (USB)"
            });
            _cmbSoundType.SelectedIndex = 0;
            _cmbSoundType.SelectedIndexChanged += (s, e) =>
            {
                bool customVisible = _cmbSoundType.SelectedIndex == 5;
                ShowCustomAudioSection(customVisible);

                if (customVisible)
                {
                    _trkFrequency.Enabled = false;
                    ScanCustomAudio();
                }
                else
                {
                    _activeSoundType = (SoundType)_cmbSoundType.SelectedIndex;
                    bool freqEnabled = _activeSoundType != SoundType.Sweep && _activeSoundType != SoundType.WhiteNoise;
                    _trkFrequency.Enabled = freqEnabled;
                }

                OnResize(EventArgs.Empty);
                _speakerPanel.Invalidate();

                if (_isPlaying) RestartTone();
            };
            Controls.Add(_cmbSoundType);

            // === Frequency Slider ===
            _lblFreqText = Theme.CreateLabel("Frequência:", 30, 140, Theme.FontBody);
            Controls.Add(_lblFreqText);
            _trkFrequency = new TrackBar
            {
                Minimum = 50,
                Maximum = 15000,
                Value = 440,
                TickFrequency = 1000,
                SmallChange = 10,
                LargeChange = 100,
                BackColor = Theme.BgPrimary
            };
            _trkFrequency.ValueChanged += (s, e) =>
            {
                _currentFrequency = _trkFrequency.Value;
                _lblFreqValue.Text = FormatFrequency(_currentFrequency);
                if (_isPlaying && _signalGen != null && _cmbSoundType.SelectedIndex != 5)
                {
                    _signalGen.Frequency = _currentFrequency;
                }
            };
            Controls.Add(_trkFrequency);

            _lblFreqValue = Theme.CreateLabel("440 Hz", 620, 140, Theme.FontButton, Theme.Accent);
            Controls.Add(_lblFreqValue);

            // Frequency hint labels
            _lblFreqMin = Theme.CreateLabel("50 Hz", 150, 168, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblFreqMin);
            _lblFreqMax = Theme.CreateLabel("15 kHz", 560, 168, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblFreqMax);

            // === Volume Slider ===
            _lblVolText = Theme.CreateLabel("Volume:", 30, 190, Theme.FontBody);
            Controls.Add(_lblVolText);
            _trkVolume = new TrackBar
            {
                Minimum = 0,
                Maximum = 200,  // 0% to 200%
                Value = 50,     // 50% default (gain = 0.5)
                TickFrequency = 25,
                SmallChange = 5,
                LargeChange = 25,
                BackColor = Theme.BgPrimary
            };
            _trkVolume.ValueChanged += (s, e) =>
            {
                _currentGain = _trkVolume.Value / 100.0f;
                _lblVolValue.Text = $"{_trkVolume.Value}%";
                _lblVolValue.ForeColor = Theme.Accent;
                if (_isPlaying)
                {
                    if (_signalGen != null)
                    {
                        _signalGen.Gain = _currentGain;
                    }
                    if (_volumeProvider != null)
                    {
                        _volumeProvider.Volume = _currentGain;
                    }
                }
            };
            Controls.Add(_trkVolume);

            _lblVolValue = Theme.CreateLabel("50%", 620, 190, Theme.FontButton, Theme.Accent);
            Controls.Add(_lblVolValue);

            // Volume hint labels
            _lblVolMin = Theme.CreateLabel("0%", 150, 218, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblVolMin);
            _lblVol100 = Theme.CreateLabel("100%", 390, 218, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblVol100);
            _lblVolMax = Theme.CreateLabel("200% (boost)", 530, 218, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblVolMax);

            // === Custom Audio Section ===
            _lblCustomAudioText = Theme.CreateLabel("Áudio USB:", 30, 240, Theme.FontBody);
            Controls.Add(_lblCustomAudioText);

            _cmbCustomAudio = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = Theme.FontBody
            };
            _cmbCustomAudio.SelectedIndexChanged += (s, e) =>
            {
                if (_isPlaying) RestartTone();
            };
            Controls.Add(_cmbCustomAudio);

            _btnBrowseCustom = Theme.CreateSecondaryButton("📂 Procurar", 0, 0, 100, 30);
            _btnBrowseCustom.Click += (s, e) => BrowseCustomAudio();
            Controls.Add(_btnBrowseCustom);

            _btnRefreshCustom = Theme.CreateSecondaryButton("🔄 Atualizar", 0, 0, 100, 30);
            _btnRefreshCustom.Click += (s, e) => ScanCustomAudio();
            Controls.Add(_btnRefreshCustom);

            _lblCustomAudioInfo = Theme.CreateLabel("", 150, 275, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblCustomAudioInfo);

            // Initially hide custom audio section
            ShowCustomAudioSection(false);

            // Speaker visualization
            _speakerPanel = new Panel
            {
                BackColor = Theme.BgDark,
                BorderStyle = BorderStyle.None
            };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_speakerPanel, true);
            _speakerPanel.Paint += SpeakerPanel_Paint;
            _speakerPanel.Resize += (s, e) => _speakerPanel.Invalidate();
            Controls.Add(_speakerPanel);

            // Buttons
            _btnLeft = Theme.CreateButton("🔈 Esquerdo", 30, 465, 170, 45);
            _btnLeft.Click += (s, e) => PlayTone(AudioChannel.Left);
            Controls.Add(_btnLeft);

            _btnRight = Theme.CreateButton("🔈 Direito", 210, 465, 170, 45);
            _btnRight.Click += (s, e) => PlayTone(AudioChannel.Right);
            Controls.Add(_btnRight);

            _btnBoth = Theme.CreateButton("🔊 Ambos", 390, 465, 170, 45);
            _btnBoth.Click += (s, e) => PlayTone(AudioChannel.Both);
            Controls.Add(_btnBoth);

            _btnStop = Theme.CreateSecondaryButton("⏹ Parar", 570, 465, 130, 45);
            _btnStop.Click += (s, e) => StopTone();
            Controls.Add(_btnStop);

            _lblStatus = Theme.CreateLabel("⏸ Aguardando...", 30, 530, Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblStatus);

            _animTimer = new System.Windows.Forms.Timer { Interval = 33 };
            _animTimer.Tick += (s, e) =>
            {
                _animPhase += 0.15f;
                _speakerPanel.Invalidate();
            };

            _beepTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _beepTimer.Tick += (s, e) =>
            {
                if (_signalGen != null && _activeSoundType == SoundType.Beep && _cmbSoundType.SelectedIndex != 5)
                {
                    _beepMuted = !_beepMuted;
                    _signalGen.Gain = _beepMuted ? 0f : _currentGain;
                }
            };
        }

        private void ShowCustomAudioSection(bool visible)
        {
            _lblCustomAudioText.Visible = visible;
            _cmbCustomAudio.Visible = visible;
            _btnBrowseCustom.Visible = visible;
            _btnRefreshCustom.Visible = visible;
            _lblCustomAudioInfo.Visible = visible;
        }

        private void ScanCustomAudio()
        {
            _customAudioFiles.Clear();
            _cmbCustomAudio.Items.Clear();

            try
            {
                var drives = System.IO.DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.IsReady && (drive.DriveType == System.IO.DriveType.Removable || drive.DriveType == System.IO.DriveType.Fixed))
                    {
                        string path = System.IO.Path.Combine(drive.RootDirectory.FullName, "TechTestAudio");
                        if (System.IO.Directory.Exists(path))
                        {
                            var files = System.IO.Directory.GetFiles(path, "*.*", System.IO.SearchOption.TopDirectoryOnly);
                            foreach (var file in files)
                            {
                                string ext = System.IO.Path.GetExtension(file).ToLower();
                                if (ext == ".mp3" || ext == ".wav" || ext == ".wma" || ext == ".flac" || ext == ".ogg" || ext == ".aac" || ext == ".m4a")
                                {
                                    _customAudioFiles.Add(file);
                                    _cmbCustomAudio.Items.Add(System.IO.Path.GetFileName(file));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"⚠️ Erro ao escanear USB: {ex.Message}";
                _lblStatus.ForeColor = Theme.Warning;
            }

            if (_customAudioFiles.Count > 0)
            {
                _cmbCustomAudio.SelectedIndex = 0;
                _cmbCustomAudio.Enabled = true;
                _lblCustomAudioInfo.Text = $"✨ {_customAudioFiles.Count} áudio(s) encontrado(s) no Pendrive!";
                _lblCustomAudioInfo.ForeColor = Theme.Success;
            }
            else
            {
                _cmbCustomAudio.Items.Add("Nenhum áudio detectado em [Pendrive]:\\TechTestAudio");
                _cmbCustomAudio.SelectedIndex = 0;
                _cmbCustomAudio.Enabled = false;
                _lblCustomAudioInfo.Text = "📁 Crie a pasta 'TechTestAudio' na raiz do pendrive ou clique em 'Procurar' para selecionar.";
                _lblCustomAudioInfo.ForeColor = Theme.TextMuted;
            }
        }

        private void BrowseCustomAudio()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Arquivos de Áudio|*.mp3;*.wav;*.wma;*.flac;*.ogg;*.aac;*.m4a|Todos os Arquivos|*.*";
                ofd.Title = "Selecionar Áudio de Teste";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string filePath = ofd.FileName;
                    string fileName = System.IO.Path.GetFileName(filePath);

                    int existingIndex = _customAudioFiles.IndexOf(filePath);
                    if (existingIndex >= 0)
                    {
                        _cmbCustomAudio.SelectedIndex = existingIndex;
                    }
                    else
                    {
                        // Remove placeholder if it was there
                        if (_customAudioFiles.Count == 0 && _cmbCustomAudio.Items.Count > 0 &&
                            (_cmbCustomAudio.Items[0].ToString().Contains("Nenhum áudio") || _cmbCustomAudio.Items[0].ToString().Contains("TechTestAudio")))
                        {
                            _cmbCustomAudio.Items.Clear();
                        }

                        _customAudioFiles.Add(filePath);
                        _cmbCustomAudio.Items.Add("📂 " + fileName);
                        _cmbCustomAudio.SelectedIndex = _cmbCustomAudio.Items.Count - 1;
                        _cmbCustomAudio.Enabled = true;
                        _lblCustomAudioInfo.Text = $"✨ Selecionado: {fileName}";
                        _lblCustomAudioInfo.ForeColor = Theme.Success;
                    }

                    if (_isPlaying) RestartTone();
                }
            }
        }

        private string FormatFrequency(float freq)
        {
            if (freq >= 1000)
                return $"{freq / 1000.0:F1} kHz";
            return $"{(int)freq} Hz";
        }

        private void PlayTone(AudioChannel channel)
        {
            StopTone();
            try
            {
                _activeChannel = channel;

                if (_cmbSoundType.SelectedIndex == 5)
                {
                    // Custom audio mode!
                    if (_customAudioFiles == null || _customAudioFiles.Count == 0 || _cmbCustomAudio.SelectedIndex < 0 || _cmbCustomAudio.SelectedIndex >= _customAudioFiles.Count)
                    {
                        throw new Exception("Nenhum arquivo de áudio selecionado ou disponível.");
                    }

                    string filePath = _customAudioFiles[_cmbCustomAudio.SelectedIndex];
                    _customAudioReader = new AudioFileReader(filePath);

                    ISampleProvider sampleProvider = _customAudioReader;
                    if (_customAudioReader.WaveFormat.Channels == 1)
                    {
                        var monoToStereo = new MonoToStereoSampleProvider(_customAudioReader);
                        switch (channel)
                        {
                            case AudioChannel.Left: monoToStereo.LeftVolume = 1f; monoToStereo.RightVolume = 0f; break;
                            case AudioChannel.Right: monoToStereo.LeftVolume = 0f; monoToStereo.RightVolume = 1f; break;
                            case AudioChannel.Both: monoToStereo.LeftVolume = 1f; monoToStereo.RightVolume = 1f; break;
                        }
                        sampleProvider = monoToStereo;
                    }
                    else if (_customAudioReader.WaveFormat.Channels == 2)
                    {
                        var stereoVol = new ChannelVolumeSampleProvider(_customAudioReader);
                        switch (channel)
                        {
                            case AudioChannel.Left: stereoVol.LeftVolume = 1f; stereoVol.RightVolume = 0f; break;
                            case AudioChannel.Right: stereoVol.LeftVolume = 0f; stereoVol.RightVolume = 1f; break;
                            case AudioChannel.Both: stereoVol.LeftVolume = 1f; stereoVol.RightVolume = 1f; break;
                        }
                        sampleProvider = stereoVol;
                    }

                    _volumeProvider = new VolumeSampleProvider(sampleProvider) { Volume = _currentGain };

                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_volumeProvider);

                    _waveOut.PlaybackStopped += (s, ev) =>
                    {
                        if (this.IsDisposed) return;
                        try
                        {
                            this.BeginInvoke((MethodInvoker)delegate
                            {
                                if (_isPlaying && _cmbSoundType.SelectedIndex == 5)
                                {
                                    StopTone();
                                }
                            });
                        }
                        catch { }
                    };

                    _waveOut.Play();
                    _isPlaying = true;
                    _animTimer.Start();

                    string chName = channel == AudioChannel.Left ? "ESQUERDO" :
                                    channel == AudioChannel.Right ? "DIREITO" : "AMBOS";
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    _lblStatus.Text = $"▶ [USB] {fileName} — Vol {_trkVolume.Value}% — Canal {chName}";
                    _lblStatus.ForeColor = Theme.Success;
                }
                else
                {
                    // Standard tone mode
                    _signalGen = new SignalGenerator(44100, 1)
                    {
                        Frequency = _currentFrequency,
                        Gain = _currentGain
                    };

                    switch (_activeSoundType)
                    {
                        case SoundType.Sine:
                            _signalGen.Type = SignalGeneratorType.Sin;
                            break;
                        case SoundType.Square:
                            _signalGen.Type = SignalGeneratorType.Square;
                            break;
                        case SoundType.WhiteNoise:
                            _signalGen.Type = SignalGeneratorType.White;
                            break;
                        case SoundType.Sweep:
                            _signalGen.Type = SignalGeneratorType.Sweep;
                            _signalGen.Frequency = 100;
                            _signalGen.FrequencyEnd = 10000;
                            _signalGen.SweepLengthSecs = 5;
                            break;
                        case SoundType.Beep:
                            _signalGen.Type = SignalGeneratorType.Sin;
                            break;
                    }

                    var stereo = new MonoToStereoSampleProvider(_signalGen);
                    switch (channel)
                    {
                        case AudioChannel.Left:
                            stereo.RightVolume = 0f;
                            stereo.LeftVolume = 1f;
                            break;
                        case AudioChannel.Right:
                            stereo.LeftVolume = 0f;
                            stereo.RightVolume = 1f;
                            break;
                        case AudioChannel.Both:
                            stereo.LeftVolume = 1f;
                            stereo.RightVolume = 1f;
                            break;
                    }

                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(stereo);
                    _waveOut.Play();
                    _isPlaying = true;
                    _animTimer.Start();

                    if (_activeSoundType == SoundType.Beep)
                    {
                        _beepMuted = false;
                        _beepTimer.Start();
                    }

                    string chName = channel == AudioChannel.Left ? "ESQUERDO" :
                                    channel == AudioChannel.Right ? "DIREITO" : "AMBOS";
                    string typeName = _cmbSoundType.SelectedItem?.ToString() ?? "Senoidal";
                    _lblStatus.Text = $"▶ {typeName} — {FormatFrequency(_currentFrequency)} — Vol {_trkVolume.Value}% — Canal {chName}";
                    _lblStatus.ForeColor = Theme.Success;
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"❌ Erro: {ex.Message}";
                _lblStatus.ForeColor = Theme.Error;
            }
        }

        private void RestartTone()
        {
            if (!_isPlaying) return;
            PlayTone(_activeChannel);
        }

        private void StopTone()
        {
            _isPlaying = false;
            _animTimer.Stop();
            _beepTimer.Stop();
            _beepMuted = false;
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
            }
            catch { }
            try
            {
                _customAudioReader?.Dispose();
            }
            catch { }
            _waveOut = null;
            _customAudioReader = null;
            _volumeProvider = null;
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
            int nbW = 500, nbH = 110;
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
            var screenRect = new Rectangle(nbX + 30, nbY + 15, nbW - 60, nbH - 45);
            using (var path = Theme.RoundedRect(screenRect, 6))
            using (var brush = new SolidBrush(Color.FromArgb(20, 20, 40)))
                g.FillPath(brush, path);

            // Frequency display inside "screen"
            if (_isPlaying)
            {
                string displayText = FormatFrequency(_currentFrequency);
                if (_cmbSoundType.SelectedIndex == 5 && _customAudioFiles.Count > 0 && _cmbCustomAudio.SelectedIndex >= 0)
                {
                    string filePath = _customAudioFiles[_cmbCustomAudio.SelectedIndex];
                    displayText = "🎵 " + System.IO.Path.GetFileNameWithoutExtension(filePath);
                }

                float fontSize = displayText.Length > 20 ? 9f : displayText.Length > 12 ? 11f : 14f;
                using (var font = new Font("Segoe UI", Theme.S(fontSize), FontStyle.Bold))
                {
                    var sz = g.MeasureString(displayText, font);
                    float tx = screenRect.X + (screenRect.Width - sz.Width) / 2;
                    float ty = screenRect.Y + (screenRect.Height - sz.Height) / 2;
                    g.DrawString(displayText, font, new SolidBrush(Theme.Accent), tx, ty);
                }
            }
            else
            {
                // Notebook label
                using (var font = Theme.FontSmall)
                {
                    var sz = g.MeasureString("NOTEBOOK", font);
                    g.DrawString("NOTEBOOK", font, new SolidBrush(Theme.TextMuted),
                        nbX + (nbW - sz.Width) / 2, nbY + nbH - 28);
                }
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

    public class ChannelVolumeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public float LeftVolume { get; set; } = 1.0f;
        public float RightVolume { get; set; } = 1.0f;

        public ChannelVolumeSampleProvider(ISampleProvider source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            int channels = WaveFormat.Channels;
            if (channels == 2)
            {
                for (int i = 0; i < read; i += 2)
                {
                    buffer[offset + i] *= LeftVolume;
                    buffer[offset + i + 1] *= RightVolume;
                }
            }
            return read;
        }
    }
}
