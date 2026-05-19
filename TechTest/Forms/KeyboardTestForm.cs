using System;
using System.Drawing;
using System.Windows.Forms;
using TechTest.Controls;
using TechTest.Helpers;

namespace TechTest.Forms
{
    public class KeyboardTestForm : Form
    {
        private NotebookKeyboardControl _keyboard;
        private Label _lblCounter;
        private ProgressBar _progressBar;
        private Button _btnReset;
        private CheckBox _chkNumpad;

        public KeyboardTestForm()
        {
            Theme.StyleForm(this, "⌨ Teste de Teclado", 950, 480);
            this.KeyPreview = true;
            this.KeyDown += OnFormKeyDown;
            BuildUI();
        }

        private void BuildUI()
        {
            var lblTitle = Theme.CreateLabel("Teste de Teclado", 30, 15, Theme.FontHeader);
            Controls.Add(lblTitle);

            var lblDesc = Theme.CreateLabel(
                "Pressione cada tecla para verificar se está funcionando. Teclas testadas ficam verdes.",
                30, 48, Theme.FontSmall, Theme.TextSecondary);
            lblDesc.MaximumSize = new Size(Theme.S(690), 0);
            Controls.Add(lblDesc);

            // Keyboard control
            _keyboard = new NotebookKeyboardControl
            {
                Location = new Point(Theme.S(30), Theme.S(85))
            };
            _keyboard.HasNumpad = true;
            _keyboard.ProgressChanged += (s, count) => UpdateProgress();
            Controls.Add(_keyboard);

            // Numpad toggle
            _chkNumpad = new CheckBox
            {
                Text = "  Notebook com Numpad",
                Location = new Point(Theme.S(520), Theme.S(48)),
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
                // Resize form and progress bar to fit numpad using scaled values
                int newWidth = Theme.S(_chkNumpad.Checked ? 950 : 750);
                this.Width = newWidth;
                _progressBar.Width = newWidth - Theme.S(190);
                _btnReset.Left = newWidth - Theme.S(140);
            };
            Controls.Add(_chkNumpad);

            // Progress area
            int bottomY = Theme.S(370);

            _lblCounter = Theme.CreateLabel("Teclas testadas: 0 / 0", 30, bottomY, Theme.FontBody);
            Controls.Add(_lblCounter);

            _progressBar = new ProgressBar
            {
                Location = new Point(Theme.S(30), bottomY + Theme.S(30)),
                Size = new Size(Theme.S(760), Theme.S(20)),
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };
            Controls.Add(_progressBar);

            _btnReset = Theme.CreateSecondaryButton("🔄 Resetar", 810, bottomY + 24, 110, 32);
            _btnReset.Click += (s, e) =>
            {
                _keyboard.ResetKeys();
                UpdateProgress();
            };
            Controls.Add(_btnReset);

            UpdateProgress();
        }

        // Intercept navigation keys (Tab, arrows, Enter, Escape) that WinForms
        // normally consumes for control navigation before they reach KeyDown.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Strip modifiers to get the base key
            Keys key = keyData & Keys.KeyCode;

            // List of keys that WinForms steals for navigation
            if (key == Keys.Tab || key == Keys.Left || key == Keys.Right ||
                key == Keys.Up || key == Keys.Down || key == Keys.Enter ||
                key == Keys.Escape || key == Keys.Space)
            {
                _keyboard.MarkKeyPressed(key);
                return true; // Mark as handled so the form doesn't use it for navigation
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            _keyboard.MarkKeyPressed(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
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
    }
}
