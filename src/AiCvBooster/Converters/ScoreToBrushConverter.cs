using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AiCvBooster.Converters;

public sealed class ScoreToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int score = value switch
        {
            int i => i,
            double d => (int)d,
            _ => 0
        };

        Color color = score switch
        {
            >= 80 => (Color)ColorConverter.ConvertFromString("#22C55E")!, // green
            >= 60 => (Color)ColorConverter.ConvertFromString("#6C63FF")!, // primary
            >= 40 => (Color)ColorConverter.ConvertFromString("#F59E0B")!, // amber
            _     => (Color)ColorConverter.ConvertFromString("#EF4444")!  // red
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
