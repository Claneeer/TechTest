using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace TechTest.Maui.Controls;

public class BatteryCanvasView : SKCanvasView
{
    #region Bindable Properties

    public static readonly BindableProperty ChargePercentProperty =
        BindableProperty.Create(
            nameof(ChargePercent),
            typeof(int),
            typeof(BatteryCanvasView),
            0,
            propertyChanged: OnVisualPropertyChanged);

    public int ChargePercent
    {
        get => (int)GetValue(ChargePercentProperty);
        set => SetValue(ChargePercentProperty, value);
    }

    public static readonly BindableProperty IsChargingProperty =
        BindableProperty.Create(
            nameof(IsCharging),
            typeof(bool),
            typeof(BatteryCanvasView),
            false,
            propertyChanged: OnVisualPropertyChanged);

    public bool IsCharging
    {
        get => (bool)GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BatteryCanvasView view)
            view.InvalidateSurface();
    }

    #endregion

    // Colors
    private static readonly SKColor BgCard = SKColor.Parse("#1C1C38");
    private static readonly SKColor BorderColor = SKColor.Parse("#32325A");
    private static readonly SKColor TextPrimary = SKColor.Parse("#F0F0FF");
    private static readonly SKColor TextSecondary = SKColor.Parse("#9494B8");
    private static readonly SKColor Green = SKColor.Parse("#22C55E");
    private static readonly SKColor Yellow = SKColor.Parse("#EAB308");
    private static readonly SKColor Red = SKColor.Parse("#EF4444");

    public BatteryCanvasView()
    {
        PaintSurface += OnPaintSurface;
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Transparent);

        float w = info.Width;
        float h = info.Height;

        // Card background
        var cardRect = new SKRoundRect(new SKRect(0, 0, w, h), 12f);
        using var cardPaint = new SKPaint
        {
            Color = BgCard,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(cardRect, cardPaint);

        // Battery dimensions
        float batW = w * 0.45f;
        float batH = h * 0.55f;
        float batX = (w - batW) / 2f;
        float batY = (h - batH) / 2f - h * 0.02f;

        // Battery terminal nub at top
        float nubW = batW * 0.3f;
        float nubH = 14f;
        float nubX = batX + (batW - nubW) / 2f;
        float nubY = batY - nubH + 4f;

        using var nubPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(nubX, nubY, nubX + nubW, nubY + nubH), 4f), nubPaint);

        // Battery body outline
        var bodyRect = new SKRect(batX, batY, batX + batW, batY + batH);
        var bodyRRect = new SKRoundRect(bodyRect, 12f);

        using var bodyBgPaint = new SKPaint
        {
            Color = SKColor.Parse("#191932"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(bodyRRect, bodyBgPaint);

        using var bodyBorderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            IsAntialias = true
        };
        canvas.DrawRoundRect(bodyRRect, bodyBorderPaint);

        // Fill level
        int pct = Math.Clamp(ChargePercent, 0, 100);
        float fillMargin = 5f;
        float maxFillH = batH - fillMargin * 2f;
        float fillH = maxFillH * pct / 100f;

        if (fillH > 0)
        {
            float fillTop = batY + batH - fillMargin - fillH;
            var fillRect = new SKRect(
                batX + fillMargin,
                fillTop,
                batX + batW - fillMargin,
                batY + batH - fillMargin);

            SKColor fillColor = pct > 50 ? Green : pct > 20 ? Yellow : Red;

            using var fillPaint = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, fillTop),
                    new SKPoint(0, batY + batH - fillMargin),
                    new[] { fillColor.WithAlpha(180), fillColor },
                    null,
                    SKShaderTileMode.Clamp),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(fillRect, 8f), fillPaint);

            // Subtle glossy highlight at top of fill
            float glossH = Math.Min(fillH * 0.3f, 20f);
            var glossRect = new SKRect(
                batX + fillMargin + 4f,
                fillTop + 2f,
                batX + batW - fillMargin - 4f,
                fillTop + glossH);
            using var glossPaint = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, fillTop + 2f),
                    new SKPoint(0, fillTop + glossH),
                    new[] { SKColors.White.WithAlpha(35), SKColors.Transparent },
                    null,
                    SKShaderTileMode.Clamp),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRoundRect(glossRect, 4f), glossPaint);
        }

        // Percentage text centered in battery
        string pctText = $"{pct}%";
        using var pctPaint = new SKPaint
        {
            Color = TextPrimary,
            TextSize = batH * 0.18f,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            FakeBoldText = true,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
        float textY = batY + batH / 2f + pctPaint.TextSize * 0.35f;
        canvas.DrawText(pctText, batX + batW / 2f, textY, pctPaint);

        // Charging lightning bolt icon
        if (IsCharging)
        {
            using var boltPaint = new SKPaint
            {
                Color = SKColor.Parse("#FBBF24"),
                TextSize = batH * 0.14f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI Emoji")
            };
            canvas.DrawText("⚡", batX + batW / 2f, batY + batH + boltPaint.TextSize + 8f, boltPaint);

            // Charging label
            using var chargingLabelPaint = new SKPaint
            {
                Color = Green,
                TextSize = 13f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            canvas.DrawText("Carregando", batX + batW / 2f, batY + batH + boltPaint.TextSize + 28f, chargingLabelPaint);
        }

        // Status label at bottom
        SKColor statusColor = pct > 50 ? Green : pct > 20 ? Yellow : Red;
        string statusLabel = pct > 50 ? "Bom" : pct > 20 ? "Médio" : "Baixo";
        using var statusPaint = new SKPaint
        {
            Color = statusColor,
            TextSize = 12f,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };

        float statusY = IsCharging ? batY + batH + 58f + statusPaint.TextSize : batY + batH + 20f;
        canvas.DrawText(statusLabel, batX + batW / 2f, statusY, statusPaint);
    }
}
