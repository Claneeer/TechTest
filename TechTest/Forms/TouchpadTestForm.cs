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

        // Fields for responsive layout positioning
        private Label _lblTitle, _lblDesc, _lblChecklistTitle, _lblLegend;

        public TouchpadTestForm()
        {
            Theme.StyleForm(this, "🖱 Teste de Touchpad", 900, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(750), Theme.S(500));
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

            // Canvas sizes: left margin = 30, right margin = 260 for checklist
            int canvasLeft = Theme.S(30);
            int canvasTop = Theme.S(85);
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
                _lblChecklistTitle.Location = new Point(rightX, Theme.S(85));

            int checkY = Theme.S(120);
            if (_lblMoved != null) { _lblMoved.Location = new Point(rightX, checkY); checkY += Theme.S(35); }
            if (_lblLeftClick != null) { _lblLeftClick.Location = new Point(rightX, checkY); checkY += Theme.S(35); }
            if (_lblRightClick != null) { _lblRightClick.Location = new Point(rightX, checkY); checkY += Theme.S(35); }
            if (_lblScrollUp != null) { _lblScrollUp.Location = new Point(rightX, checkY); checkY += Theme.S(35); }
            if (_lblScrollDown != null) { _lblScrollDown.Location = new Point(rightX, checkY); checkY += Theme.S(35); }
            if (_lblDrag != null) { _lblDrag.Location = new Point(rightX, checkY); checkY += Theme.S(35); }

            if (_lblCounter != null)
                _lblCounter.Location = new Point(rightX, checkY + Theme.S(15));

            if (_btnClear != null)
                _btnClear.Location = new Point(rightX, checkY + Theme.S(50));

            // Legend at the bottom
            if (_lblLegend != null)
                _lblLegend.Location = new Point(Theme.S(30), clientH - Theme.S(60));
        }

        private void BuildUI()
        {
            _lblTitle = Theme.CreateLabel("Teste de Touchpad", 30, 15, Theme.FontHeader);
            Controls.Add(_lblTitle);

            _lblDesc = Theme.CreateLabel(
                "Use o touchpad para realizar cada ação. O canvas registra seus movimentos.",
                30, 48, Theme.FontSmall, Theme.TextSecondary);
            Controls.Add(_lblDesc);

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
            Controls.Add(_canvas);

            // Canvas border
            _canvas.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, _canvas.Width - 1, _canvas.Height - 1);
            };

            // Checklist panel
            _lblChecklistTitle = Theme.CreateLabel("Checklist", 640, 85, Theme.FontButton);
            Controls.Add(_lblChecklistTitle);

            int checkY = Theme.S(120);
            _lblMoved = CreateCheckLabel("Movimento detectado", 640, checkY); checkY += Theme.S(35);
            _lblLeftClick = CreateCheckLabel("Clique esquerdo", 640, checkY); checkY += Theme.S(35);
            _lblRightClick = CreateCheckLabel("Clique direito", 640, checkY); checkY += Theme.S(35);
            _lblScrollUp = CreateCheckLabel("Scroll para cima", 640, checkY); checkY += Theme.S(35);
            _lblScrollDown = CreateCheckLabel("Scroll para baixo", 640, checkY); checkY += Theme.S(35);
            _lblDrag = CreateCheckLabel("Arrastar (drag)", 640, checkY); checkY += Theme.S(35);

            _lblCounter = Theme.CreateLabel("0 / 6 testes", 640, checkY + Theme.S(15), Theme.FontBody, Theme.TextSecondary);
            Controls.Add(_lblCounter);

            // Buttons
            _btnClear = Theme.CreateSecondaryButton("🔄 Limpar", 640, checkY + Theme.S(50), 120, 36);
            _btnClear.Click += (s, e) => ClearAll();
            Controls.Add(_btnClear);

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

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (_canvasBitmap != null)
                e.Graphics.DrawImage(_canvasBitmap, 0, 0);
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            CompleteCheck("move", _lblMoved);

            if (_isDragging && _lastPoint != Point.Empty)
            {
                using (var pen = new Pen(Theme.TextPrimary, 2f))
                    _canvasGraphics.DrawLine(pen, _lastPoint, e.Location);
                _canvas.Invalidate();
                CompleteCheck("drag", _lblDrag);
            }
            else if (e.Button == MouseButtons.None)
            {
                // Draw faint trail for movement
                using (var brush = new SolidBrush(Color.FromArgb(30, Theme.Accent)))
                    _canvasGraphics.FillEllipse(brush, e.X - 2, e.Y - 2, 4, 4);
                _canvas.Invalidate();
            }

            _lastPoint = e.Location;
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
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
            _canvas.Invalidate();
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
            _lastPoint = Point.Empty;
        }

        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                CompleteCheck("scrollup", _lblScrollUp);
                // Draw up arrow indicator
                using (var brush = new SolidBrush(Theme.Warning))
                    _canvasGraphics.FillPolygon(brush, new Point[]
                    {
                        new Point(e.X, e.Y - 10),
                        new Point(e.X - 6, e.Y + 4),
                        new Point(e.X + 6, e.Y + 4)
                    });
            }
            else
            {
                CompleteCheck("scrolldown", _lblScrollDown);
                using (var brush = new SolidBrush(Theme.Warning))
                    _canvasGraphics.FillPolygon(brush, new Point[]
                    {
                        new Point(e.X, e.Y + 10),
                        new Point(e.X - 6, e.Y - 4),
                        new Point(e.X + 6, e.Y - 4)
                    });
            }
            _canvas.Invalidate();
        }

        private void ClearAll()
        {
            _canvasGraphics.Clear(Theme.BgDark);
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
            _canvasGraphics?.Dispose();
            _canvasBitmap?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
