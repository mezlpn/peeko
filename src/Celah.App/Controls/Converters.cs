using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Celah.App.Controls;

/// <summary>true → Visible. Parameter "invert" membalik hasilnya.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool v = value is true;
        if (parameter as string == "invert") v = !v;
        return v ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Semua nilai bool true → Visible (untuk kondisi gabungan di MultiBinding).</summary>
public sealed class AllTrueToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool v = values.All(x => x is true);
        if (parameter as string == "invert") v = !v;
        return v ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
