using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TechTest.Helpers;

namespace TechTest.Controls
{
    public class NotebookKeyboardControl : UserControl
    {
        public event EventHandler<int> ProgressChanged;

        private struct KeyDef
        {
            public string Label;
            public float WidthFactor;
            public Keys KeyCode;
            public RectangleF Bounds;
        }

        private List<List<KeyDef>> _rows = new List<List<KeyDef>>();
        private List<List<KeyDef>> _numpadRows = new List<List<KeyDef>>();
        private HashSet<Keys> _pressedKeys = new HashSet<Keys>();
        private int _totalKeys;
        private float _keySize = Theme.S(40f);
        private float _gap = Theme.S(3f);
        private bool _hasNumpad = false;
        private float _mainWidth;

        public int TotalKeys => _totalKeys;
        public int PressedCount => _pressedKeys.Count;

        public bool HasNumpad
        {
            get => _hasNumpad;
            set
            {
                if (_hasNumpad != value)
                {
                    _hasNumpad = value;
                    BuildLayout();
                    ProgressChanged?.Invoke(this, _pressedKeys.Count);
                    Invalidate();
                }
            }
        }

        public NotebookKeyboardControl()
        {
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.Transparent;
            BuildLayout();
        }

        private void BuildLayout()
        {
            _rows.Clear();
            float ks = _keySize;

            // Row 0: Esc, F1-F12, PrtSc, Del
            _rows.Add(new List<KeyDef>
            {
                K("Esc", 1f, Keys.Escape), KS(0.3f),
                K("F1", 1f, Keys.F1), K("F2", 1f, Keys.F2), K("F3", 1f, Keys.F3), K("F4", 1f, Keys.F4), KS(0.3f),
                K("F5", 1f, Keys.F5), K("F6", 1f, Keys.F6), K("F7", 1f, Keys.F7), K("F8", 1f, Keys.F8), KS(0.3f),
                K("F9", 1f, Keys.F9), K("F10", 1f, Keys.F10), K("F11", 1f, Keys.F11), K("F12", 1f, Keys.F12), KS(0.3f),
                K("PrtSc", 1f, Keys.PrintScreen), K("Del", 1f, Keys.Delete),
            });

            // Row 1: ` 1-0 - = Backspace
            _rows.Add(new List<KeyDef>
            {
                K("'", 1f, Keys.OemQuotes), K("1", 1f, Keys.D1), K("2", 1f, Keys.D2), K("3", 1f, Keys.D3),
                K("4", 1f, Keys.D4), K("5", 1f, Keys.D5), K("6", 1f, Keys.D6), K("7", 1f, Keys.D7),
                K("8", 1f, Keys.D8), K("9", 1f, Keys.D9), K("0", 1f, Keys.D0),
                K("-", 1f, Keys.OemMinus), K("=", 1f, Keys.Oemplus),
                K("⌫", 2f, Keys.Back),
            });

            // Row 2: Tab Q-P [ ] 
            _rows.Add(new List<KeyDef>
            {
                K("Tab", 1.5f, Keys.Tab),
                K("Q", 1f, Keys.Q), K("W", 1f, Keys.W), K("E", 1f, Keys.E), K("R", 1f, Keys.R),
                K("T", 1f, Keys.T), K("Y", 1f, Keys.Y), K("U", 1f, Keys.U), K("I", 1f, Keys.I),
                K("O", 1f, Keys.O), K("P", 1f, Keys.P),
                K("[", 1f, Keys.OemOpenBrackets), K("]", 1f, Keys.OemCloseBrackets),
                K("Enter", 1.5f, Keys.Enter),
            });

            // Row 3: CapsLock A-L ; ' \ 
            _rows.Add(new List<KeyDef>
            {
                K("Caps", 1.8f, Keys.CapsLock),
                K("A", 1f, Keys.A), K("S", 1f, Keys.S), K("D", 1f, Keys.D), K("F", 1f, Keys.F),
                K("G", 1f, Keys.G), K("H", 1f, Keys.H), K("J", 1f, Keys.J), K("K", 1f, Keys.K),
                K("L", 1f, Keys.L), K("Ç", 1f, Keys.Oem1),
                K("~", 1f, Keys.Oem7), K("\\", 1f, Keys.Oem5),
            });

            // Row 4: LShift \ Z-M , . / RShift
            _rows.Add(new List<KeyDef>
            {
                K("Shift", 1.3f, Keys.LShiftKey),
                K("|", 1f, Keys.OemBackslash),
                K("Z", 1f, Keys.Z), K("X", 1f, Keys.X), K("C", 1f, Keys.C), K("V", 1f, Keys.V),
                K("B", 1f, Keys.B), K("N", 1f, Keys.N), K("M", 1f, Keys.M),
                K(",", 1f, Keys.Oemcomma), K(".", 1f, Keys.OemPeriod), K("/", 1f, Keys.OemQuestion),
                K("Shift", 2.7f, Keys.RShiftKey),
            });

            // Row 5: Ctrl Fn Win Alt Space AltGr Ctrl ← ↑↓ →
            _rows.Add(new List<KeyDef>
            {
                K("Ctrl", 1.3f, Keys.LControlKey),
                K("Fn", 1f, Keys.None),
                K("Win", 1.1f, Keys.LWin),
                K("Alt", 1.2f, Keys.LMenu),
                K("Espaço", 5.2f, Keys.Space),
                K("AltGr", 1.2f, Keys.RMenu),
                K("Ctrl", 1.3f, Keys.RControlKey), KS(0.3f),
                K("←", 1f, Keys.Left), K("↑↓", 1f, Keys.Up), K("→", 1f, Keys.Right),
            });

            // Calculate bounds and count total keys
            _totalKeys = 0;
            float offsetY = 5f;
            float maxRowWidth = 0f;
            foreach (var row in _rows)
            {
                float offsetX = 5f;
                for (int i = 0; i < row.Count; i++)
                {
                    var key = row[i];
                    float w = key.WidthFactor * ks;
                    if (key.KeyCode == Keys.None && key.Label == null)
                    {
                        // Spacer
                        offsetX += w + _gap;
                        continue;
                    }
                    key.Bounds = new RectangleF(offsetX, offsetY, w, ks);
                    row[i] = key;
                    offsetX += w + _gap;
                    if (key.KeyCode != Keys.None) _totalKeys++;
                }
                if (offsetX > maxRowWidth) maxRowWidth = offsetX;
                offsetY += ks + _gap;
            }

            _mainWidth = maxRowWidth;

            // Build numpad if enabled
            _numpadRows.Clear();
            float numpadWidth = 0f;
            if (_hasNumpad)
            {
                // Numpad rows (aligned to main keyboard rows 1-5)
                // Row 0 (aligns with F-key row): NumLk  /  *  -
                _numpadRows.Add(new List<KeyDef>
                {
                    K("Num", 1f, Keys.NumLock), K("/", 1f, Keys.Divide),
                    K("*", 1f, Keys.Multiply), K("-", 1f, Keys.Subtract),
                });

                // Row 1: 7  8  9  + (+ spans 2 rows, handled as normal key here)
                _numpadRows.Add(new List<KeyDef>
                {
                    K("7", 1f, Keys.NumPad7), K("8", 1f, Keys.NumPad8),
                    K("9", 1f, Keys.NumPad9), K("+", 1f, Keys.Add),
                });

                // Row 2: 4  5  6  (empty for + continuation, use spacer)
                _numpadRows.Add(new List<KeyDef>
                {
                    K("4", 1f, Keys.NumPad4), K("5", 1f, Keys.NumPad5),
                    K("6", 1f, Keys.NumPad6), KS(1f),
                });

                // Row 3: 1  2  3  Enter
                _numpadRows.Add(new List<KeyDef>
                {
                    K("1", 1f, Keys.NumPad1), K("2", 1f, Keys.NumPad2),
                    K("3", 1f, Keys.NumPad3), K("Ent", 1f, Keys.Return),
                });

                // Row 4: 0 (wide)  .  (spacer)
                _numpadRows.Add(new List<KeyDef>
                {
                    K("0", 2f, Keys.NumPad0), K(".", 1f, Keys.Decimal), KS(1f),
                });

                // Calculate numpad bounds
                float numpadStartX = _mainWidth + Theme.S(20f); // gap between main keyboard and numpad
                float numOffsetY = Theme.S(5f); // start at same Y as main keyboard
                // Skip first main row (F-keys) - numpad starts at row 1 level
                numOffsetY += ks + _gap;

                for (int r = 0; r < _numpadRows.Count; r++)
                {
                    var row = _numpadRows[r];
                    float offsetX = numpadStartX;
                    for (int i = 0; i < row.Count; i++)
                    {
                        var key = row[i];
                        float w = key.WidthFactor * ks;
                        if (key.KeyCode == Keys.None && key.Label == null)
                        {
                            offsetX += w + _gap;
                            continue;
                        }
                        key.Bounds = new RectangleF(offsetX, numOffsetY, w, ks);
                        row[i] = key;
                        offsetX += w + _gap;
                        if (key.KeyCode != Keys.None) _totalKeys++;
                    }
                    if (offsetX > numpadWidth) numpadWidth = offsetX;
                    numOffsetY += ks + _gap;
                }
            }

            float totalWidth = _hasNumpad ? numpadWidth + Theme.S(10f) : Theme.S(680f);
            this.Size = new Size((int)totalWidth, (int)offsetY + Theme.S(10));
        }

        private KeyDef K(string label, float width, Keys key)
        {
            return new KeyDef { Label = label, WidthFactor = width, KeyCode = key };
        }

        private KeyDef KS(float width) // Spacer
        {
            return new KeyDef { Label = null, WidthFactor = width, KeyCode = Keys.None };
        }

        public void MarkKeyPressed(Keys key)
        {
            // Normalize shift keys etc.
            if (key == Keys.ShiftKey) { _pressedKeys.Add(Keys.LShiftKey); _pressedKeys.Add(Keys.RShiftKey); }
            else if (key == Keys.ControlKey) { _pressedKeys.Add(Keys.LControlKey); _pressedKeys.Add(Keys.RControlKey); }
            else if (key == Keys.Menu) { _pressedKeys.Add(Keys.LMenu); _pressedKeys.Add(Keys.RMenu); }
            else _pressedKeys.Add(key);

            // Handle Up key also marks Down
            if (key == Keys.Up) _pressedKeys.Add(Keys.Down);
            if (key == Keys.Down) _pressedKeys.Add(Keys.Up);

            ProgressChanged?.Invoke(this, _pressedKeys.Count);
            Invalidate();
        }

        public void ResetKeys()
        {
            _pressedKeys.Clear();
            ProgressChanged?.Invoke(this, 0);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            foreach (var row in _rows)
            {
                foreach (var key in row)
                {
                    if (key.Label == null) continue;
                    DrawKey(g, key);
                }
            }

            if (_hasNumpad && _numpadRows.Count > 0)
            {
                // Draw separator line
                float sepX = _mainWidth + Theme.S(8f);
                using (var pen = new Pen(Theme.Border, 1f) { DashStyle = DashStyle.Dot })
                    g.DrawLine(pen, sepX, Theme.S(10f), sepX, this.Height - Theme.S(15f));

                // Draw numpad label
                using (var font = new Font("Segoe UI", Theme.S(8f), FontStyle.Bold))
                {
                    float labelX = _mainWidth + Theme.S(20f);
                    using (var brush = new SolidBrush(Theme.TextMuted))
                        g.DrawString("NUMPAD", font, brush, labelX, _keySize + _gap - 14f);
                }

                // Draw numpad keys
                foreach (var row in _numpadRows)
                {
                    foreach (var key in row)
                    {
                        if (key.Label == null) continue;
                        DrawKey(g, key);
                    }
                }
            }
        }

        private void DrawKey(Graphics g, KeyDef key)
        {
            var rect = Rectangle.Round(key.Bounds);
            bool isPressed = key.KeyCode != Keys.None && _pressedKeys.Contains(key.KeyCode);
            bool isFnKey = key.KeyCode == Keys.None;

            Color bgColor = isPressed ? Theme.Success : (isFnKey ? Color.FromArgb(35, 35, 50) : Theme.BgCard);
            Color borderColor = isPressed ? Color.FromArgb(56, 220, 120) : Theme.Border;
            Color textColor = isPressed ? Color.White : Theme.TextSecondary;

            using (var path = Theme.RoundedRect(rect, 5))
            {
                using (var brush = new SolidBrush(bgColor))
                    g.FillPath(brush, path);
                using (var pen = new Pen(borderColor, isPressed ? 1.5f : 0.8f))
                    g.DrawPath(pen, path);
            }

            // Draw label
            using (var font = new Font("Segoe UI", Theme.S(key.WidthFactor > 1.5f ? 8f : 9f), FontStyle.Regular))
            {
                var sz = g.MeasureString(key.Label, font);
                float tx = key.Bounds.X + (key.Bounds.Width - sz.Width) / 2;
                float ty = key.Bounds.Y + (key.Bounds.Height - sz.Height) / 2;
                g.DrawString(key.Label, font, new SolidBrush(textColor), tx, ty);
            }
        }
    }
}
