using JrTools.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace JrTools.Pages
{
    public sealed partial class ViewPathExplorerPage : Page
    {
        public ViewPathExplorerViewModel ViewModel { get; }

        public ViewPathExplorerPage()
        {
            this.InitializeComponent();
            ViewModel = new ViewPathExplorerViewModel();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.InitializeDispatcher();
            await ViewModel.InicializarAsync();
        }
    }
}
