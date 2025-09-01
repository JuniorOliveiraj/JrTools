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
    public sealed partial class RhProdPage : Page
    {
        public List<string> ListaDeProjetos { get; set; }
        public RhProdPage()
        {
            InitializeComponent();
            CarregarProjetos();
        }
        private void CarregarProjetos()
        {
            ListaDeProjetos = new List<string>
            {
                "producao_09.00",
                "producao_08.05",
                "producao_08.06",
                "dev-09.00.00",
                "Outro"
            };

            ProjetoComboBox.ItemsSource = ListaDeProjetos;
            ProjetoComboBox.SelectedIndex = 0;
        }

        private async void ProcessarButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationInfoBar.IsOpen = false;  

             if (ProjetoComboBox.SelectedItem?.ToString() == "Outro")
            {
                if (string.IsNullOrWhiteSpace(breachEspesifica.Text))
                {
                    ValidationInfoBar.Message = "Informe um valor para o campo 'Breach'.";
                    ValidationInfoBar.IsOpen = true;
                    return;  
                }
            }

         }

        private void ProjetoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjetoComboBox.SelectedItem?.ToString() == "Outro")
            {
                breachEspesifica.Visibility = Visibility.Visible;
            }
            else
            {
                breachEspesifica.Visibility = Visibility.Collapsed;
                breachEspesifica.Text = string.Empty;  
            }
        }
    }
}
