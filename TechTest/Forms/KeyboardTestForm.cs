using System;
using System.Drawing;
using System.Windows.Forms;
using TechTest.Controls;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class KeyboardTestForm : Form, IMessageFilter
    {
        private NotebookKeyboardControl _keyboard;
        private Label _lblCounter;
        private ProgressBar _progressBar;
        private Button _btnReset;
        private CheckBox _chkNumpad;
        private Label _lblTitle;
        private Label _lblDesc;

        private const int WM_KEYDOWN = 0x100;
        private const int WM_KEYUP = 0x101;
        private const int WM_SYSKEYDOWN = 0x104;
        private const int WM_SYSKEYUP = 0x105;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point pt;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        public KeyboardTestForm()
        {
            Theme.StyleForm(this, "⌨ Teste de Teclado", 950, 480);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(Theme.S(650), Theme.S(400));
            this.KeyPreview = true;
            this.KeyDown += OnFormKeyDown;
            this.KeyUp += OnFormKeyUp;

            Application.AddMessageFilter(this);

            BuildUI();

            // Set initial layout placement
            OnResize(EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_keyboard == null) return;

            int clientW = this.ClientSize.Width;
            int clientH = this.ClientSize.Height;

            // Title
            if (_lblTitle != null)
                _lblTitle.Location = new Point(Theme.S(30), Theme.S(15));

            // Description
            if (_lblDesc != null)
            {
                _lblDesc.Location = new Point(Theme.S(30), Theme.S(48));
                _lblDesc.MaximumSize = new Size(clientW - Theme.S(250), 0);
            }

            // Numpad toggle (align top right)
            if (_chkNumpad != null)
            {
                _chkNumpad.Location = new Point(clientW - _chkNumpad.Width - Theme.S(30), Theme.S(15));
            }

            // Keyboard Size & Position
            int keyboardTop = Theme.S(85);
            int footerHeight = Theme.S(65);
            int kbW = clientW - Theme.S(60);
            int kbH = clientH - keyboardTop - footerHeight;

            if (kbW < Theme.S(100)) kbW = Theme.S(100);
            if (kbH < Theme.S(100)) kbH = Theme.S(100);

            _keyboard.Location = new Point(Theme.S(30), keyboardTop);
            _keyboard.Size = new Size(kbW, kbH);

            // Footer layout
            int footerY = _keyboard.Bottom + Theme.S(15);

            if (_lblCounter != null)
                _lblCounter.Location = new Point(Theme.S(30), footerY + Theme.S(5));

            if (_progressBar != null)
            {
                _progressBar.Location = new Point(Theme.S(240), footerY + Theme.S(5));
                int pbW = clientW - Theme.S(240) - _btnReset.Width - Theme.S(60);
                if (pbW < Theme.S(50)) pbW = Theme.S(50);
                _progressBar.Size = new Size(pbW, Theme.S(16));
            }

            if (_btnReset != null)
                _btnReset.Location = new Point(clientW - _btnReset.Width - Theme.S(30), footerY);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_KEYDOWN && m.Msg != WM_KEYUP &&
                m.Msg != WM_SYSKEYDOWN && m.Msg != WM_SYSKEYUP)
                return false;

            // Only process messages when this form is active
            if (Form.ActiveForm != this)
                return false;

            int vkCode = (int)m.WParam & 0xFF;
            int lParam = (int)m.LParam;
            bool isExtended = (lParam & (1 << 24)) != 0;
            bool isKeyDown = (m.Msg == WM_KEYDOWN || m.Msg == WM_SYSKEYDOWN);

            Keys mappedKey = (Keys)vkCode;

            // For special keys (Windows, PrintScreen) that often lose focus or miss events,
            // immediately mark them as PressedOnce and consume the message to avoid stuck states.
            if (mappedKey == Keys.PrintScreen || mappedKey == Keys.LWin || mappedKey == Keys.RWin)
            {
                Keys targetKey = (mappedKey == Keys.RWin) ? Keys.LWin : mappedKey;
                _keyboard.MarkKeyPressed(targetKey);
                return true; // Consume the message so it doesn't process default down/up state transitions
            }

            // Map modifier keys to specific left/right variants
            if (vkCode == VK_SHIFT)
            {
                // Extract scan code from LParam bits 16-23
                uint scanCode = (uint)((lParam >> 16) & 0xFF);
                // MapVirtualKey with MAPVK_VSC_TO_VK_EX (3) to get extended key
                uint mappedVk = MapVirtualKey(scanCode, 3);
                if (mappedVk == 0)
                {
                    // Fallback: scancode 0x2A = LShift, 0x36 = RShift
                    mappedKey = (scanCode == 0x36) ? Keys.RShiftKey : Keys.LShiftKey;
                }
                else
                {
                    mappedKey = (Keys)mappedVk;
                }
            }
            else if (vkCode == VK_CONTROL)
            {
                // Discard fake Left Control messages generated when AltGr is pressed
                if (!isExtended)
                {
                    MSG nextMsg;
                    if (PeekMessage(out nextMsg, IntPtr.Zero, 0, 0, 0))
                    {
                        if ((nextMsg.message == WM_KEYDOWN || nextMsg.message == WM_SYSKEYDOWN ||
                             nextMsg.message == WM_KEYUP || nextMsg.message == WM_SYSKEYUP) &&
                            ((int)nextMsg.wParam & 0xFF) == VK_MENU &&
                            ((int)nextMsg.lParam & (1 << 24)) != 0) // Extended key is Right Alt
                        {
                            return true; // Discard fake Left Control message
                        }
                    }
                }
                mappedKey = isExtended ? Keys.RControlKey : Keys.LControlKey;
            }
            else if (vkCode == VK_MENU)
            {
                mappedKey = isExtended ? Keys.RMenu : Keys.LMenu;
            }

            if (isKeyDown)
            {
                _keyboard.MarkKeyDown(mappedKey);
            }
            else
            {
                _keyboard.MarkKeyUp(mappedKey);
            }

            // Don't consume the message; let ProcessCmdKey handle form-level consumption
            return false;
        }

        private void BuildUI()
        {
            _lblTitle = Theme.CreateLabel("Teste de Teclado", 30, 15, Theme.FontHeader);
            Controls.Add(_lblTitle);

            _lblDesc = Theme.CreateLabel(
                "Pressione cada tecla para verificar se está funcionando. Teclas testadas ficam verdes.",
                30, 48, Theme.FontSmall, Theme.TextSecondary);
            _lblDesc.MaximumSize = new Size(Theme.S(690), 0);
            Controls.Add(_lblDesc);

            // Keyboard control
            _keyboard = new NotebookKeyboardControl();
            _keyboard.HasNumpad = true;
            _keyboard.ProgressChanged += (s, count) => UpdateProgress();
            Controls.Add(_keyboard);

            // Numpad toggle
            _chkNumpad = new CheckBox
            {
                Text = "  Notebook com Numpad",
                AutoSize = true,
                Font = Theme.FontSmall,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Checked = true
            };
            _chkNumpad.CheckedChanged += (s, e) =>
            {
                _keyboard.HasNumpad = _chkNumpad.Checked;
                // Just change form width, triggering OnResize to re-layout everything
                this.Width = Theme.S(_chkNumpad.Checked ? 950 : 750);
            };
            Controls.Add(_chkNumpad);

            // Progress area
            _lblCounter = Theme.CreateLabel("Teclas testadas: 0 / 0", 30, 0, Theme.FontBody);
            Controls.Add(_lblCounter);

            _progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };
            Controls.Add(_progressBar);

            _btnReset = Theme.CreateSecondaryButton("🔄 Resetar", 0, 0, 110, 32);
            _btnReset.Click += (s, e) =>
            {
                _keyboard.ResetKeys();
                UpdateProgress();
            };
            Controls.Add(_btnReset);

            UpdateProgress();
        }

        // Intercept navigation keys (Tab, arrows, Enter, Escape, Space) that WinForms
        // normally consumes for control navigation before they reach KeyDown.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Strip modifiers to get the base key
            Keys key = keyData & Keys.KeyCode;

            // List of keys that WinForms steals for navigation
            if (key == Keys.Tab || key == Keys.Left || key == Keys.Right ||
                key == Keys.Up || key == Keys.Down || key == Keys.Enter ||
                key == Keys.Escape || key == Keys.Space ||
                key == Keys.Insert || key == Keys.Home || key == Keys.End ||
                key == Keys.PageUp || key == Keys.PageDown)
            {
                _keyboard.MarkKeyDown(key);
                return true; // Mark as handled so the form doesn't use it for navigation
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            _keyboard.MarkKeyDown(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void OnFormKeyUp(object sender, KeyEventArgs e)
        {
            _keyboard.MarkKeyUp(e.KeyCode);
            e.Handled = true;
        }

        private void UpdateProgress()
        {
            int pressed = _keyboard.PressedCount;
            int total = _keyboard.TotalKeys;
            _lblCounter.Text = $"Teclas testadas: {pressed} / {total}";

            if (total > 0)
                _progressBar.Value = Math.Min(100, pressed * 100 / total);

            if (pressed >= total && total > 0)
                _lblCounter.ForeColor = Theme.Success;
            else
                _lblCounter.ForeColor = Theme.TextPrimary;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Application.RemoveMessageFilter(this);
            base.OnFormClosing(e);
        }
    }
}
