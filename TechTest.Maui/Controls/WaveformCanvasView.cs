using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace TechTest.Maui.Controls;

public class WaveformCanvasView : SKCanvasView
{
    #region Bindable Properties

    public static readonly BindableProperty WaveformSamplesProperty =
        BindableProperty.Create(
            nameof(WaveformSamples),
            typeof(float[]),
            typeof(WaveformCanvasView),
            Array.Empty<float>(),
            propertyChanged: OnVisualPropertyChanged);

    public float[] WaveformSamples
    {
        get => (float[])GetValue(WaveformSamplesProperty);
        set => SetValue(WaveformSamplesProperty, value);
    }

    public static readonly BindableProperty PeakProperty =
        BindableProperty.Create(
            nameof(Peak),
            typeof(float),
            typeof(WaveformCanvasView),
            0f,
            propertyChanged: OnVisualPropertyChanged);

    public float Peak
    {
        get => (float)GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(
            nameof(IsActive),
            typeof(bool),
            typeof(WaveformCanvasView),
            false,
            propertyChanged: OnVisualPropertyChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is WaveformCanvasView view)
            view.InvalidateSurface();
    }

    #endregion

    // Colors
    private static readonly SKColor BgDark = SKColor.Parse("#0A0A14");
    private static readonly SKColor BorderColor = SKColor.Parse("#32325A");
    private static readonly SKColor Accent = SKColor.Parse("#6366F1");
    private static readonly SKColor AccentFaint = SKColor.Parse("#6366F1").WithAlpha(40);
    private static readonly SKColor VolGreen = SKColor.Parse("#22C55E");
    private static readonly SKColor VolYellow = SKColor.Parse("#EAB308");
    private static readonly SKColor VolRed = SKColor.Parse("#EF4444");

    public WaveformCanvasView()
    {
        PaintSurface += OnPaintSurface;
    }

    public void UpdateSamples(float[] samples)
    {
        WaveformSamples = samples;
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(BgDark);

        float w = info.Width;
        float h = info.Height;

        // Reserve right side for volume meter
        float volumeBarWidth = 50f;
        float volumeGap = 12f;
        float waveformWidth = w - volumeBarWidth - volumeGap - 8f;

        // Draw border around waveform area
        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, 0, waveformWidth, h), 4f), borderPaint);

        // Center line (faint accent)
        float centerY = h / 2f;
        using var centerLinePaint = new SKPaint
        {
            Color = AccentFaint,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawLine(4, centerY, waveformWidth - 4, centerY, centerLinePaint);

        // Draw waveform
        var samples = WaveformSamples;
        if (samples != null && samples.Length >= 2)
        {
            using var waveformPaint = new SKPaint
            {
                Color = Accent,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            using var path = new SKPath();
            float margin = 6f;
            float drawWidth = waveformWidth - margin * 2f;
            float halfH = h / 2f - margin;

            for (int i = 0; i < samples.Length; i++)
            {
                float x = margin + (float)i / samples.Length * drawWidth;
                float y = centerY - samples[i] * halfH;

                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }

            canvas.DrawPath(path, waveformPaint);
        }

        // Draw volume meter on the right
        float vmX = waveformWidth + volumeGap;
        float vmW = volumeBarWidth;
        float vmMarginY = 4f;

        // Volume meter border
        var vmRect = new SKRect(vmX, vmMarginY, vmX + vmW, h - vmMarginY);
        canvas.DrawRoundRect(new SKRoundRect(vmRect, 4f), borderPaint);

        // Volume fill
        float peak = Math.Clamp(Peak, 0f, 1f);
        float fillH = (h - vmMarginY * 2f - 4f) * peak;
        if (fillH > 0)
        {
            float fillTop = h - vmMarginY - 2f - fillH;
            var fillRect = new SKRect(vmX + 3, fillTop, vmX + vmW - 3, h - vmMarginY - 2f);

            SKColor fillColor = peak > 0.7f ? VolRed : peak > 0.3f ? VolYellow : VolGreen;
            using var fillPaint = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, fillTop),
                    new SKPoint(0, h - vmMarginY - 2f),
                    new[] { fillColor.WithAlpha(200), fillColor },
                    null,
                    SKShaderTileMode.Clamp),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(fillRect, 3f), fillPaint);
        }

        // Peak percentage text
        using var textPaint = new SKPaint
        {
            Color = SKColor.Parse("#9494B8"),
            TextSize = 11f,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
        string peakText = $"{(int)(peak * 100)}%";
        canvas.DrawText(peakText, vmX + vmW / 2f, h - vmMarginY + 14f, textPaint);
    }
}
