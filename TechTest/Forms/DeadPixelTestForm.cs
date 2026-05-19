using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class DeadPixelTestForm : Form
    {
        private readonly Color[] _colors = new Color[]
        {
            Color.Black,
            Color.White,
            Color.Red,
            Color.FromArgb(0, 255, 0),
            Color.Blue,
            Color.Magenta,
            Color.Cyan,
            Color.Yellow
        };

        private readonly string[] _colorNames = new string[]
        {
            "Preto (detecta stuck-on)", "Branco (detecta dead pixel)",
            "Vermelho (subpixel R)", "Verde (subpixel G)",
            "Azul (subpixel B)", "Magenta", "Ciano", "Amarelo"
        };

        private int _colorIndex;
        private bool _showGradient;
        private bool _showCheckerboard;
        private Label _lblInstruction;
        private Label _lblColorInfo;
        private System.Windows.Forms.Timer _hideTimer;
        private bool _instructionsVisible = true;

        public DeadPixelTestForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.BackColor = Color.Black;
            this.Cursor = Cursors.Cross;
            this.KeyPreview = true;
            this.DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _lblInstruction = new Label
            {
                Text = "ESPAÇO / Clique = Próxima cor  |  G = Gradiente  |  X = Xadrez  |  ESC = Sair",
                AutoSize = true,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(180, 200, 200, 200),
                BackColor = Color.FromArgb(120, 0, 0, 0),
                Padding = new Padding(15, 8, 15, 8)
            };
            Controls.Add(_lblInstruction);

            _lblColorInfo = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(150, 200, 200, 200),
                BackColor = Color.FromArgb(100, 0, 0, 0),
                Padding = new Padding(10, 5, 10, 5)
            };
            Controls.Add(_lblColorInfo);

            UpdateColorInfo();

            this.Resize += (s, e) => PositionLabels();
            this.Click += (s, e) => NextColor();
            this.KeyDown += OnKeyDown;
            this.MouseMove += (s, e) => ShowInstructions();

            _hideTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _hideTimer.Tick += (s, e) =>
            {
                _hideTimer.Stop();
                _lblInstruction.Visible = false;
                _instructionsVisible = false;
            };
            _hideTimer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PositionLabels();
        }

        private void PositionLabels()
        {
            _lblInstruction.Location = new Point(
                (this.ClientSize.Width - _lblInstruction.Width) / 2, 20);
            _lblColorInfo.Location = new Point(10, this.ClientSize.Height - _lblColorInfo.Height - 10);
        }

        private void ShowInstructions()
        {
            if (!_instructionsVisible)
            {
                _lblInstruction.Visible = true;
                _instructionsVisible = true;
            }
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
                case Keys.Space:
                case Keys.Right:
                case Keys.Enter:
                    NextColor();
                    break;
                case Keys.Left:
                    PrevColor();
                    break;
                case Keys.G:
                    _showGradient = true;
                    _showCheckerboard = false;
                    Invalidate();
                    UpdateColorInfo();
                    break;
                case Keys.X:
                    _showCheckerboard = true;
                    _showGradient = false;
                    Invalidate();
                    UpdateColorInfo();
                    break;
            }
            e.Handled = true;
        }

        private void NextColor()
        {
            _showGradient = false;
            _showCheckerboard = false;
            _colorIndex = (_colorIndex + 1) % _colors.Length;
            this.BackColor = _colors[_colorIndex];
            Invalidate();
            UpdateColorInfo();
            UpdateLabelColors();
        }

        private void PrevColor()
        {
            _showGradient = false;
            _showCheckerboard = false;
            _colorIndex = (_colorIndex - 1 + _colors.Length) % _colors.Length;
            this.BackColor = _colors[_colorIndex];
            Invalidate();
            UpdateColorInfo();
            UpdateLabelColors();
        }

        private void UpdateColorInfo()
        {
            string text;
            if (_showGradient)
                text = "Modo: Gradiente horizontal";
            else if (_showCheckerboard)
                text = "Modo: Xadrez (checkerboard)";
            else
                text = $"Cor {_colorIndex + 1}/{_colors.Length}: {_colorNames[_colorIndex]}";

            _lblColorInfo.Text = text;
            PositionLabels();
        }

        private void UpdateLabelColors()
        {
            bool isDark = _colorIndex == 0 || _colorIndex == 4;
            Color fg = isDark ? Color.FromArgb(180, 200, 200, 200) : Color.FromArgb(180, 30, 30, 30);
            Color bg = isDark ? Color.FromArgb(100, 0, 0, 0) : Color.FromArgb(100, 255, 255, 255);

            _lblInstruction.ForeColor = fg;
            _lblInstruction.BackColor = bg;
            _lblColorInfo.ForeColor = fg;
            _lblColorInfo.BackColor = bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            if (_showGradient)
            {
                using (var brush = new LinearGradientBrush(
                    this.ClientRectangle, Color.Black, Color.White, 0f))
                {
                    g.FillRectangle(brush, this.ClientRectangle);
                }
            }
            else if (_showCheckerboard)
            {
                int cellSize = 20;
                for (int x = 0; x < this.ClientSize.Width; x += cellSize)
                {
                    for (int y = 0; y < this.ClientSize.Height; y += cellSize)
                    {
                        bool isWhite = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                        using (var brush = new SolidBrush(isWhite ? Color.White : Color.Black))
                            g.FillRectangle(brush, x, y, cellSize, cellSize);
                    }
                }
            }
        }
    }
}
