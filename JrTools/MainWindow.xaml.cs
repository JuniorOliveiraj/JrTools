using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using WinRT.Interop;

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
        private Pages.Documentador _documentador = new Pages.Documentador();
        private Pages.LancarHoras _lancarHoras = new Pages.LancarHoras();
        private Pages.ConfiguracoesPage _configuracoesPage = new Pages.ConfiguracoesPage();

        public MainWindow()
        {
            InitializeComponent();
            ContentFrame.NavigateToType(_homePage.GetType(), null, new FrameNavigationOptions() { IsNavigationStackEnabled = false });
            ContentFrame.Content = _homePage; // força usar a instância criada


            // Obtenha o HWND da janela
            IntPtr hWnd = WindowNative.GetWindowHandle(this);

            // Obtenha o WindowId a partir do HWND
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);

            // Agora pegue o AppWindow de forma correta
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            // Personalize a barra de título
            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Defina o CustomTitleBar (deve estar no XAML)
            //this.SetTitleBar(CustomTitleBar);


            if (AppWindowTitleBar.IsCustomizationSupported() is true)
            {
                appWindow.SetIcon(@"Assets\PERFIL.ico");
            }
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
                    case "Documentador":
                        ContentFrame.Content = _documentador;
                        break;
                    case "LancarHoras":
                        ContentFrame.Content = _lancarHoras;
                        break;
                    case "config":
                        ContentFrame.Content = _configuracoesPage;
                        break;
                }
            }
        }
    }
}
