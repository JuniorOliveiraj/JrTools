using JrTools.Dto;
using JrTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Linq;

namespace JrTools.Pages
{
    public sealed partial class Copy : Page
    {
        public CopyViewModel ViewModel { get; }

        public Copy()
        {
            this.InitializeComponent();
            
            // CRITICAL: Prevent page from being destroyed on navigation
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            
            ViewModel = CopyViewModel.Instance;
            this.DataContext = ViewModel;
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // Initialize dispatcher for current UI thread
            ViewModel.InitializeDispatcher();

            // Load profiles only if empty
            if (ViewModel.Perfis.Count == 0)
            {
                _ = ViewModel.LoadProfilesAsync();
            }

            // Subscribe to property changes
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged; // Remove first to avoid duplicates
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            
            // Don't unsubscribe - we want to keep receiving updates even when not visible
            // The ViewModel is a singleton and should continue working
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CopyViewModel.Logs))
            {
                RolarLogs();
            }
            // Control visibility based on Mirroring state
            if (e.PropertyName == nameof(CopyViewModel.IsMirroring))
            {
                if (ViewModel.IsMirroring)
                {
                    ProgressPanel.Visibility = Visibility.Visible;
                    
                    // Visual changes for Active State
                    IniciarButton.Content = "⏹ Parar Espelhamento";
                    IniciarButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Red);

                    // Disable interactions
                    PerfisListView.IsEnabled = false;
                    AdicionarPerfilButton.IsEnabled = false;
                    RemoverPerfilButton.IsEnabled = false;
                }
                else
                {
                    // Visual changes for Idle State
                    IniciarButton.Content = "▶ Iniciar Espelhamento";
                    IniciarButton.Background = (SolidColorBrush)Application.Current.Resources["ButtonBackground"];

                    // Enable interactions
                    PerfisListView.IsEnabled = true;
                    AdicionarPerfilButton.IsEnabled = true;
                    RemoverPerfilButton.IsEnabled = true;
                }
            }
        }

        private async void AdicionarPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            NomePerfilInput.Text = "";
            OrigemInput.Text = "";
            DestinoInput.Text = "";

            var result = await NovoPerfilDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var novoPerfil = new PerfilEspelhamento
                {
                    Nome = NomePerfilInput.Text,
                    DiretorioOrigem = OrigemInput.Text,
                    DiretorioDestino = DestinoInput.Text
                };

                await ViewModel.AddProfileAsync(novoPerfil);
            }
        }
        
        private async void RemoverPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            // This is still needed because the confirmation dialog or logic 
            // might live here, though the Command is bound in XAML, 
            // the button Click event is ALSO set in XAML, which is redundant.
            // I will remove the Click handler in XAML in the next step to be cleaner
            // OR keep it here if I want to show a Dialog before removing.
            // For now, let's just let the Command handle it directly in ViewModel if possible,
            // but the ViewModel doesn't show dialogs. 
            // So I will keep this Click handler for now, but call the VM method.
            
            if (ViewModel.SelectedPerfil != null)
            {
                 // Confirm dialog could go here
                 await ViewModel.RemoveProfileAsync(ViewModel.SelectedPerfil);
            }
        }

        private void RolarLogs()
        {
             var grid = (Grid)VisualTreeHelper.GetChild(LogsTextBox, 0);
             if (grid != null && VisualTreeHelper.GetChildrenCount(grid) > 0)
             {
                 for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
                 {
                     if (VisualTreeHelper.GetChild(grid, i) is ScrollViewer viewer)
                     {
                         viewer.ChangeView(0.0, viewer.ScrollableHeight, 1, true);
                         break;
                     }
                 }
             }
        }
    }
}