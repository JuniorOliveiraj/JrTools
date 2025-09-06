using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JrTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
  
        public HomePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += RenderizarPages_Loaded;


            // Define horários padrão para facilitar o teste
            Entrada1TimePicker.Time = new TimeSpan(7, 0, 0);
            Saida1TimePicker.Time = new TimeSpan(12, 0, 0);
            Entrada2TimePicker.Time = new TimeSpan(13, 0, 0);
            Saida2TimePicker.Time = new TimeSpan(17, 0, 0);
        }
        private void RenderizarPages_Loaded(object sender, RoutedEventArgs e)
        {
            InnerFrame.Navigate(typeof(JrTools.Pages.FecharProcessos));

           
        }
        private void CalcularHorasButton_Click(object sender, RoutedEventArgs e)
        {
            CalculadoraInfoBar.IsOpen = false;

            try
            {
                // Pega os valores dos TimePickers
                TimeSpan entrada1 = Entrada1TimePicker.Time;
                TimeSpan saida1 = Saida1TimePicker.Time;
                TimeSpan entrada2 = Entrada2TimePicker.Time;
                TimeSpan saida2 = Saida2TimePicker.Time;

                // Validação simples
                if (saida1 < entrada1 || saida2 < entrada2)
                {
                    CalculadoraInfoBar.Title = "Erro de Lógica";
                    CalculadoraInfoBar.Message = "O horário de saída não pode ser anterior ao de entrada.";
                    CalculadoraInfoBar.IsOpen = true;
                    TotalHorasTextBlock.Text = "Erro";
                    return;
                }

                // Calcula a duração de cada período
                TimeSpan periodo1 = saida1.Subtract(entrada1);
                TimeSpan periodo2 = saida2.Subtract(entrada2);

                // Soma os períodos para obter o total
                TimeSpan totalHoras = periodo1.Add(periodo2);

                // Formata o resultado e exibe na tela
                TotalHorasTextBlock.Text = $"{totalHoras.Hours:D2}:{totalHoras.Minutes:D2}";
            }
            catch (Exception ex)
            {
                // Captura qualquer outro erro inesperado
                CalculadoraInfoBar.Title = "Erro Inesperado";
                CalculadoraInfoBar.Message = $"Ocorreu um erro: {ex.Message}";
                CalculadoraInfoBar.IsOpen = true;
                TotalHorasTextBlock.Text = "Erro";
            }
        }
    }
}
