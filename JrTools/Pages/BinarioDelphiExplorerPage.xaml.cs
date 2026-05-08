using JrTools.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace JrTools.Pages
{
    public sealed partial class BinarioDelphiExplorerPage : Page
    {
        public BinarioDelphiExplorerViewModel ViewModel { get; } = new BinarioDelphiExplorerViewModel();

        public BinarioDelphiExplorerPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.DataContext = ViewModel;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.InitializeDispatcher();
            await ViewModel.InicializarAsync();

            // Populate the extension ComboBox after the index is built
            PopularExtensaoComboBox();
        }

        private void PopularExtensaoComboBox()
        {
            var selectedExt = ViewModel.ExtensaoFiltro;

            ExtensaoComboBox.SelectionChanged -= ExtensaoComboBox_SelectionChanged;
            ExtensaoComboBox.Items.Clear();
            ExtensaoComboBox.Items.Add("Todas");

            foreach (var ext in ViewModel.ExtensoesDisponiveis)
                ExtensaoComboBox.Items.Add(ext);

            // Restore selection
            if (selectedExt != null && ViewModel.ExtensoesDisponiveis.Contains(selectedExt))
                ExtensaoComboBox.SelectedItem = selectedExt;
            else
                ExtensaoComboBox.SelectedIndex = 0;

            ExtensaoComboBox.SelectionChanged += ExtensaoComboBox_SelectionChanged;
        }

        private void ExtensaoComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ExtensaoComboBox.SelectedItem as string;
            ViewModel.ExtensaoFiltro = selected == "Todas" ? null : selected;
        }
    }
}
