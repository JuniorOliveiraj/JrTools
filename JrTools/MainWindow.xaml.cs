using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JrTools
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private Pages.HomePage _homePage = new Pages.HomePage();
        private Pages.EspesificosPage _espesificosPage = new Pages.EspesificosPage();
        private Pages.RhProdPage _rhProdPage = new Pages.RhProdPage();
        private Pages.BuildarProjeto _buildarProjeto = new Pages.BuildarProjeto();
        private Pages.FecharProcessos _fecharProcessos = new Pages.FecharProcessos();
        private Pages.ConfiguracoesPage _configuracoesPage = new Pages.ConfiguracoesPage();

        public MainWindow()
        {
            InitializeComponent();
            ContentFrame.NavigateToType(_homePage.GetType(), null, new FrameNavigationOptions() { IsNavigationStackEnabled = false });
            ContentFrame.Content = _homePage; // força usar a instância criada
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag)
                {
                    case "Home":
                        ContentFrame.Content = _homePage;
                        break;
                    case "Espesificos":
                        ContentFrame.Content = _espesificosPage;
                        break;
                    case "Rhprod":
                        ContentFrame.Content = _rhProdPage;
                        break;
                    case "BuildarProjeto":
                        ContentFrame.Content = _buildarProjeto;
                        break;
                    case "FecharProcessos":
                        ContentFrame.Content = _fecharProcessos;
                        break;
                    case "config":
                        ContentFrame.Content = _configuracoesPage;
                        break;
                }
            }
        }
    }
}
