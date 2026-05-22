using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TechTest.Helpers;

namespace TechTest.Controls
{
    public enum KeyTestState
    {
        Untested,
        PressedOnce,
        PressedMultiple,
        HeldDown
    }

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
        private Dictionary<Keys, KeyTestState> _keyStates = new Dictionary<Keys, KeyTestState>();
        private HashSet<Keys> _currentlyHeldKeys = new HashSet<Keys>();
        private Dictionary<Keys, DateTime> _keyPressStartTimes = new Dictionary<Keys, DateTime>();
        private int _totalKeys;
        private float _keySize = Theme.S(40f);
        private float _gap = Theme.S(3f);
        private bool _hasNumpad = false;
        private float _mainWidth;

        // Down arrow rendered separately (stacked below Up arrow)
        private KeyDef _downArrow;

        // Pulse animation for HeldDown state
        private System.Windows.Forms.Timer _pulseTimer;
        private float _pulsePhase = 0f;

        public int TotalKeys => _totalKeys;
        public int PressedCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _keyStates)
                {
                    if (kvp.Value != KeyTestState.Untested)
                        count++;
                }
                return count;
            }
        }

        public bool HasNumpad
        {
            get => _hasNumpad;
            set
            {
                if (_hasNumpad != value)
                {
                    _hasNumpad = value;
                    BuildLayout();
                    ProgressChanged?.Invoke(this, PressedCount);
                    Invalidate();
                }
            }
        }

        public NotebookKeyboardControl()
        {
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.Transparent;

            _pulseTimer = new System.Windows.Forms.Timer();
            _pulseTimer.Interval = 30;
            _pulseTimer.Tick += PulseTimer_Tick;

            BuildLayout();
        }

        private void PulseTimer_Tick(object sender, EventArgs e)
        {
            _pulsePhase += 0.15f;
            if (_pulsePhase > (float)(2 * Math.PI))
                _pulsePhase -= (float)(2 * Math.PI);
            Invalidate();
        }

        private void UpdatePulseTimer()
        {
            if (_currentlyHeldKeys.Count > 0)
            {
                if (!_pulseTimer.Enabled)
                    _pulseTimer.Start();
            }
            else
            {
                if (_pulseTimer.Enabled)
                    _pulseTimer.Stop();
            }
        }

        private void BuildLayout()
        {
            _rows.Clear();
            float ks = _keySize;

            if (_hasNumpad)
            {
                // Row 0: Esc, F1-F12, PrtSc, Del (aligned to 15.6)
                _rows.Add(new List<KeyDef>
                {
                    K("Esc", 1f, Keys.Escape), KS(0.2f),
                    K("F1", 1f, Keys.F1), K("F2", 1f, Keys.F2), K("F3", 1f, Keys.F3), K("F4", 1f, Keys.F4), KS(0.2f),
                    K("F5", 1f, Keys.F5), K("F6", 1f, Keys.F6), K("F7", 1f, Keys.F7), K("F8", 1f, Keys.F8), KS(0.2f),
                    K("F9", 1f, Keys.F9), K("F10", 1f, Keys.F10), K("F11", 1f, Keys.F11), K("F12", 1f, Keys.F12),
                    K("PrtSc", 1f, Keys.PrintScreen), K("Del", 1f, Keys.Delete),
                });

                // Row 1: ` 1-0 - = Backspace, Spacer (aligned to 15.6)
                _rows.Add(new List<KeyDef>
                {
                    K("'", 1f, Keys.Oem8), K("1", 1f, Keys.D1), K("2", 1f, Keys.D2), K("3", 1f, Keys.D3),
                    K("4", 1f, Keys.D4), K("5", 1f, Keys.D5), K("6", 1f, Keys.D6), K("7", 1f, Keys.D7),
                    K("8", 1f, Keys.D8), K("9", 1f, Keys.D9), K("0", 1f, Keys.D0),
                    K("-", 1f, Keys.OemMinus), K("=", 1f, Keys.Oemplus),
                    K("⌫", 2f, Keys.Back), KS(0.6f),
                });

                // Row 2: Tab Q-P [ ] Enter, Spacer (aligned to 15.6)
                _rows.Add(new List<KeyDef>
                {
                    K("Tab", 1.5f, Keys.Tab),
                    K("Q", 1f, Keys.Q), K("W", 1f, Keys.W), K("E", 1f, Keys.E), K("R", 1f, Keys.R),
                    K("T", 1f, Keys.T), K("Y", 1f, Keys.Y), K("U", 1f, Keys.U), K("I", 1f, Keys.I),
                    K("O", 1f, Keys.O), K("P", 1f, Keys.P),
                    K("[", 1f, Keys.OemOpenBrackets), K("]", 1f, Keys.OemCloseBrackets),
                    K("Enter", 1.5f, Keys.Enter), KS(0.6f),
                });

                // Row 3: CapsLock A-L ; ' backslash, Spacer (aligned to 15.6)
                _rows.Add(new List<KeyDef>
                {
                    K("Caps", 1.8f, Keys.CapsLock),
                    K("A", 1f, Keys.A), K("S", 1f, Keys.S), K("D", 1f, Keys.D), K("F", 1f, Keys.F),
                    K("G", 1f, Keys.G), K("H", 1f, Keys.H), K("J", 1f, Keys.J), K("K", 1f, Keys.K),
                    K("L", 1f, Keys.L), K("Ç", 1f, Keys.Oem1),
                    K("~", 1f, Keys.Oem7), K("\\", 1f, Keys.Oem5), KS(1.8f),
                });

                // Row 4: LShift | Z-M , . / RShift, Spacer (aligned to 15.6)
                _rows.Add(new List<KeyDef>
                {
                    K("Shift", 1.3f, Keys.LShiftKey),
                    K("|", 1f, Keys.OemBackslash),
                    K("Z", 1f, Keys.Z), K("X", 1f, Keys.X), K("C", 1f, Keys.C), K("V", 1f, Keys.V),
                    K("B", 1f, Keys.B), K("N", 1f, Keys.N), K("M", 1f, Keys.M),
                    K(",", 1f, Keys.Oemcomma), K(".", 1f, Keys.OemPeriod), K("/", 1f, Keys.OemQuestion),
                    K("Shift", 2.7f, Keys.RShiftKey), KS(0.6f),
                });

                // Row 5: Ctrl Fn Win Alt Space AltGr Ctrl  ← ↑ → (no spacer, ends at 15.6)
                _rows.Add(new List<KeyDef>
                {
                    K("Ctrl", 1.3f, Keys.LControlKey),
                    K("Fn", 1f, Keys.None),
                    K("Win", 1.1f, Keys.LWin),
                    K("Alt", 1.2f, Keys.LMenu),
                    K("Espaço", 5.2f, Keys.Space),
                    K("AltGr", 1.2f, Keys.RMenu),
                    K("Ctrl", 1.3f, Keys.RControlKey), KS(0.3f),
                    K("←", 1f, Keys.Left), K("↑", 1f, Keys.Up), K("→", 1f, Keys.Right),
                });
            }
            else
            {
                // Row 0: Esc, F1-F12, PrtSc, Ins, Del (aligned to 17.2)
                _rows.Add(new List<KeyDef>
                {
                    K("Esc", 1f, Keys.Escape), KS(0.3f),
                    K("F1", 1f, Keys.F1), K("F2", 1f, Keys.F2), K("F3", 1f, Keys.F3), K("F4", 1f, Keys.F4), KS(0.3f),
                    K("F5", 1f, Keys.F5), K("F6", 1f, Keys.F6), K("F7", 1f, Keys.F7), K("F8", 1f, Keys.F8), KS(0.3f),
                    K("F9", 1f, Keys.F9), K("F10", 1f, Keys.F10), K("F11", 1f, Keys.F11), K("F12", 1f, Keys.F12), KS(0.3f),
                    K("PrtSc", 1f, Keys.PrintScreen), K("Ins", 1f, Keys.Insert), K("Del", 1f, Keys.Delete),
                });

                // Row 1: ` 1-0 - = Backspace, Home
                _rows.Add(new List<KeyDef>
                {
                    K("'", 1f, Keys.Oem8), K("1", 1f, Keys.D1), K("2", 1f, Keys.D2), K("3", 1f, Keys.D3),
                    K("4", 1f, Keys.D4), K("5", 1f, Keys.D5), K("6", 1f, Keys.D6), K("7", 1f, Keys.D7),
                    K("8", 1f, Keys.D8), K("9", 1f, Keys.D9), K("0", 1f, Keys.D0),
                    K("-", 1f, Keys.OemMinus), K("=", 1f, Keys.Oemplus),
                    K("⌫", 2f, Keys.Back), KS(1.2f),
                    K("Home", 1f, Keys.Home),
                });

                // Row 2: Tab Q-P [ ] Enter, PgUp
                _rows.Add(new List<KeyDef>
                {
                    K("Tab", 1.5f, Keys.Tab),
                    K("Q", 1f, Keys.Q), K("W", 1f, Keys.W), K("E", 1f, Keys.E), K("R", 1f, Keys.R),
                    K("T", 1f, Keys.T), K("Y", 1f, Keys.Y), K("U", 1f, Keys.U), K("I", 1f, Keys.I),
                    K("O", 1f, Keys.O), K("P", 1f, Keys.P),
                    K("[", 1f, Keys.OemOpenBrackets), K("]", 1f, Keys.OemCloseBrackets),
                    K("Enter", 1.5f, Keys.Enter), KS(1.2f),
                    K("PgUp", 1f, Keys.PageUp),
                });

                // Row 3: CapsLock A-L ; ' backslash, PgDn
                _rows.Add(new List<KeyDef>
                {
                    K("Caps", 1.8f, Keys.CapsLock),
                    K("A", 1f, Keys.A), K("S", 1f, Keys.S), K("D", 1f, Keys.D), K("F", 1f, Keys.F),
                    K("G", 1f, Keys.G), K("H", 1f, Keys.H), K("J", 1f, Keys.J), K("K", 1f, Keys.K),
                    K("L", 1f, Keys.L), K("Ç", 1f, Keys.Oem1),
                    K("~", 1f, Keys.Oem7), K("\\", 1f, Keys.Oem5), KS(2.4f),
                    K("PgDn", 1f, Keys.PageDown),
                });

                // Row 4: LShift | Z-M , . / RShift, End
                _rows.Add(new List<KeyDef>
                {
                    K("Shift", 1.3f, Keys.LShiftKey),
                    K("|", 1f, Keys.OemBackslash),
                    K("Z", 1f, Keys.Z), K("X", 1f, Keys.X), K("C", 1f, Keys.C), K("V", 1f, Keys.V),
                    K("B", 1f, Keys.B), K("N", 1f, Keys.N), K("M", 1f, Keys.M),
                    K(",", 1f, Keys.Oemcomma), K(".", 1f, Keys.OemPeriod), K("/", 1f, Keys.OemQuestion),
                    K("Shift", 2.7f, Keys.RShiftKey), KS(1.2f),
                    K("End", 1f, Keys.End),
                });

                // Row 5: Ctrl Fn Win Alt Space AltGr Ctrl  ← ↑ →, Spacer (aligned to 17.2)
                _rows.Add(new List<KeyDef>
                {
                    K("Ctrl", 1.3f, Keys.LControlKey),
                    K("Fn", 1f, Keys.None),
                    K("Win", 1.1f, Keys.LWin),
                    K("Alt", 1.2f, Keys.LMenu),
                    K("Espaço", 5.2f, Keys.Space),
                    K("AltGr", 1.2f, Keys.RMenu),
                    K("Ctrl", 1.3f, Keys.RControlKey), KS(0.3f),
                    K("←", 1f, Keys.Left), K("↑", 1f, Keys.Up), K("→", 1f, Keys.Right), KS(1.6f),
                });
            }

            // Count total keys
            _totalKeys = 0;
            for (int r = 0; r < _rows.Count; r++)
            {
                var row = _rows[r];
                for (int i = 0; i < row.Count; i++)
                {
                    var key = row[i];
                    if (key.KeyCode != Keys.None && !(r == 5 && key.KeyCode == Keys.Up))
                    {
                        _totalKeys++;
                    }
                }
            }

            // Up and Down keys on arrow stacked
            _totalKeys += 2; // Up and Down

            // Build numpad if enabled
            _numpadRows.Clear();
            if (_hasNumpad)
            {
                _numpadRows.Add(new List<KeyDef>
                {
                    K("Num", 1f, Keys.NumLock), K("/", 1f, Keys.Divide),
                    K("*", 1f, Keys.Multiply), K("-", 1f, Keys.Subtract),
                });

                _numpadRows.Add(new List<KeyDef>
                {
                    K("7", 1f, Keys.NumPad7), K("8", 1f, Keys.NumPad8),
                    K("9", 1f, Keys.NumPad9), K("+", 1f, Keys.Add),
                });

                _numpadRows.Add(new List<KeyDef>
                {
                    K("4", 1f, Keys.NumPad4), K("5", 1f, Keys.NumPad5),
                    K("6", 1f, Keys.NumPad6), KS(1f),
                });

                _numpadRows.Add(new List<KeyDef>
                {
                    K("1", 1f, Keys.NumPad1), K("2", 1f, Keys.NumPad2),
                    K("3", 1f, Keys.NumPad3), K("Ent", 1f, Keys.Return),
                });

                _numpadRows.Add(new List<KeyDef>
                {
                    K("0", 2f, Keys.NumPad0), K(".", 1f, Keys.Decimal), KS(1f),
                });

                foreach (var row in _numpadRows)
                {
                    foreach (var key in row)
                    {
                        if (key.KeyCode != Keys.None) _totalKeys++;
                    }
                }
            }

            RecalculateBounds();
        }

        private void RecalculateBounds()
        {
            if (_rows.Count == 0) return;

            // Total logical units X and Y for sizing
            float totalUnitsX = _hasNumpad ? 21.6f : 18.7f;
            float totalUnitsY = 6.45f;

            float paddingX = Theme.S(10f);
            float paddingY = Theme.S(10f);

            float availW = this.Width - paddingX * 2;
            float availH = this.Height - paddingY * 2;

            if (availW < 10) availW = 10;
            if (availH < 10) availH = 10;

            float ksX = availW / totalUnitsX;
            float ksY = availH / totalUnitsY;

            // Choose the smaller one to maintain square aspect ratio
            float ks = Math.Min(ksX, ksY);
            _keySize = ks;
            _gap = ks * 0.075f;

            // Actual dimensions of the keyboard content
            float kbW = totalUnitsX * ks;
            float kbH = totalUnitsY * ks;

            // Center offsets within control
            float startX = paddingX + (availW - kbW) / 2f;
            float startY = paddingY + (availH - kbH) / 2f;

            float offsetY = startY;
            float maxRowWidth = 0f;

            for (int rowIdx = 0; rowIdx < _rows.Count; rowIdx++)
            {
                var row = _rows[rowIdx];
                float offsetX = startX;
                for (int i = 0; i < row.Count; i++)
                {
                    var key = row[i];
                    float w = key.WidthFactor * ks;
                    if (key.KeyCode == Keys.None && key.Label == null)
                    {
                        offsetX += w + _gap;
                        continue;
                    }

                    // Up arrow gets half height, Down arrow stacked below it
                    if (rowIdx == 5 && key.KeyCode == Keys.Up)
                    {
                        float halfH = ks / 2f;
                        key.Bounds = new RectangleF(offsetX, offsetY, w, halfH);
                        row[i] = key;

                        _downArrow = new KeyDef
                        {
                            Label = "↓",
                            WidthFactor = 1f,
                            KeyCode = Keys.Down,
                            Bounds = new RectangleF(offsetX, offsetY + halfH + _gap * 0.5f, w, halfH)
                        };

                        offsetX += w + _gap;
                        continue;
                    }

                    key.Bounds = new RectangleF(offsetX, offsetY, w, ks);
                    row[i] = key;
                    offsetX += w + _gap;
                }
                if (offsetX > maxRowWidth) maxRowWidth = offsetX;
                offsetY += ks + _gap;
            }

            _mainWidth = maxRowWidth;

            // Compute numpad positions relative to the keyboard grid
            if (_hasNumpad && _numpadRows.Count > 0)
            {
                float numpadStartX = _mainWidth + 0.35f * ks; // 0.35 gap after main keyboard
                float numOffsetY = startY + ks + _gap;

                for (int r = 0; r < _numpadRows.Count; r++)
                {
                    var row = _numpadRows[r];
                    float npOffsetX = numpadStartX;
                    for (int i = 0; i < row.Count; i++)
                    {
                        var key = row[i];
                        float w = key.WidthFactor * ks;
                        if (key.KeyCode == Keys.None && key.Label == null)
                        {
                            npOffsetX += w + _gap;
                            continue;
                        }
                        key.Bounds = new RectangleF(npOffsetX, numOffsetY, w, ks);
                        row[i] = key;
                        npOffsetX += w + _gap;
                    }
                    numOffsetY += ks + _gap;
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateBounds();
            Invalidate();
        }

        private KeyDef K(string label, float width, Keys key)
        {
            return new KeyDef { Label = label, WidthFactor = width, KeyCode = key };
        }

        private KeyDef KS(float width) // Spacer
        {
            return new KeyDef { Label = null, WidthFactor = width, KeyCode = Keys.None };
        }

        private KeyTestState GetKeyState(Keys key)
        {
            if (key == Keys.None) return KeyTestState.Untested;

            // Map synonyms for ABNT2 vs US layouts on the first key of row 1 (apostrophe/tilde)
            if (key == Keys.Oem8)
            {
                var state8 = _keyStates.TryGetValue(Keys.Oem8, out var s8) ? s8 : KeyTestState.Untested;
                var stateTilde = _keyStates.TryGetValue(Keys.Oemtilde, out var st) ? st : KeyTestState.Untested;
                return (state8 > stateTilde) ? state8 : stateTilde;
            }

            if (_keyStates.TryGetValue(key, out var state))
                return state;
            return KeyTestState.Untested;
        }

        /// <summary>
        /// Called when a key is physically pressed down.
        /// Sets state to HeldDown and tracks it in _currentlyHeldKeys.
        /// </summary>
        public void MarkKeyDown(Keys key)
        {
            if (key == Keys.None) return;

            if (!_currentlyHeldKeys.Contains(key))
            {
                _currentlyHeldKeys.Add(key);
                _keyPressStartTimes[key] = DateTime.Now;
            }
            _keyStates[key] = KeyTestState.HeldDown;

            UpdatePulseTimer();
            ProgressChanged?.Invoke(this, PressedCount);
            Invalidate();
        }

        /// <summary>
        /// Called when a key is released.
        /// Transitions from HeldDown to PressedOnce or PressedMultiple based on duration.
        /// </summary>
        public void MarkKeyUp(Keys key)
        {
            if (key == Keys.None) return;

            _currentlyHeldKeys.Remove(key);

            bool heldForTwoSeconds = false;
            if (_keyPressStartTimes.TryGetValue(key, out var startTime))
            {
                heldForTwoSeconds = (DateTime.Now - startTime).TotalMilliseconds >= 2000;
                _keyPressStartTimes.Remove(key);
            }

            if (_keyStates.TryGetValue(key, out var currentState))
            {
                if (heldForTwoSeconds)
                {
                    _keyStates[key] = KeyTestState.PressedMultiple;
                }
                else
                {
                    // If it was already PressedMultiple (blue border) from a prior long press, keep it.
                    // Otherwise, it becomes PressedOnce (green).
                    if (currentState != KeyTestState.PressedMultiple)
                    {
                        _keyStates[key] = KeyTestState.PressedOnce;
                    }
                }
            }
            else
            {
                _keyStates[key] = heldForTwoSeconds ? KeyTestState.PressedMultiple : KeyTestState.PressedOnce;
            }

            UpdatePulseTimer();
            ProgressChanged?.Invoke(this, PressedCount);
            Invalidate();
        }

        /// <summary>
        /// Legacy compatibility method. Transitions:
        /// Untested → PressedOnce, PressedOnce → PressedMultiple, PressedMultiple stays.
        /// </summary>
        public void MarkKeyPressed(Keys key)
        {
            if (key == Keys.None) return;

            if (!_keyStates.TryGetValue(key, out var currentState) || currentState == KeyTestState.Untested)
            {
                _keyStates[key] = KeyTestState.PressedOnce;
            }

            ProgressChanged?.Invoke(this, PressedCount);
            Invalidate();
        }

        public void ResetKeys()
        {
            _keyStates.Clear();
            _currentlyHeldKeys.Clear();
            _keyPressStartTimes.Clear();
            UpdatePulseTimer();
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

            // Draw the down arrow key separately
            DrawKey(g, _downArrow);

            if (_hasNumpad && _numpadRows.Count > 0)
            {
                // Draw separator line relative to main width and key size
                float sepX = _mainWidth + _keySize * 0.175f;
                using (var pen = new Pen(Theme.Border, 1f) { DashStyle = DashStyle.Dot })
                    g.DrawLine(pen, sepX, Theme.S(10f), sepX, this.Height - Theme.S(15f));

                // Draw numpad label relative to main width and key size
                using (var font = new Font("Segoe UI", Theme.S(8f), FontStyle.Bold))
                {
                    float labelX = _mainWidth + _keySize * 0.35f;
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
            bool isFnKey = key.KeyCode == Keys.None;

            string label;
            Keys activeKey;
            GetDynamicKeyDetails(key, out label, out activeKey);

            KeyTestState state = isFnKey ? KeyTestState.Untested : GetKeyState(activeKey);

            Color bgColor;
            Color borderColor;
            Color textColor;
            float borderWidth;

            if (isFnKey)
            {
                // Fn key: hardware key, can't be detected
                bgColor = Color.FromArgb(35, 35, 50);
                borderColor = Theme.Border;
                borderWidth = 0.8f;
                textColor = Theme.TextSecondary;
            }
            else
            {
                switch (state)
                {
                    case KeyTestState.PressedOnce:
                        bgColor = Theme.Success;
                        borderColor = Color.FromArgb(56, 220, 120);
                        borderWidth = 1.5f;
                        textColor = Color.White;
                        break;

                    case KeyTestState.PressedMultiple:
                        bgColor = Theme.Success;
                        borderColor = Color.FromArgb(59, 130, 246);
                        borderWidth = 3f;
                        textColor = Color.White;
                        break;

                    case KeyTestState.HeldDown:
                        // Pulse effect: modulate blue background brightness with sine wave
                        float pulse = (float)Math.Sin(_pulsePhase);
                        int blueBase = 59;
                        int blueVar = (int)(20 * pulse);
                        int r = Math.Max(0, Math.Min(255, blueBase + blueVar));
                        int gv = Math.Max(0, Math.Min(255, 130 + blueVar));
                        int b = Math.Max(0, Math.Min(255, 246 + blueVar));
                        bgColor = Color.FromArgb(r, gv, b);
                        borderColor = Color.FromArgb(96, 165, 250);
                        borderWidth = 2.5f;
                        textColor = Color.White;
                        break;

                    default: // Untested
                        bgColor = Theme.BgCard;
                        borderColor = Theme.Border;
                        borderWidth = 0.8f;
                        textColor = Theme.TextSecondary;
                        break;
                }
            }

            // For PressedMultiple, draw a subtle blue glow behind the key
            if (state == KeyTestState.PressedMultiple && !isFnKey)
            {
                var glowRect = Rectangle.Inflate(rect, 3, 3);
                using (var glowPath = Theme.RoundedRect(glowRect, 7))
                using (var glowBrush = new SolidBrush(Color.FromArgb(50, 59, 130, 246)))
                {
                    g.FillPath(glowBrush, glowPath);
                }
            }

            using (var path = Theme.RoundedRect(rect, 5))
            {
                using (var brush = new SolidBrush(bgColor))
                    g.FillPath(brush, path);
                using (var pen = new Pen(borderColor, borderWidth))
                    g.DrawPath(pen, path);
            }

            // Draw label
            float fontSize = key.WidthFactor > 1.5f ? 8f : 9f;
            // Use smaller font for half-height keys (arrows)
            if (key.Bounds.Height < _keySize * 0.75f)
                fontSize = 7.5f;

            using (var font = new Font("Segoe UI", Theme.S(fontSize), FontStyle.Regular))
            {
                var sz = g.MeasureString(label, font);
                float tx = key.Bounds.X + (key.Bounds.Width - sz.Width) / 2;
                float ty = key.Bounds.Y + (key.Bounds.Height - sz.Height) / 2;
                using (var brush = new SolidBrush(textColor))
                    g.DrawString(label, font, brush, tx, ty);
            }
        }

        private bool IsAbntLayout()
        {
            try
            {
                var currentLayout = InputLanguage.CurrentInputLanguage;
                if (currentLayout != null)
                {
                    string layoutName = currentLayout.LayoutName ?? "";
                    string cultureName = currentLayout.Culture?.DisplayName ?? "";

                    if (layoutName.IndexOf("Português", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        layoutName.IndexOf("Brazil", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        layoutName.IndexOf("ABNT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cultureName.IndexOf("Português", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cultureName.IndexOf("Brazil", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback: default to true since it's a Brazilian test app
                return true;
            }
            return false;
        }

        private void GetDynamicKeyDetails(KeyDef key, out string label, out Keys keycode)
        {
            label = key.Label;
            keycode = key.KeyCode;

            if (key.KeyCode == Keys.Oem8)
            {
                if (IsAbntLayout())
                {
                    label = "'";
                    keycode = Keys.Oem8;
                }
                else
                {
                    label = "~";
                    keycode = Keys.Oemtilde;
                }
            }
            else if (key.KeyCode == Keys.Oem7)
            {
                if (IsAbntLayout())
                {
                    label = "~";
                    keycode = Keys.Oem7;
                }
                else
                {
                    label = "'";
                    keycode = Keys.Oem7;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_pulseTimer != null)
                {
                    _pulseTimer.Stop();
                    _pulseTimer.Dispose();
                    _pulseTimer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
