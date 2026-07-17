using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using WinRT.Interop;

namespace JrTools
{
    public sealed partial class MainWindow : Window
    {
        // Páginas inicializadas apenas quando o usuário navegar pela primeira vez
        // AppsRh não está aqui — é cacheada pelo próprio Frame via NavigationCacheMode.Required
        private Pages.HomePage _homePage;
        private Pages.EspecificosPage _especificosPage;
        private Pages.BuildarProjeto _buildarProjeto;
        private Pages.Copy _CopyPage;
        private Pages.FecharProcessos _fecharProcessos;
        private Pages.Documentador _documentador;
        private Pages.LancarHoras _lancarHoras;
        private Pages.ConfiguracoesPage _configuracoesPage;
        private Pages.SisconDashboardPage _sisconDashboardPage;
        private Pages.SelecionadorSistemaPage _selecionadorSistemaPage;

        public MainWindow()
        {
            InitializeComponent();

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            var titleBar = appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            if (AppWindowTitleBar.IsCustomizationSupported())
                appWindow.SetIcon(@"Assets\PERFIL.ico");

            ContentFrame.Navigated += ContentFrame_Navigated;

            NavigarPara("Home");
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem item)
                NavigarPara(item.Tag as string);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = ContentFrame.CanGoBack;
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        private void NavigarPara(string tag)
        {
            // AppsRh usa Frame.Navigate para que this.Frame fique definido nas sub-páginas
            if (tag == "Rhprod")
            {
                ContentFrame.Navigate(typeof(Pages.AppsRh));
                // Navigate() empurra a página atual pro BackStack — limpa para não mostrar botão voltar
                ContentFrame.BackStack.Clear();
                return;
            }

            // Para as demais páginas, limpa qualquer stack de sub-navegação anterior
            ContentFrame.BackStack.Clear();

            Page pagina = tag switch
            {
                "Home"                => _homePage                ??= new Pages.HomePage(),
                "BuildarProjeto"      => _buildarProjeto          ??= new Pages.BuildarProjeto(),
                "CopyPage"            => _CopyPage                ??= new Pages.Copy(),
                "FecharProcessos"     => _fecharProcessos         ??= new Pages.FecharProcessos(),
                "Documentador"        => _documentador            ??= new Pages.Documentador(),
                "LancarHoras"         => _lancarHoras             ??= new Pages.LancarHoras(),
                "Siscon"              => _sisconDashboardPage     ??= new Pages.SisconDashboardPage(),
                "SelecionadorSistema" => _selecionadorSistemaPage ??= new Pages.SelecionadorSistemaPage(),
                "config"              => _configuracoesPage       ??= new Pages.ConfiguracoesPage(),
                _                     => null
            };

            if (pagina != null)
            {
                ContentFrame.Content = pagina;
                NavView.IsBackEnabled = false;
            }
        }
    }
}
