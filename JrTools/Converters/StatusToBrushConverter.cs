using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using JrTools.Dto;

namespace JrTools.Converters
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            StatusArtefato status = value switch
            {
                StatusArtefato s => s,
                string str => StatusArtefatoExtensions.ParseStatus(str),
                _ => StatusArtefato.NaoVerificado
            };

            string key = status switch
            {
                StatusArtefato.Instalado => "SystemFillColorSuccessBrush",
                StatusArtefato.Modificado => "SystemFillColorCautionBrush",
                StatusArtefato.Novo => "SystemFillColorAttentionBrush",
                StatusArtefato.Excluido => "SystemFillColorCriticalBrush",
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
