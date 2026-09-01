using JrTools.Dto;
using JrTools.Models;
using JrTools.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;

namespace JrTools.Pages
{
    public sealed partial class FecharProcessos : Page
    {
        public FecharProcessosViewModel ViewModel { get; }
        private List<ProcessoDisponivel> _todosProcessosDisponiveis = new();

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
            ProcessosItemsControl.ItemsSource = ViewModel.MonitoredProcesses;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Singleton ViewModel mantém o trabalho em background vivo
        }

        private void ProcessosDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = ProcessosDataGrid.SelectedItem as ProcessInfo;
            ViewModel.SelectedProvider = selected;
            MatarProcessoButton.IsEnabled = selected != null;

            // Scroll automático do log ao selecionar
            if (selected != null)
                ViewModel.SelectedProviderLog = "";
        }

        private void LogTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || LogTypeCombo.SelectedIndex < 0) return;
            ViewModel.SelectedLogType = (ProviderLogType)LogTypeCombo.SelectedIndex;
        }

        private void FecharBannerDeploy_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DeployRecovery = null;
        }

        private async void ReiniciarPoolButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RestartPoolManualAsync();
        }

        private void MatarProcessoButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedProvider != null)
                _ = ViewModel.KillProviderAsync(ViewModel.SelectedProvider.PID);
        }

        private void KillNowButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ProcessViewModel vm)
                _ = ViewModel.KillProcessNowAsync(vm.Name);
        }

        private void RemoveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is ProcessViewModel vm)
                _ = ViewModel.RemoveCustomProcessAsync(vm.Name);
        }

        private async void AdicionarProcessoButton_Click(object sender, RoutedEventArgs e)
        {
            AdicionarProcessoDialog.XamlRoot = XamlRoot;
            TxtBuscaProcesso.Text = string.Empty;
            LoadingProcessosDisponiveis.IsActive = true;
            ListProcessosDisponiveis.ItemsSource = null;

            try
            {
                var todos = await ViewModel.GetProcessosDisponiveisAsync();
                var jaMonitorados = ViewModel.MonitoredProcesses
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                _todosProcessosDisponiveis = todos
                    .Where(p => !jaMonitorados.Contains(p.Nome))
                    .ToList();

                ListProcessosDisponiveis.ItemsSource = _todosProcessosDisponiveis;
            }
            finally
            {
                LoadingProcessosDisponiveis.IsActive = false;
            }

            await AdicionarProcessoDialog.ShowAsync();
        }

        private void TxtBuscaProcesso_TextChanged(object sender, TextChangedEventArgs e)
        {
            var termo = TxtBuscaProcesso.Text;
            ListProcessosDisponiveis.ItemsSource = string.IsNullOrWhiteSpace(termo)
                ? _todosProcessosDisponiveis
                : _todosProcessosDisponiveis.Where(p => p.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void ListProcessosDisponiveis_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListProcessosDisponiveis.SelectedItem is ProcessoDisponivel selecionado)
            {
                _ = ViewModel.AddCustomProcessAsync(selecionado.Nome);
                AdicionarProcessoDialog.Hide();
            }
        }

        private async void ExportarLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.SuggestedFileName = $"ProviderLog_{ViewModel.SelectedProvider?.ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}";
            picker.FileTypeChoices.Add("Arquivo de texto", new[] { ".txt" });

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            await File.WriteAllTextAsync(file.Path, ViewModel.SelectedProviderLog);
        }

        private void ProviderLogOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ProviderLogOutput.SelectionStart = ProviderLogOutput.Text.Length;
            ProviderLogOutput.SelectionLength = 0;
        }
    }

    public class ProcessCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? MasterTemplate { get; set; }
        public DataTemplate? ProcessTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ProcessViewModel vm)
                return vm.Name == "MASTER_CONTROL" ? MasterTemplate : ProcessTemplate;
            return base.SelectTemplateCore(item, container);
        }
    }
}
