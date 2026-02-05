using JrTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using System;

namespace JrTools.Pages
{
    public sealed partial class FecharProcessos : Page
    {
        public FecharProcessosViewModel ViewModel { get; }

        public FecharProcessos()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            
            ViewModel = FecharProcessosViewModel.Instance;
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.InitializeDispatcher();
            
            // Re-bind items source to ensure dynamic UI shows up
            ProcessosItemsControl.ItemsSource = ViewModel.MonitoredProcesses;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // CRITICAL: We NO LONGER cancel anything here. 
            // The Singleton ViewModel keeps the background work alive.
        }




        private void ProcessosDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Detalhes extras podem ser implementados via ViewModel
        }

        private void MatarProcessoButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
             _ = ViewModel.KillAllNowAsync();
        }
    }

    public class ProcessCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? MasterTemplate { get; set; }
        public DataTemplate? ProcessTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ProcessViewModel vm)
            {
                return vm.Name == "MASTER_CONTROL" ? MasterTemplate : ProcessTemplate;
            }
            return base.SelectTemplateCore(item, container);
        }
    }
}
