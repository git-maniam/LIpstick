using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace LIpstick_App.Converters;

public class ConnectionColorConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(ColorHelper.FromArgb(255, 16, 185, 129));
    private static readonly SolidColorBrush RedBrush = new(ColorHelper.FromArgb(255, 239, 68, 68));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isConnected && isConnected)
        {
            return GreenBrush;
        }
        return RedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BoolToConnectedTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? "Connected" : "Disconnected";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class BoolToDisconnectButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? "Disconnect" : "Connect";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value is bool b && b) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class SectionTabStyleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value?.ToString()?.Equals(parameter?.ToString(), StringComparison.OrdinalIgnoreCase) == true)
        {
            if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accent))
            {
                return accent;
            }
        }
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("DefaultButtonStyle", out var def))
        {
            return def;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class MetricFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double val = value is double d ? d : 0.0;
        return $"{parameter}: {val:F1}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
