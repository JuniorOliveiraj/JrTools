using Microsoft.UI.Xaml.Controls;
using System;

namespace JrTools.Utils
{
    /// <summary>
    /// Helpers para os pares de <see cref="NumberBox"/> (Hora/Minuto) usados como substituto do
    /// TimePicker em HomePage e LancarHoras — digitável, com formatação de 2 dígitos (ex.: "8"
    /// vira "08" ao sair do campo).
    /// </summary>
    internal static class CamposHoraMinutoHelper
    {
        /// <summary>Aplica um formatador de 2 dígitos aos NumberBox informados.</summary>
        public static void AplicarFormatacaoDoisDigitos(params NumberBox[] boxes)
        {
            var formatter = new Windows.Globalization.NumberFormatting.DecimalFormatter
            {
                IntegerDigits = 2,
                FractionDigits = 0
            };

            foreach (var box in boxes)
                box.NumberFormatter = formatter;
        }

        public static void Definir(NumberBox horaBox, NumberBox minutoBox, TimeSpan valor)
        {
            horaBox.Value = valor.Hours;
            minutoBox.Value = valor.Minutes;
        }

        /// <summary>Define os campos com o valor, ou os limpa (mostrando o placeholder) se nulo.</summary>
        public static void Definir(NumberBox horaBox, NumberBox minutoBox, TimeSpan? valor)
        {
            if (valor.HasValue)
            {
                Definir(horaBox, minutoBox, valor.Value);
            }
            else
            {
                horaBox.Value = double.NaN;
                minutoBox.Value = double.NaN;
            }
        }

        /// <summary>Lê os campos como TimeSpan, tratando valores não preenchidos como 0.</summary>
        public static TimeSpan Obter(NumberBox horaBox, NumberBox minutoBox)
            => ObterOuNull(horaBox, minutoBox) ?? TimeSpan.Zero;

        /// <summary>
        /// Lê os campos como TimeSpan, ou null se ambos estiverem vazios (nenhum valor digitado) —
        /// preserva a semântica de "horário não informado" que o TimePicker.SelectedTime tinha.
        /// </summary>
        public static TimeSpan? ObterOuNull(NumberBox horaBox, NumberBox minutoBox)
        {
            bool horaVazia = double.IsNaN(horaBox.Value);
            bool minutoVazio = double.IsNaN(minutoBox.Value);
            if (horaVazia && minutoVazio) return null;

            int hora = horaVazia ? 0 : (int)Math.Clamp(horaBox.Value, 0, 23);
            int minuto = minutoVazio ? 0 : (int)Math.Clamp(minutoBox.Value, 0, 59);
            return new TimeSpan(hora, minuto, 0);
        }
    }
}
