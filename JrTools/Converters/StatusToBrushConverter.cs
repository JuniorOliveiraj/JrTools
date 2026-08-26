using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace JrTools.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string status = value as string ?? string.Empty;
            string key = status switch
            {
                var s when s.Equals("Equal", StringComparison.OrdinalIgnoreCase) => "SystemFillColorSuccessBrush",
                var s when s.Equals("Diferent", StringComparison.OrdinalIgnoreCase) || s.Equals("Modificado", StringComparison.OrdinalIgnoreCase) => "SystemFillColorCautionBrush",
                var s when s.Equals("FileOnly", StringComparison.OrdinalIgnoreCase) || s.Equals("Novo", StringComparison.OrdinalIgnoreCase) => "SystemFillColorAttentionBrush",
                _ => "TextFillColorSecondaryBrush"
            };

            return Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush b
                ? b
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }
}
