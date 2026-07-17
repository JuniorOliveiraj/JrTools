using JrTools.Dto;
using JrTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;

namespace JrTools.Pages
{
    public sealed partial class Copy : Page
    {
        public CopyViewModel ViewModel { get; }

        public Copy()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            ViewModel = CopyViewModel.Instance;
            this.DataContext = ViewModel;
            this.Loaded += Copy_Loaded;
        }

        private async void Copy_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.InitializeDispatcher();

            if (ViewModel.Perfis.Count == 0)
                await ViewModel.LoadProfilesAsync();

            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CopyViewModel.Logs))
                RolarLogs();

            if (e.PropertyName == nameof(CopyViewModel.IsMirroring))
            {
                if (ViewModel.IsMirroring)
                {
                    ProgressPanel.Visibility            = Visibility.Visible;
                    IniciarButton.Content               = "Parar Espelhamento";
                    IniciarButton.Background            = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    PerfisListView.IsEnabled            = false;
                    AdicionarPerfilButton.IsEnabled     = false;
                    EditarPerfilButton.IsEnabled        = false;
                    RemoverPerfilButton.IsEnabled       = false;
                }
                else
                {
                    ProgressPanel.Visibility            = Visibility.Collapsed;
                    IniciarButton.Content               = "Iniciar Espelhamento";
                    IniciarButton.Background            = null;
                    PerfisListView.IsEnabled            = true;
                    AdicionarPerfilButton.IsEnabled     = true;
                    EditarPerfilButton.IsEnabled        = true;
                    RemoverPerfilButton.IsEnabled       = true;
                }
            }
        }

        // ── Botões da lista de perfis ─────────────────────────────────────────

        private async void AdicionarPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            NomePerfilInput.Text        = string.Empty;
            OrigemInput.Text            = string.Empty;
            DestinoInput.Text           = string.Empty;
            ModoEspelharRadio.IsChecked = true;
            PerfilDialog.Title          = "Novo Perfil";

            PerfilDialog.XamlRoot = this.XamlRoot;
            var resultado = await PerfilDialog.ShowAsync();
            if (resultado != ContentDialogResult.Primary) return;

            await ViewModel.AddProfileAsync(new PerfilEspelhamento
            {
                Nome             = NomePerfilInput.Text.Trim(),
                DiretorioOrigem  = OrigemInput.Text.Trim(),
                DiretorioDestino = DestinoInput.Text.Trim(),
                Modo             = ModoSincronizarRadio.IsChecked == true
                                    ? ModoEspelhamento.Sincronizar
                                    : ModoEspelhamento.Espelhar
            });
        }

        private async void EditarPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedPerfil == null) return;

            var perfil               = ViewModel.SelectedPerfil;
            NomePerfilInput.Text     = perfil.Nome;
            OrigemInput.Text         = perfil.DiretorioOrigem;
            DestinoInput.Text        = perfil.DiretorioDestino;
            PerfilDialog.Title       = "Editar Perfil";

            if (perfil.Modo == ModoEspelhamento.Sincronizar)
                ModoSincronizarRadio.IsChecked = true;
            else
                ModoEspelharRadio.IsChecked = true;

            PerfilDialog.XamlRoot = this.XamlRoot;
            var resultado = await PerfilDialog.ShowAsync();
            if (resultado != ContentDialogResult.Primary) return;

            perfil.Nome             = NomePerfilInput.Text.Trim();
            perfil.DiretorioOrigem  = OrigemInput.Text.Trim();
            perfil.DiretorioDestino = DestinoInput.Text.Trim();
            perfil.Modo             = ModoSincronizarRadio.IsChecked == true
                                        ? ModoEspelhamento.Sincronizar
                                        : ModoEspelhamento.Espelhar;

            // Força refresh visual da lista
            var idx = ViewModel.Perfis.IndexOf(perfil);
            if (idx >= 0)
            {
                ViewModel.Perfis.RemoveAt(idx);
                ViewModel.Perfis.Insert(idx, perfil);
                ViewModel.SelectedPerfil = perfil;
            }

            await ViewModel.SaveProfilesAsync();
        }

        private async void RemoverPerfilButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedPerfil != null)
                await ViewModel.RemoveProfileAsync(ViewModel.SelectedPerfil);
        }

        // ── Seletores de pasta (dentro do ContentDialog) ─────────────────────

        private void SelecionarOrigemButton_Click(object sender, RoutedEventArgs e)
        {
            var pasta = EscolherPasta();
            if (pasta != null) OrigemInput.Text = pasta;
        }

        private void SelecionarDestinoButton_Click(object sender, RoutedEventArgs e)
        {
            var pasta = EscolherPasta();
            if (pasta != null) DestinoInput.Text = pasta;
        }

        private static string EscolherPasta()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description         = "Selecionar pasta",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };
            var resultado = dialog.ShowDialog();
            return resultado == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        // ── Scroll automático dos logs ────────────────────────────────────────

        private void RolarLogs()
        {
            var grid = (Grid)VisualTreeHelper.GetChild(LogsTextBox, 0);
            if (grid == null) return;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                if (VisualTreeHelper.GetChild(grid, i) is ScrollViewer sv)
                {
                    sv.ChangeView(0.0, sv.ScrollableHeight, 1, true);
                    break;
                }
            }
        }
    }
}
