namespace TechTest.Maui.Helpers;

public static class ResponsiveHelper
{
    private static double _scaleFactor = 1.0;
    private static bool _isCustomScale;

    public static double ScaleFactor
    {
        get => _scaleFactor;
        private set => _scaleFactor = value;
    }

    public static bool IsCustomScale
    {
        get => _isCustomScale;
        private set => _isCustomScale = value;
    }

    public static double S(double value)
    {
        return value * _scaleFactor;
    }

    public static void ResetScale()
    {
        _isCustomScale = false;

        var displayInfo = DeviceDisplay.MainDisplayInfo;
        double density = displayInfo.Density;

        if (density <= 0)
        {
            _scaleFactor = 1.0;
            return;
        }

        double widthInDip = displayInfo.Width / density;

        if (widthInDip >= 1920)
        {
            _scaleFactor = 1.0;
        }
        else if (widthInDip >= 1440)
        {
            _scaleFactor = 0.95;
        }
        else if (widthInDip >= 1280)
        {
            _scaleFactor = 0.9;
        }
        else if (widthInDip >= 1024)
        {
            _scaleFactor = 0.85;
        }
        else if (widthInDip >= 768)
        {
            _scaleFactor = 0.8;
        }
        else
        {
            _scaleFactor = 0.75;
        }
    }

    public static void SetScale(double factor)
    {
        if (factor <= 0)
            return;

        _scaleFactor = factor;
        _isCustomScale = true;
    }

    public static int GetOptimalColumns(double availableWidth, double minCardWidth)
    {
        if (availableWidth <= 0 || minCardWidth <= 0)
            return 1;

        int columns = (int)Math.Floor(availableWidth / minCardWidth);
        return Math.Max(1, columns);
    }
}
