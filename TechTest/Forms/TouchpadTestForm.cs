using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class TouchpadTestForm : Form
    {
        private Panel _canvas;
        private Bitmap _canvasBitmap;
        private Graphics _canvasGraphics;
        private Label _lblMoved, _lblLeftClick, _lblRightClick, _lblScrollUp, _lblScrollDown, _lblDrag;
        private Label _lblCounter;
        private Button _btnClear;
        private bool _isDragging;
        private Point _lastPoint = Point.Empty;
        private HashSet<string> _completedChecks = new HashSet<string>();

        // Mode Selection Controls
        private RadioButton _radFreeDraw;
        private RadioButton _radGridMode;
        private Label _lblModeText;
        private Label _lblGridProgress;

        // Grid parameters: 10 columns by 8 rows (10x8) = 80 zones
        private const int GridCols = 10;
        private const int GridRows = 8;
        private bool[,] _visitedZones = new bool[GridCols, GridRows];
        private Point _currentZone = new Point(-1, -1);
        private int _totalZones = GridCols * GridRows;
        private int _visitedCount = 0;

        // Pointer Lock & 2D Absolute Coordinates mapping
        // X ranges from 0 to 3000, Y ranges from 0 to 2000
        private bool _isPointerLocked = false;
        private Point _lockPoint = Point.Empty;
        private float _rawTouchX = 1500f; // Initialized in the center (0 to 3000)
        private float _rawTouchY = 1000f; // Initialized in the center (0 to 2000)

        // Math Mapping visualizer fields
        private GroupBox _grpMathInfo;
        private Label _lblMathDriverRes;
        private Label _lblMathGridSize;
        private Label _lblMathFormulaX;
        private Label _lblMathFormulaY;
        private Label _lblMathCurrentRaw;
        private Label _lblMathMappingResult;

        // Fields for responsive layout positioning
        private Label _lblTitle, _lblDesc, _lblChecklistTitle, _lblLegend;

        public TouchpadTestForm()
        {
            Theme.StyleForm(this, "🖱 Teste de Touchpad", 900, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(750), Theme.S(500));
            this.KeyPreview = true; // Required to capture the ESC key globally
            BuildUI();

            // Set initial layout placement
            OnResize(EventArgs.Empty);
        }

        private void UpdateCanvasSize(int w, int h)
        {
            if (w < 1) w = 1;
            if (h < 1) h = 1;

            if (_canvasBitmap != null && _canvasBitmap.Width == w && _canvasBitmap.Height == h)
                return;

            Bitmap oldBitmap = _canvasBitmap;
            Graphics oldGraphics = _canvasGraphics;

            _canvasBitmap = new Bitmap(w, h);
            _canvasGraphics = Graphics.FromImage(_canvasBitmap);
            _canvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (oldBitmap != null)
            {
                _canvasGraphics.Clear(Theme.BgDark);
                _canvasGraphics.DrawImage(oldBitmap, 0, 0);
                oldGraphics?.Dispose();
                oldBitmap?.Dispose();
            }
            else
            {
                _canvasGraphics.Clear(Theme.BgDark);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_canvas == null) return;

            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            // Header labels layout
            if (_lblTitle != null)
                _lblTitle.Location = new Point(Theme.S(30), Theme.S(15));

            if (_lblDesc != null)
                _lblDesc.Location = new Point(Theme.S(30), Theme.S(48));

            // Mode Selector positioning
            if (_lblModeText != null)
                _lblModeText.Location = new Point(Theme.S(30), Theme.S(80));

            if (_radFreeDraw != null)
                _radFreeDraw.Location = new Point(Theme.S(150), Theme.S(76));

            if (_radGridMode != null)
                _radGridMode.Location = new Point(Theme.S(300), Theme.S(76));

            // Canvas sizes: left margin = 30, right margin = 260 for checklist
            int canvasLeft = Theme.S(30);
            int canvasTop = Theme.S(115); // pushed down by mode selector
            int rightPanelW = Theme.S(240);
            int bottomMargin = Theme.S(85);

            int canvasW = clientW - canvasLeft - rightPanelW;
            int canvasH = clientH - canvasTop - bottomMargin;

            if (canvasW < Theme.S(100)) canvasW = Theme.S(100);
            if (canvasH < Theme.S(100)) canvasH = Theme.S(100);

            _canvas.Location = new Point(canvasLeft, canvasTop);
            _canvas.Size = new Size(canvasW, canvasH);

            // Recreate/resize the canvas drawing bitmap without clearing it
            UpdateCanvasSize(canvasW, canvasH);

            // Checklist positioning on the right side
            int rightX = _canvas.Right + Theme.S(20);
            if (_lblChecklistTitle != null)
                _lblChecklistTitle.Location = new Point(rightX, Theme.S(115));

            int checkY = Theme.S(140);
            int step = Theme.S(26);
            if (_lblMoved != null) { _lblMoved.Location = new Point(rightX, checkY); checkY += step; }
            if (_lblLeftClick != null) { _lblLeftClick.Location = new Point(rightX, checkY); checkY += step; }
            if (_lblRightClick != null) { _lblRightClick.Location = new Point(rightX, checkY); checkY += step; }
            if (_lblScrollUp != null) { _lblScrollUp.Location = new Point(rightX, checkY); checkY += step; }
            if (_lblScrollDown != null) { _lblScrollDown.Location = new Point(rightX, checkY); checkY += step; }
            if (_lblDrag != null) { _lblDrag.Location = new Point(rightX, checkY); checkY += step; }

            if (_lblGridProgress != null)
                _lblGridProgress.Location = new Point(rightX, checkY + Theme.S(2));

            if (_lblCounter != null)
                _lblCounter.Location = new Point(rightX, checkY + Theme.S(22));

            if (_grpMathInfo != null)
            {
                _grpMathInfo.Location = new Point(rightX, checkY + Theme.S(45));
                _grpMathInfo.Size = new Size(Theme.S(230), Theme.S(165));
            }

            if (_btnClear != null)
            {
                if (_grpMathInfo != null && _grpMathInfo.Visible)
                {
                    _btnClear.Location = new Point(rightX, _grpMathInfo.Bottom + Theme.S(8));
                }
                else
                {
                    _btnClear.Location = new Point(rightX, checkY + Theme.S(50));
                }
            }

            // Legend at the bottom
            if (_lblLegend != null)
                _lblLegend.Location = new Point(Theme.S(30), clientH - Theme.S(55));
        }

        private void BuildUI()
        {
            _lblTitle = Theme.CreateLabel("Teste de Touchpad", 30, 15, Theme.FontHeader);
            Controls.Add(_lblTitle);

            _lblDesc = Theme.CreateLabel(
                "Use o touchpad para realizar cada ação. O canvas registra seus movimentos.",
                30, 48, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblDesc);

            // === Mode Selector ===
            _lblModeText = Theme.CreateLabel("Modo de Teste:", 30, 80, Theme.FontBody);
            Controls.Add(_lblModeText);

            _radFreeDraw = new RadioButton
            {
                Text = "🖊️ Desenho Livre",
                Location = new Point(Theme.S(150), Theme.S(76)),
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Checked = true
            };
            _radFreeDraw.CheckedChanged += (s, e) => { if (_radFreeDraw.Checked) SwitchMode(false); };
            Controls.Add(_radFreeDraw);

            _radGridMode = new RadioButton
            {
                Text = "🔲 Grade de Zonas (Quadrados)",
                Location = new Point(Theme.S(300), Theme.S(76)),
                Font = Theme.FontBody,
                ForeColor = Theme.TextPrimary,
                AutoSize = true
            };
            _radGridMode.CheckedChanged += (s, e) => { if (_radGridMode.Checked) SwitchMode(true); };
            Controls.Add(_radGridMode);

            // Canvas
            _canvas = new Panel
            {
                BackColor = Theme.BgDark,
                Cursor = Cursors.Cross,
                BorderStyle = BorderStyle.None
            };
            _canvas.Paint += Canvas_Paint;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseUp += Canvas_MouseUp;
            _canvas.MouseWheel += Canvas_MouseWheel;
            _canvas.MouseLeave += (s, e) =>
            {
                if (_radGridMode != null && _radGridMode.Checked && !_isPointerLocked)
                {
                    _currentZone = new Point(-1, -1);
                    _canvas.Invalidate();
                }
            };
            Controls.Add(_canvas);

            // Canvas border drawing handler
            _canvas.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, _canvas.Width - 1, _canvas.Height - 1);
            };

            // Checklist panel
            _lblChecklistTitle = Theme.CreateLabel("Checklist", 640, 115, Theme.FontButton);
            Controls.Add(_lblChecklistTitle);

            int checkY = Theme.S(150);
            _lblMoved = CreateCheckLabel("Movimento detectado", 640, checkY); checkY += Theme.S(35);
            _lblLeftClick = CreateCheckLabel("Clique esquerdo", 640, checkY); checkY += Theme.S(35);
            _lblRightClick = CreateCheckLabel("Clique direito", 640, checkY); checkY += Theme.S(35);
            _lblScrollUp = CreateCheckLabel("Scroll para cima", 640, checkY); checkY += Theme.S(35);
            _lblScrollDown = CreateCheckLabel("Scroll para baixo", 640, checkY); checkY += Theme.S(35);
            _lblDrag = CreateCheckLabel("Arrastar (drag)", 640, checkY); checkY += Theme.S(35);

            // Grid Progress Label
            _lblGridProgress = Theme.CreateLabel("0 / 80 zonas tocadas", 640, checkY + Theme.S(5), Theme.FontBody, Theme.TextSecondary);
            _lblGridProgress.Visible = false;
            Controls.Add(_lblGridProgress);

            _lblCounter = Theme.CreateLabel("0 / 6 testes", 640, checkY + Theme.S(30), Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblCounter);

            // Buttons
            _btnClear = Theme.CreateSecondaryButton("🔄 Limpar", 640, checkY + Theme.S(65), 120, 36);
            _btnClear.Click += (s, e) => ClearAll();
            Controls.Add(_btnClear);

            // Math panel definition for absolute 10x8 driver calculation display
            _grpMathInfo = new GroupBox
            {
                Text = "📐 Matemática do Mapeamento",
                ForeColor = Theme.Accent,
                Font = new Font(Theme.FontButton.FontFamily, 9f, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Visible = false // Only visible in Grid Mode
            };

            _lblMathDriverRes = new Label
            {
                Text = "Res. Driver Raw: 0 a 3000 x 0 a 2000",
                Location = new Point(Theme.S(10), Theme.S(22)),
                AutoSize = true,
                Font = new Font(Theme.FontSmall.FontFamily, 8f),
                ForeColor = Theme.TextSecondary
            };
            _grpMathInfo.Controls.Add(_lblMathDriverRes);

            _lblMathGridSize = new Label
            {
                Text = "Grade: X (Largura)=10 > Y (Altura)=8",
                Location = new Point(Theme.S(10), Theme.S(40)),
                AutoSize = true,
                Font = new Font(Theme.FontSmall.FontFamily, 8f),
                ForeColor = Theme.TextSecondary
            };
            _grpMathInfo.Controls.Add(_lblMathGridSize);

            _lblMathFormulaX = new Label
            {
                Text = "Passo X: 3000 / 10 = 300 p/ Coluna",
                Location = new Point(Theme.S(10), Theme.S(58)),
                AutoSize = true,
                Font = new Font(Theme.FontSmall.FontFamily, 8f),
                ForeColor = Theme.TextMuted
            };
            _grpMathInfo.Controls.Add(_lblMathFormulaX);

            _lblMathFormulaY = new Label
            {
                Text = "Passo Y: 2000 / 8 = 250 p/ Linha",
                Location = new Point(Theme.S(10), Theme.S(76)),
                AutoSize = true,
                Font = new Font(Theme.FontSmall.FontFamily, 8f),
                ForeColor = Theme.TextMuted
            };
            _grpMathInfo.Controls.Add(_lblMathFormulaY);

            _lblMathCurrentRaw = new Label
            {
                Text = "Raw Toque: X = 1500 | Y = 1000",
                Location = new Point(Theme.S(10), Theme.S(100)),
                AutoSize = true,
                Font = new Font(Theme.FontBody.FontFamily, 8.5f, FontStyle.Regular),
                ForeColor = Theme.TextPrimary
            };
            _grpMathInfo.Controls.Add(_lblMathCurrentRaw);

            _lblMathMappingResult = new Label
            {
                Text = "Mapeado: Coluna 5 | Linha 4",
                Location = new Point(Theme.S(10), Theme.S(120)),
                AutoSize = true,
                Font = new Font(Theme.FontBody.FontFamily, 9f, FontStyle.Bold),
                ForeColor = Theme.Success
            };
            _grpMathInfo.Controls.Add(_lblMathMappingResult);

            Controls.Add(_grpMathInfo);

            // Legend
            _lblLegend = Theme.CreateLabel("🟢 Clique esquerdo  🔵 Clique direito  ⬜ Arraste",
                30, 500, Theme.FontSmall, Theme.TextMuted);
            Controls.Add(_lblLegend);
        }

        private Label CreateCheckLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = $"⬜ {text}",
                Location = new Point(x, y),
                AutoSize = true,
                Font = Theme.FontBody,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent
            };
            Controls.Add(lbl);
            return lbl;
        }

        private void CompleteCheck(string checkName, Label label)
        {
            if (_completedChecks.Contains(checkName)) return;
            _completedChecks.Add(checkName);
            label.Text = "✅" + label.Text.Substring(1);
            label.ForeColor = Theme.Success;
            _lblCounter.Text = $"{_completedChecks.Count} / 6 testes";

            if (_completedChecks.Count >= 6)
            {
                _lblCounter.ForeColor = Theme.Success;
                _lblCounter.Text = "✅ 6 / 6 — Todos os testes OK!";
            }
        }

        private void SwitchMode(bool gridMode)
        {
            _lblGridProgress.Visible = gridMode;
            if (_grpMathInfo != null)
            {
                _grpMathInfo.Visible = gridMode;
            }
            if (gridMode)
            {
                _lblLegend.Text = "🟢 Quadrado visitado  🔵 Posição atual";
                _lblDesc.Text = "Modo Grade: Clique no canvas para ocultar o cursor e testar a posição absoluta (X:0-3000, Y:0-2000). Pressione ESC para sair.";
                _lblDesc.ForeColor = Theme.Accent;
            }
            else
            {
                UnlockPointer();
                _lblLegend.Text = "🟢 Clique esquerdo  🔵 Clique direito  ⬜ Arraste";
                _lblDesc.Text = "Use o touchpad para realizar cada ação. O canvas registra seus movimentos.";
                _lblDesc.ForeColor = Theme.TextSecondary;
            }

            _currentZone = new Point(-1, -1);
            OnResize(EventArgs.Empty); // Re-layout to accommodate math info panel and move the Clear button
            _canvas.Invalidate();
        }

        private void LockPointer()
        {
            if (_isPointerLocked) return;

            _isPointerLocked = true;
            Cursor.Hide();

            // Calculate center of canvas in screen coordinates
            _lockPoint = _canvas.PointToScreen(new Point(_canvas.Width / 2, _canvas.Height / 2));
            Cursor.Position = _lockPoint;

            _lblGridProgress.Text = "🔒 Cursor Oculto e Travado! Pressione ESC para sair.";
            _lblGridProgress.ForeColor = Theme.Accent;
        }

        private void UnlockPointer()
        {
            if (!_isPointerLocked) return;

            _isPointerLocked = false;
            Cursor.Show();

            _currentZone = new Point(-1, -1);
            _canvas.Invalidate();

            UpdateGridProgress();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                if (_isPointerLocked)
                {
                    UnlockPointer();
                    e.Handled = true;
                }
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            UnlockPointer(); // Make sure cursor is restored if app loses focus
        }

        private void UpdateGridProgress()
        {
            if (_lblGridProgress != null)
            {
                if (_isPointerLocked)
                {
                    _lblGridProgress.Text = $"🔒 X:{(int)_rawTouchX} Y:{(int)_rawTouchY} | {_visitedCount} / {_totalZones} zonas (ESC p/ sair)";
                    _lblGridProgress.ForeColor = Theme.Accent;
                }
                else
                {
                    _lblGridProgress.Text = $"{_visitedCount} / {_totalZones} zonas tocadas";
                    if (_visitedCount >= _totalZones)
                    {
                        _lblGridProgress.Text = $"✅ {_totalZones} / {_totalZones} — Touchpad 100% funcional!";
                        _lblGridProgress.ForeColor = Theme.Success;
                    }
                    else
                    {
                        _lblGridProgress.ForeColor = Theme.TextSecondary;
                    }
                }
            }
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_radGridMode != null && _radGridMode.Checked)
            {
                // Draw Grid Mode (10 cols x 8 rows)
                int w = _canvas.Width;
                int h = _canvas.Height;
                float cellW = (float)w / GridCols;
                float cellH = (float)h / GridRows;

                for (int col = 0; col < GridCols; col++)
                {
                    for (int row = 0; row < GridRows; row++)
                    {
                        float x = col * cellW;
                        float y = row * cellH;

                        RectangleF rect = new RectangleF(x, y, cellW, cellH);

                        // Pick background color
                        Color bgColor = Theme.BgDark;
                        if (_currentZone.X == col && _currentZone.Y == row)
                        {
                            bgColor = Color.FromArgb(50, Theme.Accent); // Highlight current zone (blue)
                        }
                        else if (_visitedZones[col, row])
                        {
                            bgColor = Color.FromArgb(40, Theme.Success); // Visited zone (green)
                        }

                        using (var brush = new SolidBrush(bgColor))
                            g.FillRectangle(brush, rect);

                        // Draw borders
                        Color borderColor = Theme.Border;
                        float borderWidth = 1f;

                        if (_currentZone.X == col && _currentZone.Y == row)
                        {
                            borderColor = Theme.Accent;
                            borderWidth = 2.5f;
                        }
                        else if (_visitedZones[col, row])
                        {
                            borderColor = Theme.Success;
                            borderWidth = 1.5f;
                        }

                        using (var pen = new Pen(borderColor, borderWidth))
                        {
                            g.DrawRectangle(pen, x + borderWidth / 2, y + borderWidth / 2, cellW - borderWidth, cellH - borderWidth);
                        }
                    }
                }
            }
            else
            {
                // Draw Free Draw Mode
                if (_canvasBitmap != null)
                    g.DrawImage(_canvasBitmap, 0, 0);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            CompleteCheck("move", _lblMoved);

            if (_radGridMode != null && _radGridMode.Checked)
            {
                if (_isPointerLocked)
                {
                    // Calculate relative movements from lock point
                    Point currentScreenPos = Cursor.Position;
                    int deltaX = currentScreenPos.X - _lockPoint.X;
                    int deltaY = currentScreenPos.Y - _lockPoint.Y;

                    if (deltaX != 0 || deltaY != 0)
                    {
                        // Accumulate movement into absolute 2D touchpad coordinates
                        // X range: 0 to 3000, Y range: 0 to 2000
                        float sensitivity = 1.8f;
                        _rawTouchX += deltaX * sensitivity;
                        _rawTouchY += deltaY * sensitivity;

                        // Clamp values to defined touchpad ranges
                        _rawTouchX = Math.Max(0f, Math.Min(3000f, _rawTouchX));
                        _rawTouchY = Math.Max(0f, Math.Min(2000f, _rawTouchY));

                        // Force the pointer back to the lock point to maintain block
                        Cursor.Position = _lockPoint;

                        // Mapeamento Matemático (10 colunas por 8 linhas):
                        // Largura X (0..3000) dividida por 10 colunas -> largura do quadrado = 300
                        // Altura Y (0..2000) dividida por 8 linhas -> altura do quadrado = 250
                        int col = (int)(_rawTouchX / 300f);
                        int row = (int)(_rawTouchY / 250f);

                        // Clamp mapped column and row indexes
                        col = Math.Max(0, Math.Min(GridCols - 1, col));
                        row = Math.Max(0, Math.Min(GridRows - 1, row));

                        if (_lblMathCurrentRaw != null)
                        {
                            _lblMathCurrentRaw.Text = $"Raw Toque: X = {(int)_rawTouchX} | Y = {(int)_rawTouchY}";
                        }
                        if (_lblMathMappingResult != null)
                        {
                            _lblMathMappingResult.Text = $"Mapeado: Coluna {col + 1} | Linha {row + 1}";
                        }

                        Point newZone = new Point(col, row);
                        if (newZone != _currentZone)
                        {
                            _currentZone = newZone;

                            if (!_visitedZones[col, row])
                            {
                                _visitedZones[col, row] = true;
                                _visitedCount = 0;
                                for (int c = 0; c < GridCols; c++)
                                    for (int r = 0; r < GridRows; r++)
                                        if (_visitedZones[c, r]) _visitedCount++;

                                UpdateGridProgress();
                            }
                        }

                        _canvas.Invalidate();
                    }
                }
                else
                {
                    // Standard hover highlighted cell if not locked
                    int w = _canvas.Width;
                    int h = _canvas.Height;
                    if (w > 0 && h > 0)
                    {
                        float cellW = (float)w / GridCols;
                        float cellH = (float)h / GridRows;

                        int col = (int)(e.X / cellW);
                        int row = (int)(e.Y / cellH);

                        col = Math.Max(0, Math.Min(col, GridCols - 1));
                        row = Math.Max(0, Math.Min(row, GridRows - 1));

                        Point newZone = new Point(col, row);
                        if (newZone != _currentZone)
                        {
                            _currentZone = newZone;
                            _canvas.Invalidate();
                        }
                    }
                }
            }
            else
            {
                // Free Draw Mode logic
                if (_isDragging && _lastPoint != Point.Empty)
                {
                    using (var pen = new Pen(Theme.TextPrimary, 2f))
                        _canvasGraphics.DrawLine(pen, _lastPoint, e.Location);
                    _canvas.Invalidate();
                    CompleteCheck("drag", _lblDrag);
                }
                else if (e.Button == MouseButtons.None)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(30, Theme.Accent)))
                        _canvasGraphics.FillEllipse(brush, e.X - 2, e.Y - 2, 4, 4);
                    _canvas.Invalidate();
                }
            }

            _lastPoint = e.Location;
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_radGridMode != null && _radGridMode.Checked)
            {
                // Clicking in Grid mode registers clicks and toggles pointer lock/hiding
                if (!_isPointerLocked)
                {
                    LockPointer();
                }

                if (e.Button == MouseButtons.Left)
                {
                    CompleteCheck("leftclick", _lblLeftClick);
                }
                else if (e.Button == MouseButtons.Right)
                {
                    CompleteCheck("rightclick", _lblRightClick);
                }
            }
            else
            {
                // Free Draw Mode logic
                if (e.Button == MouseButtons.Left)
                {
                    CompleteCheck("leftclick", _lblLeftClick);
                    using (var brush = new SolidBrush(Theme.Success))
                        _canvasGraphics.FillEllipse(brush, e.X - 6, e.Y - 6, 12, 12);
                    _isDragging = true;
                    _lastPoint = e.Location;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    CompleteCheck("rightclick", _lblRightClick);
                    using (var brush = new SolidBrush(Theme.Accent))
                        _canvasGraphics.FillEllipse(brush, e.X - 6, e.Y - 6, 12, 12);
                }
            }
            _canvas.Invalidate();
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_radFreeDraw.Checked)
            {
                _isDragging = false;
                _lastPoint = Point.Empty;
            }
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                CompleteCheck("scrollup", _lblScrollUp);
                if (_radFreeDraw.Checked)
                {
                    using (var brush = new SolidBrush(Theme.Warning))
                        _canvasGraphics.FillPolygon(brush, new Point[]
                        {
                            new Point(e.X, e.Y - 10),
                            new Point(e.X - 6, e.Y + 4),
                            new Point(e.X + 6, e.Y + 4)
                        });
                }
            }
            else
            {
                CompleteCheck("scrolldown", _lblScrollDown);
                if (_radFreeDraw.Checked)
                {
                    using (var brush = new SolidBrush(Theme.Warning))
                        g_draw_polygon(brush, e.X, e.Y);
                }
            }
            _canvas.Invalidate();
        }

        private void g_draw_polygon(Brush brush, int x, int y)
        {
            _canvasGraphics.FillPolygon(brush, new Point[]
            {
                new Point(x, y + 10),
                new Point(x - 6, y - 4),
                new Point(x + 6, y - 4)
            });
        }

        private void ClearAll()
        {
            if (_canvasGraphics != null)
                _canvasGraphics.Clear(Theme.BgDark);

            Array.Clear(_visitedZones, 0, _visitedZones.Length);
            _visitedCount = 0;
            _currentZone = new Point(-1, -1);
            _rawTouchX = 1500f; // Reset raw coordinates
            _rawTouchY = 1000f;

            if (_lblMathCurrentRaw != null)
            {
                _lblMathCurrentRaw.Text = "Raw Toque: X = 1500 | Y = 1000";
            }
            if (_lblMathMappingResult != null)
            {
                _lblMathMappingResult.Text = "Mapeado: Coluna 5 | Linha 4";
            }

            UpdateGridProgress();

            _canvas.Invalidate();
            _completedChecks.Clear();

            _lblMoved.Text = "⬜ Movimento detectado"; _lblMoved.ForeColor = Theme.TextSecondary;
            _lblLeftClick.Text = "⬜ Clique esquerdo"; _lblLeftClick.ForeColor = Theme.TextSecondary;
            _lblRightClick.Text = "⬜ Clique direito"; _lblRightClick.ForeColor = Theme.TextSecondary;
            _lblScrollUp.Text = "⬜ Scroll para cima"; _lblScrollUp.ForeColor = Theme.TextSecondary;
            _lblScrollDown.Text = "⬜ Scroll para baixo"; _lblScrollDown.ForeColor = Theme.TextSecondary;
            _lblDrag.Text = "⬜ Arrastar (drag)"; _lblDrag.ForeColor = Theme.TextSecondary;
            _lblCounter.Text = "0 / 6 testes";
            _lblCounter.ForeColor = Theme.TextSecondary;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnlockPointer(); // Restore normal cursor
            _canvasGraphics?.Dispose();
            _canvasBitmap?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
