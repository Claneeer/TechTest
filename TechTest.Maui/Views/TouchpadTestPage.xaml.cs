using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace TechTest.Maui.Views
{
    public partial class TouchpadTestPage : ContentPage
    {
        private const int GridCols = 8;
        private const int GridRows = 6;
        private const int TotalGridCells = GridCols * GridRows;

        private readonly bool[,] _gridCells = new bool[GridCols, GridRows];
        private readonly List<List<SKPoint>> _paths = new();
        private List<SKPoint> _currentPath;
        private int _clickCount = 0;

        public TouchpadTestPage()
        {
            InitializeComponent();
            ResetTest();
        }

        private void ResetTest()
        {
            Array.Clear(_gridCells, 0, _gridCells.Length);
            _paths.Clear();
            _currentPath = null;
            _clickCount = 0;

            UpdateStats();
            CanvasTouch.InvalidateSurface();
        }

        private void UpdateStats()
        {
            int covered = 0;
            for (int col = 0; col < GridCols; col++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    if (_gridCells[col, row])
                        covered++;
                }
            }

            double pct = (double)covered / TotalGridCells;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblCoverage.Text = $"{covered} / {TotalGridCells} ({(int)(pct * 100)}%)";
                ProgressBarCoverage.Progress = pct;
                LblClicks.Text = _clickCount.ToString();
            });
        }

        private void OnCanvasTouch(object sender, SKTouchEventArgs e)
        {
            var location = e.Location;
            float canvasW = CanvasTouch.CanvasSize.Width;
            float canvasH = CanvasTouch.CanvasSize.Height;

            if (canvasW <= 0 || canvasH <= 0) return;

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    _currentPath = new List<SKPoint> { location };
                    _paths.Add(_currentPath);
                    _clickCount++;
                    MarkGridCell(location.X, location.Y, canvasW, canvasH);
                    e.Handled = true;
                    break;

                case SKTouchAction.Moved:
                    if (_currentPath != null)
                    {
                        _currentPath.Add(location);
                        MarkGridCell(location.X, location.Y, canvasW, canvasH);
                    }
                    e.Handled = true;
                    break;

                case SKTouchAction.Released:
                case SKTouchAction.Cancelled:
                    _currentPath = null;
                    e.Handled = true;
                    break;
            }

            UpdateStats();
            CanvasTouch.InvalidateSurface();
        }

        private void MarkGridCell(float x, float y, float width, float height)
        {
            int col = (int)(x / (width / GridCols));
            int row = (int)(y / (height / GridRows));

            if (col >= 0 && col < GridCols && row >= 0 && row < GridRows)
            {
                _gridCells[col, row] = true;
            }
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            var info = e.Info;
            
            canvas.Clear(SKColor.Parse("#0A0A14"));

            float w = info.Width;
            float h = info.Height;
            float cellW = w / GridCols;
            float cellH = h / GridRows;

            // Draw filled tested cells
            using var cellPaint = new SKPaint
            {
                Color = SKColor.Parse("#22C55E").WithAlpha(40), // translucent green
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            for (int col = 0; col < GridCols; col++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    if (_gridCells[col, row])
                    {
                        var cellRect = new SKRect(col * cellW, row * cellH, (col + 1) * cellW, (row + 1) * cellH);
                        canvas.DrawRect(cellRect, cellPaint);
                    }
                }
            }

            // Draw Grid Lines
            using var gridPaint = new SKPaint
            {
                Color = SKColor.Parse("#32325A"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1f,
                IsAntialias = true
            };

            for (int i = 1; i < GridCols; i++)
            {
                canvas.DrawLine(i * cellW, 0, i * cellW, h, gridPaint);
            }
            for (int j = 1; j < GridRows; j++)
            {
                canvas.DrawLine(0, j * cellH, w, j * cellH, gridPaint);
            }

            // Draw User Paths (Stroke)
            using var brushPaint = new SKPaint
            {
                Color = SKColor.Parse("#3B82F6"),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 5f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true
            };

            foreach (var path in _paths)
            {
                if (path.Count < 2) continue;
                
                using var skPath = new SKPath();
                skPath.MoveTo(path[0]);
                for (int idx = 1; idx < path.Count; idx++)
                {
                    skPath.LineTo(path[idx]);
                }
                canvas.DrawPath(skPath, brushPaint);
            }
        }

        private void OnResetClicked(object sender, EventArgs e)
        {
            ResetTest();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            ResetTest();
            await Shell.Current.GoToAsync("//MainPage");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            ResetTest();
        }
    }
}
