using System;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using TechTest.Maui.Services;

namespace TechTest.Maui.Controls
{
    public class SpeakerCanvasView : SKCanvasView
    {
        private bool _isPlaying;
        private AudioChannel _activeChannel = AudioChannel.Both;
        private float _animationPhase;
        private IDispatcherTimer _animationTimer;

        // Colors
        private static readonly SKColor BgDark = SKColor.Parse("#0A0A14");
        private static readonly SKColor BorderColor = SKColor.Parse("#32325A");
        private static readonly SKColor Accent = SKColor.Parse("#6366F1");
        private static readonly SKColor AccentGlow = SKColor.Parse("#3B82F6");
        private static readonly SKColor TextMuted = SKColor.Parse("#64648C");

        public SpeakerCanvasView()
        {
            PaintSurface += OnPaintSurface;
            StartAnimationTimer();
        }

        private void StartAnimationTimer()
        {
            _animationTimer = Application.Current?.Dispatcher?.CreateTimer();
            if (_animationTimer != null)
            {
                _animationTimer.Interval = TimeSpan.FromMilliseconds(30);
                _animationTimer.Tick += (s, e) =>
                {
                    if (_isPlaying)
                    {
                        _animationPhase = (_animationPhase + 0.05f) % 1.0f;
                        InvalidateSurface();
                    }
                };
                _animationTimer.Start();
            }
        }

        public void SetPlayingState(bool isPlaying, AudioChannel channel)
        {
            _isPlaying = isPlaying;
            _activeChannel = channel;
            if (isPlaying)
            {
                _animationPhase = 0f;
            }
            InvalidateSurface();
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            canvas.Clear(BgDark);

            float w = info.Width;
            float h = info.Height;

            // Draw clean background box
            using var borderPaint = new SKPaint
            {
                Color = BorderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRect(2, 2, w - 2, h - 2), 8f, 8f, borderPaint);

            // Left speaker center
            float leftCenterX = w * 0.25f;
            float rightCenterX = w * 0.75f;
            float centerY = h * 0.5f;
            float maxRadius = Math.Min(w * 0.15f, h * 0.35f);

            // Draw Left Speaker
            DrawSpeaker(canvas, leftCenterX, centerY, maxRadius, "ESQUERDO", 
                _isPlaying && (_activeChannel == AudioChannel.Left || _activeChannel == AudioChannel.Both));

            // Draw Right Speaker
            DrawSpeaker(canvas, rightCenterX, centerY, maxRadius, "DIREITO", 
                _isPlaying && (_activeChannel == AudioChannel.Right || _activeChannel == AudioChannel.Both));

            // Central laptop icon / details
            DrawLaptopIcon(canvas, w * 0.5f, centerY, maxRadius * 0.9f);
        }

        private void DrawSpeaker(SKCanvas canvas, float cx, float cy, float radius, string label, bool isAnimating)
        {
            // Outer casing
            using var casingPaint = new SKPaint
            {
                Color = SKColor.Parse("#121224"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(cx, cy, radius, casingPaint);

            using var casingBorderPaint = new SKPaint
            {
                Color = isAnimating ? AccentGlow : BorderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = isAnimating ? 3f : 1.5f,
                IsAntialias = true
            };
            canvas.DrawCircle(cx, cy, radius, casingBorderPaint);

            // Inner cone
            using var conePaint = new SKPaint
            {
                Color = SKColor.Parse("#1C1C38"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(cx, cy, radius * 0.7f, conePaint);

            // Center cap
            SKColor centerColor = isAnimating ? Accent : SKColor.Parse("#32325A");
            using var capPaint = new SKPaint
            {
                Color = centerColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(cx, cy, radius * 0.25f, capPaint);

            // Draw sound waves if animating
            if (isAnimating)
            {
                using var wavePaint = new SKPaint
                {
                    Color = AccentGlow,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2.5f,
                    IsAntialias = true,
                    StrokeCap = SKStrokeCap.Round
                };

                for (int i = 0; i < 3; i++)
                {
                    float waveProgress = (_animationPhase + i * 0.33f) % 1.0f;
                    float waveRadius = radius + waveProgress * (radius * 0.8f);
                    byte alpha = (byte)((1.0f - waveProgress) * 255);
                    wavePaint.Color = AccentGlow.WithAlpha(alpha);

                    // Left speaker: waves pulse to the left, Right speaker: waves pulse to the right
                    float startAngle = label == "ESQUERDO" ? 120f : -60f;
                    float sweepAngle = 120f;

                    using var path = new SKPath();
                    path.AddArc(new SKRect(cx - waveRadius, cy - waveRadius, cx + waveRadius, cy + waveRadius), startAngle, sweepAngle);
                    canvas.DrawPath(path, wavePaint);
                }
            }

            // Label
            using var textPaint = new SKPaint
            {
                Color = isAnimating ? AccentGlow : TextMuted,
                TextSize = 12f,
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            };
            canvas.DrawText(label, cx, cy + radius + 18f, textPaint);
        }

        private void DrawLaptopIcon(SKCanvas canvas, float cx, float cy, float size)
        {
            float w = size * 1.3f;
            float h = size * 0.8f;

            // Screen frame
            var screenRect = new SKRect(cx - w/2f, cy - h/2f - 8f, cx + w/2f, cy + h/2f - 8f);
            using var screenPaint = new SKPaint
            {
                Color = SKColor.Parse("#1C1C38"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(screenRect, 4f, 4f, screenPaint);

            using var screenBorder = new SKPaint
            {
                Color = BorderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };
            canvas.DrawRoundRect(screenRect, 4f, 4f, screenBorder);

            // Screen inner display
            var displayRect = new SKRect(screenRect.Left + 6, screenRect.Top + 6, screenRect.Right - 6, screenRect.Bottom - 6);
            using var displayPaint = new SKPaint
            {
                Color = SKColor.Parse("#0A0A14"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRect(displayRect, displayPaint);

            // If audio playing, draw frequency lines inside display
            if (_isPlaying)
            {
                using var waveLinePaint = new SKPaint
                {
                    Color = Accent.WithAlpha(120),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.5f,
                    IsAntialias = true
                };

                float displayW = displayRect.Width;
                float displayH = displayRect.Height;
                float displayCenterY = displayRect.MidY;

                using var path = new SKPath();
                path.MoveTo(displayRect.Left, displayCenterY);
                for (float dx = 0; dx <= displayW; dx += 2)
                {
                    float dy = (float)Math.Sin(dx * 0.1f + _animationPhase * Math.PI * 2) * (displayH * 0.25f);
                    path.LineTo(displayRect.Left + dx, displayCenterY + dy);
                }
                canvas.DrawPath(path, waveLinePaint);
            }

            // Keyboard base
            float baseTop = screenRect.Bottom;
            float baseBottom = baseTop + 8f;
            float baseW = w * 1.15f;

            using var basePaint = new SKPaint
            {
                Color = SKColor.Parse("#32325A"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            using var baseBorder = new SKPaint
            {
                Color = BorderColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };

            using var basePath = new SKPath();
            basePath.MoveTo(cx - w/2f - 4, baseTop);
            basePath.LineTo(cx + w/2f + 4, baseTop);
            basePath.LineTo(cx + baseW/2f, baseBottom);
            basePath.LineTo(cx - baseW/2f, baseBottom);
            basePath.Close();

            canvas.DrawPath(basePath, basePaint);
            canvas.DrawPath(basePath, baseBorder);
        }
    }
}
