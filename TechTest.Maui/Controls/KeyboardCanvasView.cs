using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace TechTest.Maui.Controls;

public enum KeyState
{
    Untested,
    PressedOnce,
    PressedMultiple,
    HeldDown
}

public class KeyboardCanvasView : SKCanvasView
{
    #region Key Definition

    private struct KeyDef
    {
        public string Label;
        public float WidthFactor;
        public string KeyId;
        public SKRect Bounds;
        public bool IsSpacer;
        public float HeightFactor;

        public static KeyDef Key(string label, float widthFactor, string keyId, float heightFactor = 1.0f)
        {
            return new KeyDef
            {
                Label = label,
                WidthFactor = widthFactor,
                KeyId = keyId,
                IsSpacer = false,
                HeightFactor = heightFactor
            };
        }

        public static KeyDef Spacer(float widthFactor)
        {
            return new KeyDef
            {
                Label = null,
                WidthFactor = widthFactor,
                KeyId = null,
                IsSpacer = true,
                HeightFactor = 1.0f
            };
        }
    }

    #endregion

    #region Bindable Properties

    public static readonly BindableProperty HasNumpadProperty =
        BindableProperty.Create(
            nameof(HasNumpad),
            typeof(bool),
            typeof(KeyboardCanvasView),
            true,
            propertyChanged: OnLayoutPropertyChanged);

    public bool HasNumpad
    {
        get => (bool)GetValue(HasNumpadProperty);
        set => SetValue(HasNumpadProperty, value);
    }

    private static void OnLayoutPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is KeyboardCanvasView view)
        {
            view.BuildLayout();
            view.InvalidateSurface();
        }
    }

    #endregion

    #region State

    private Dictionary<string, KeyState> _keyStates = new();
    private HashSet<string> _currentlyHeldKeys = new();
    private List<List<KeyDef>> _rows = new();
    private List<List<KeyDef>> _numpadRows = new();
    private float _mainWidth;
    private int _totalKeys;
    private int _pressedCount;
    private float _pulsePhase;
    private IDispatcherTimer _pulseTimer;

    public Dictionary<string, KeyState> KeyStates => _keyStates;
    public HashSet<string> CurrentlyHeldKeys => _currentlyHeldKeys;

    public int PressedCount => _pressedCount;
    public int TotalKeys => _totalKeys;

    public event EventHandler<int> ProgressChanged;

    #endregion

    #region Colors

    private static readonly SKColor BgCard = SKColor.Parse("#1C1C38");
    private static readonly SKColor BorderDefault = SKColor.Parse("#32325A");
    private static readonly SKColor TextSecondary = SKColor.Parse("#9494B8");
    private static readonly SKColor GreenBg = SKColor.Parse("#22C55E");
    private static readonly SKColor GreenBorder = SKColor.Parse("#38DC78");
    private static readonly SKColor BlueBg = SKColor.Parse("#3B82F6");
    private static readonly SKColor BlueBorder = SKColor.Parse("#60A5FA");
    private static readonly SKColor BlueGlow = SKColor.Parse("#3B82F6");
    private static readonly SKColor TextMuted = SKColor.Parse("#64648C");
    private static readonly SKColor BgDark = SKColor.Parse("#0A0A14");

    #endregion

    public KeyboardCanvasView()
    {
        PaintSurface += OnPaintSurface;
        BuildLayout();
        StartPulseTimer();
    }

    private void StartPulseTimer()
    {
        _pulseTimer = Application.Current?.Dispatcher?.CreateTimer();
        if (_pulseTimer != null)
        {
            _pulseTimer.Interval = TimeSpan.FromMilliseconds(50);
            _pulseTimer.Tick += (s, e) =>
            {
                if (_currentlyHeldKeys.Count > 0)
                {
                    _pulsePhase += 0.12f;
                    InvalidateSurface();
                }
            };
            _pulseTimer.Start();
        }
    }

    #region Key State Management

    public void KeyPressed(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return;

        if (_keyStates.TryGetValue(keyId, out var state))
        {
            if (state == KeyState.Untested)
                _keyStates[keyId] = KeyState.PressedOnce;
            else if (state == KeyState.PressedOnce || state == KeyState.HeldDown)
                _keyStates[keyId] = KeyState.PressedMultiple;
        }
        else
        {
            _keyStates[keyId] = KeyState.PressedOnce;
        }

        RecalculatePressedCount();
        InvalidateSurface();
    }

    public void KeyDown(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return;

        _currentlyHeldKeys.Add(keyId);

        // If not yet registered, mark as PressedOnce first
        if (!_keyStates.ContainsKey(keyId) || _keyStates[keyId] == KeyState.Untested)
        {
            _keyStates[keyId] = KeyState.PressedOnce;
            RecalculatePressedCount();
        }

        InvalidateSurface();
    }

    public void KeyUp(string keyId)
    {
        if (string.IsNullOrEmpty(keyId)) return;

        _currentlyHeldKeys.Remove(keyId);

        if (_keyStates.TryGetValue(keyId, out var state))
        {
            // On release, if we had registered multiple presses or it was held, keep appropriate state
            if (state == KeyState.HeldDown || state == KeyState.PressedMultiple)
                _keyStates[keyId] = KeyState.PressedMultiple;
            else
                _keyStates[keyId] = KeyState.PressedOnce;
        }

        InvalidateSurface();
    }

    public void ResetKeys()
    {
        _keyStates.Clear();
        _currentlyHeldKeys.Clear();
        _pressedCount = 0;
        ProgressChanged?.Invoke(this, 0);
        InvalidateSurface();
    }

    private void RecalculatePressedCount()
    {
        int count = 0;
        // Only count keys that are part of the layout
        var allKeyIds = GetAllKeyIds();
        foreach (var kvp in _keyStates)
        {
            if (allKeyIds.Contains(kvp.Key) && kvp.Value != KeyState.Untested)
                count++;
        }
        _pressedCount = count;
        ProgressChanged?.Invoke(this, _pressedCount);
    }

    private HashSet<string> GetAllKeyIds()
    {
        var ids = new HashSet<string>();
        foreach (var row in _rows)
            foreach (var key in row)
                if (!key.IsSpacer && key.KeyId != null)
                    ids.Add(key.KeyId);

        if (HasNumpad)
            foreach (var row in _numpadRows)
                foreach (var key in row)
                    if (!key.IsSpacer && key.KeyId != null)
                        ids.Add(key.KeyId);

        return ids;
    }

    public string MapKeyToId(string platformKey)
    {
        // Normalize common platform key codes to our KeyId strings
        return platformKey switch
        {
            "Escape" or "Esc" => "Esc",
            "F1" => "F1", "F2" => "F2", "F3" => "F3", "F4" => "F4",
            "F5" => "F5", "F6" => "F6", "F7" => "F7", "F8" => "F8",
            "F9" => "F9", "F10" => "F10", "F11" => "F11", "F12" => "F12",
            "PrintScreen" or "Snapshot" => "PrtSc",
            "Delete" => "Del",
            "OemQuotes" or "Oem3" or "OemTilde" => "'",
            "D1" or "1" => "1", "D2" or "2" => "2", "D3" or "3" => "3",
            "D4" or "4" => "4", "D5" or "5" => "5", "D6" or "6" => "6",
            "D7" or "7" => "7", "D8" or "8" => "8", "D9" or "9" => "9",
            "D0" or "0" => "0",
            "OemMinus" => "-",
            "OemPlus" or "Oemplus" => "=",
            "Back" or "Backspace" => "Backspace",
            "Tab" => "Tab",
            "Q" => "Q", "W" => "W", "E" => "E", "R" => "R", "T" => "T",
            "Y" => "Y", "U" => "U", "I" => "I", "O" => "O", "P" => "P",
            "OemOpenBrackets" or "Oem4" => "[",
            "OemCloseBrackets" or "Oem6" or "Oem1" when platformKey == "Oem6" => "]",
            "Return" or "Enter" => "Enter",
            "CapsLock" or "Capital" => "Caps",
            "A" => "A", "S" => "S", "D" => "D", "F" => "F", "G" => "G",
            "H" => "H", "J" => "J", "K" => "K", "L" => "L",
            "Oem1" or "OemSemicolon" => "Ç",
            "Oem7" or "OemQuotes2" => "~",
            "Oem5" or "OemPipe" => "\\",
            "LShiftKey" or "LeftShift" => "LShift",
            "RShiftKey" or "RightShift" => "RShift",
            "ShiftKey" or "Shift" => "LShift", // fallback, caller should disambiguate
            "OemBackslash" or "Oem102" => "|",
            "Z" => "Z", "X" => "X", "C" => "C", "V" => "V", "B" => "B",
            "N" => "N", "M" => "M",
            "OemComma" or "Oemcomma" => ",",
            "OemPeriod" => ".",
            "OemQuestion" or "Oem2" => "/",
            "LControlKey" or "LeftCtrl" or "LeftControl" => "LCtrl",
            "RControlKey" or "RightCtrl" or "RightControl" => "RCtrl",
            "ControlKey" or "Ctrl" or "Control" => "LCtrl", // fallback
            "LWin" or "LeftWindows" or "LMeta" => "Win",
            "RWin" or "RightWindows" or "RMeta" => "Win",
            "LMenu" or "LeftAlt" => "Alt",
            "RMenu" or "RightAlt" => "AltGr",
            "Menu" or "Alt" => "Alt", // fallback
            "Space" => "Space",
            "Left" or "ArrowLeft" => "Left",
            "Right" or "ArrowRight" => "Right",
            "Up" or "ArrowUp" => "Up",
            "Down" or "ArrowDown" => "Down",
            "Fn" => "Fn",
            // Numpad
            "NumLock" => "Num",
            "Divide" or "NumpadDivide" => "Np/",
            "Multiply" or "NumpadMultiply" => "Np*",
            "Subtract" or "NumpadSubtract" => "Np-",
            "NumPad7" or "Numpad7" => "Np7",
            "NumPad8" or "Numpad8" => "Np8",
            "NumPad9" or "Numpad9" => "Np9",
            "Add" or "NumpadAdd" => "Np+",
            "NumPad4" or "Numpad4" => "Np4",
            "NumPad5" or "Numpad5" => "Np5",
            "NumPad6" or "Numpad6" => "Np6",
            "NumPad1" or "Numpad1" => "Np1",
            "NumPad2" or "Numpad2" => "Np2",
            "NumPad3" or "Numpad3" => "Np3",
            "NumpadEnter" => "NpEnt",
            "NumPad0" or "Numpad0" => "Np0",
            "Decimal" or "NumpadDecimal" => "Np.",
            _ => platformKey
        };
    }

    #endregion

    #region Layout

    private void BuildLayout()
    {
        _rows.Clear();
        _numpadRows.Clear();

        // Row 0: Function row
        _rows.Add(new List<KeyDef>
        {
            K("Esc", 1f, "Esc"), S(0.3f),
            K("F1", 1f, "F1"), K("F2", 1f, "F2"), K("F3", 1f, "F3"), K("F4", 1f, "F4"), S(0.3f),
            K("F5", 1f, "F5"), K("F6", 1f, "F6"), K("F7", 1f, "F7"), K("F8", 1f, "F8"), S(0.3f),
            K("F9", 1f, "F9"), K("F10", 1f, "F10"), K("F11", 1f, "F11"), K("F12", 1f, "F12"), S(0.3f),
            K("PrtSc", 1f, "PrtSc"), K("Del", 1f, "Del"),
        });

        // Row 1: Number row
        _rows.Add(new List<KeyDef>
        {
            K("'", 1f, "'"), K("1", 1f, "1"), K("2", 1f, "2"), K("3", 1f, "3"),
            K("4", 1f, "4"), K("5", 1f, "5"), K("6", 1f, "6"), K("7", 1f, "7"),
            K("8", 1f, "8"), K("9", 1f, "9"), K("0", 1f, "0"),
            K("-", 1f, "-"), K("=", 1f, "="),
            K("⌫", 2f, "Backspace"),
        });

        // Row 2: QWERTY row
        _rows.Add(new List<KeyDef>
        {
            K("Tab", 1.5f, "Tab"),
            K("Q", 1f, "Q"), K("W", 1f, "W"), K("E", 1f, "E"), K("R", 1f, "R"),
            K("T", 1f, "T"), K("Y", 1f, "Y"), K("U", 1f, "U"), K("I", 1f, "I"),
            K("O", 1f, "O"), K("P", 1f, "P"),
            K("[", 1f, "["), K("]", 1f, "]"),
            K("Enter", 1.5f, "Enter"),
        });

        // Row 3: Home row
        _rows.Add(new List<KeyDef>
        {
            K("Caps", 1.8f, "Caps"),
            K("A", 1f, "A"), K("S", 1f, "S"), K("D", 1f, "D"), K("F", 1f, "F"),
            K("G", 1f, "G"), K("H", 1f, "H"), K("J", 1f, "J"), K("K", 1f, "K"),
            K("L", 1f, "L"), K("Ç", 1f, "Ç"),
            K("~", 1f, "~"), K("\\", 1f, "\\"),
        });

        // Row 4: Shift row
        _rows.Add(new List<KeyDef>
        {
            K("Shift", 1.3f, "LShift"),
            K("|", 1f, "|"),
            K("Z", 1f, "Z"), K("X", 1f, "X"), K("C", 1f, "C"), K("V", 1f, "V"),
            K("B", 1f, "B"), K("N", 1f, "N"), K("M", 1f, "M"),
            K(",", 1f, ","), K(".", 1f, "."), K("/", 1f, "/"),
            K("Shift", 2.7f, "RShift"),
        });

        // Row 5: Bottom row (arrows are handled separately)
        _rows.Add(new List<KeyDef>
        {
            K("Ctrl", 1.3f, "LCtrl"),
            K("Fn", 1f, "Fn"),
            K("Win", 1.1f, "Win"),
            K("Alt", 1.2f, "Alt"),
            K("Espaço", 5.2f, "Space"),
            K("AltGr", 1.2f, "AltGr"),
            K("Ctrl", 1.3f, "RCtrl"), S(0.3f),
            K("←", 1f, "Left"),
            // Up/Down split keys placeholder — actual position computed in layout
            K("↑", 1f, "Up", 0.5f),
            K("→", 1f, "Right"),
        });

        // Build numpad if enabled
        if (HasNumpad)
        {
            _numpadRows.Add(new List<KeyDef>
            {
                K("Num", 1f, "Num"), K("/", 1f, "Np/"),
                K("*", 1f, "Np*"), K("-", 1f, "Np-"),
            });

            _numpadRows.Add(new List<KeyDef>
            {
                K("7", 1f, "Np7"), K("8", 1f, "Np8"),
                K("9", 1f, "Np9"), K("+", 1f, "Np+"),
            });

            _numpadRows.Add(new List<KeyDef>
            {
                K("4", 1f, "Np4"), K("5", 1f, "Np5"),
                K("6", 1f, "Np6"), S(1f),
            });

            _numpadRows.Add(new List<KeyDef>
            {
                K("1", 1f, "Np1"), K("2", 1f, "Np2"),
                K("3", 1f, "Np3"), K("Ent", 1f, "NpEnt"),
            });

            _numpadRows.Add(new List<KeyDef>
            {
                K("0", 2f, "Np0"), K(".", 1f, "Np."), S(1f),
            });
        }

        // Recalculate total keys
        _totalKeys = 0;
        foreach (var row in _rows)
            foreach (var key in row)
                if (!key.IsSpacer && key.KeyId != null)
                    _totalKeys++;

        // Count ↓ separately (not in _rows yet, added during paint)
        _totalKeys++; // for "Down" key

        if (HasNumpad)
            foreach (var row in _numpadRows)
                foreach (var key in row)
                    if (!key.IsSpacer && key.KeyId != null)
                        _totalKeys++;

        RecalculatePressedCount();
    }

    private static KeyDef K(string label, float widthFactor, string keyId, float heightFactor = 1.0f)
        => KeyDef.Key(label, widthFactor, keyId, heightFactor);

    private static KeyDef S(float widthFactor)
        => KeyDef.Spacer(widthFactor);

    #endregion

    #region Paint

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(BgDark);

        float canvasW = info.Width;
        float canvasH = info.Height;

        // Calculate key size based on available space
        // Total row width units for longest row (row 1 has ~15 units)
        float totalRowUnits = 15f; // approximate
        float padding = 10f;
        float gap = 3f;

        // Calculate available width for the main keyboard
        float numpadTotalUnits = HasNumpad ? 4f : 0f;
        float numpadSepWidth = HasNumpad ? 25f : 0f;

        // We have 6 main rows + need to fit
        float availW = canvasW - padding * 2f - numpadSepWidth - (HasNumpad ? (numpadTotalUnits * 40f + 5 * gap) : 0f);
        float ks = (availW - totalRowUnits * gap) / totalRowUnits;
        ks = Math.Min(ks, 44f); // cap key size
        ks = Math.Max(ks, 20f); // minimum

        // Recalculate with actual row widths to fit properly
        float maxUnits = 0;
        foreach (var row in _rows)
        {
            float rowUnits = 0;
            foreach (var key in row)
                rowUnits += key.WidthFactor;
            if (rowUnits > maxUnits) maxUnits = rowUnits;
        }

        ks = (availW - maxUnits * gap) / maxUnits;
        ks = Math.Min(ks, 44f);
        ks = Math.Max(ks, 20f);

        // Calculate bounds for all keys
        float offsetY = padding;
        float maxRowWidth = 0f;
        for (int r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            float offsetX = padding;
            for (int i = 0; i < row.Count; i++)
            {
                var key = row[i];
                float w = key.WidthFactor * ks;
                if (key.IsSpacer)
                {
                    offsetX += w + gap;
                    continue;
                }

                float keyHeight = ks * key.HeightFactor;
                key.Bounds = new SKRect(offsetX, offsetY, offsetX + w, offsetY + keyHeight);
                row[i] = key;
                offsetX += w + gap;
            }
            if (offsetX > maxRowWidth) maxRowWidth = offsetX;
            offsetY += ks + gap;
        }

        _mainWidth = maxRowWidth;

        // Draw all main keyboard keys
        for (int r = 0; r < _rows.Count; r++)
        {
            var row = _rows[r];
            for (int i = 0; i < row.Count; i++)
            {
                var key = row[i];
                if (key.IsSpacer) continue;

                // Special handling for Up key - draw it at top half
                if (key.KeyId == "Up")
                {
                    DrawKey(canvas, key, ks, gap);
                    // Also draw Down key at bottom half
                    var downKey = K("↓", 1f, "Down", 0.5f);
                    float downY = key.Bounds.Top + ks * 0.5f;
                    downKey.Bounds = new SKRect(key.Bounds.Left, downY, key.Bounds.Right, downY + ks * 0.5f);
                    DrawKey(canvas, downKey, ks, gap);
                    continue;
                }

                DrawKey(canvas, key, ks, gap);
            }
        }

        // Draw numpad
        if (HasNumpad && _numpadRows.Count > 0)
        {
            float numpadStartX = _mainWidth + 15f;

            // Draw separator line (dotted)
            float sepX = _mainWidth + 6f;
            using var sepPaint = new SKPaint
            {
                Color = BorderDefault,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                PathEffect = SKPathEffect.CreateDash(new[] { 4f, 4f }, 0),
                IsAntialias = true
            };
            canvas.DrawLine(sepX, padding + 8f, sepX, offsetY - gap - 8f, sepPaint);

            // NUMPAD label
            using var numpadLabelPaint = new SKPaint
            {
                Color = TextMuted,
                TextSize = 10f,
                IsAntialias = true,
                FakeBoldText = true,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            canvas.DrawText("NUMPAD", numpadStartX, padding + ks - 2f, numpadLabelPaint);

            // Numpad starts aligned with row 1 (skip function row)
            float numOffsetY = padding + ks + gap;
            for (int r = 0; r < _numpadRows.Count; r++)
            {
                var row = _numpadRows[r];
                float numOffsetX = numpadStartX;
                for (int i = 0; i < row.Count; i++)
                {
                    var key = row[i];
                    float w = key.WidthFactor * ks;
                    if (key.IsSpacer)
                    {
                        numOffsetX += w + gap;
                        continue;
                    }
                    key.Bounds = new SKRect(numOffsetX, numOffsetY, numOffsetX + w, numOffsetY + ks);
                    row[i] = key;
                    numOffsetX += w + gap;
                    DrawKey(canvas, key, ks, gap);
                }
                numOffsetY += ks + gap;
            }
        }
    }

    private void DrawKey(SKCanvas canvas, KeyDef key, float ks, float gap)
    {
        if (key.IsSpacer || key.KeyId == null) return;

        KeyState state = KeyState.Untested;
        if (_currentlyHeldKeys.Contains(key.KeyId))
            state = KeyState.HeldDown;
        else if (_keyStates.TryGetValue(key.KeyId, out var s))
            state = s;

        SKColor bgColor, borderColor, textColor;
        float borderWidth = 1f;

        switch (state)
        {
            case KeyState.PressedOnce:
                bgColor = GreenBg;
                borderColor = GreenBorder;
                textColor = SKColors.White;
                borderWidth = 1.5f;
                break;
            case KeyState.PressedMultiple:
                bgColor = GreenBg;
                borderColor = BlueGlow;
                textColor = SKColors.White;
                borderWidth = 3f;
                break;
            case KeyState.HeldDown:
                float pulse = (float)(Math.Sin(_pulsePhase) * 0.5 + 0.5);
                byte alpha = (byte)(200 + (int)(55 * pulse));
                bgColor = BlueBg.WithAlpha(alpha);
                borderColor = BlueBorder;
                textColor = SKColors.White;
                borderWidth = 2f;
                break;
            default: // Untested
                bgColor = BgCard;
                borderColor = BorderDefault;
                textColor = TextSecondary;
                borderWidth = 0.8f;
                break;
        }

        var rect = key.Bounds;
        var rrect = new SKRoundRect(rect, 5f);

        // For PressedMultiple, draw blue glow behind
        if (state == KeyState.PressedMultiple)
        {
            var glowRect = SKRect.Inflate(rect, 3f, 3f);
            using var glowPaint = new SKPaint
            {
                Color = BlueGlow.WithAlpha(80),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4f)
            };
            canvas.DrawRoundRect(new SKRoundRect(glowRect, 7f), glowPaint);
        }

        // Key background
        using var bgPaint = new SKPaint
        {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rrect, bgPaint);

        // Key border
        using var borderPaint = new SKPaint
        {
            Color = borderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rrect, borderPaint);

        // Key label
        float fontSize = key.WidthFactor > 1.5f ? 9f : key.HeightFactor < 1f ? 8f : 10f;
        // Adjust for very small keys
        if (rect.Height < 25f) fontSize = 7f;

        using var textPaint = new SKPaint
        {
            Color = textColor,
            TextSize = fontSize,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        float textX = rect.MidX;
        float textY = rect.MidY + textPaint.TextSize * 0.35f;
        canvas.DrawText(key.Label ?? "", textX, textY, textPaint);
    }

    #endregion

    #region Cleanup

    protected virtual void OnDisappearing()
    {
        _pulseTimer?.Stop();
    }

    #endregion
}
