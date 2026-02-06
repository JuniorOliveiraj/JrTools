using JrTools.Dto;
using JrTools.Flows;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class LancarHoras : Page
    {
        private readonly ObservableCollection<HoraLancamento> Lancamentos = new();
        private readonly ObservableCollection<string> Projetos = new();
        private HorasToggle? _horasService;

        public LancarHoras()
        {
            InitializeComponent();
            LancamentosListView.ItemsSource = Lancamentos;
            ProjetoComboBox.ItemsSource = Projetos;
            Loaded += LancarHoras_Loaded;
        }

        private async void LancarHoras_Loaded(object sender, RoutedEventArgs e)
        {
            DiaLancamento.Date = DateTimeOffset.Now;
            await InicializarAsync();
        }

        private async Task InicializarAsync()
        {
            Projetos.Clear();
            var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();
            _horasService = new HorasToggle(perfil.ApiToggl);

            var projetos = await HorasToggle.CarregarProjetosAsync();
            foreach (var projeto in projetos)
                Projetos.Add(projeto);

            ProjetoComboBox.SelectedItem = "Nenhum";
            await CarregarLancamentosAsync();
        }

        private async void AddProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var novoProjetoTextBox = new TextBox { PlaceholderText = "Ex: @MeuProjeto" };
            var dialog = new ContentDialog
            {
                Title = "Adicionar Novo Projeto",
                Content = novoProjetoTextBox,
                PrimaryButtonText = "Adicionar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    await HorasToggle.AdicionarProjetoAsync(novoProjetoTextBox.Text, Projetos);
                    Projetos.Add(novoProjetoTextBox.Text);
                    ProjetoComboBox.SelectedItem = novoProjetoTextBox.Text;
                }
                catch (Exception ex)
                {
                    ShowValidationError(ex.Message);
                }
            }
        }

        private async void SalvarLancamentoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_horasService == null) return;

                if (string.IsNullOrEmpty(DescricaoBox.Text) && ProjetoComboBox.SelectedItem?.ToString() == "Nenhum")
                {
                    ShowValidationError("A Descrição ou o projeto devem ser preenchidos");
                    return;
                }

                if (TotalHorasBox.Value <= 0)
                {
                    ShowValidationError("A duração deve ser maior que zero.");
                    return;
                }

                string? projetoSelecionado = ProjetoComboBox.SelectedItem?.ToString() == "Nenhum"
                    ? string.Empty
                    : ProjetoComboBox.SelectedItem?.ToString();

                string descricaoBase = _horasService.GerarDescricaoFinal(DescricaoBox.Text, projetoSelecionado);
                TimeSpan duracaoTotal = TimeSpan.FromHours(TotalHorasBox.Value);

                TimeSpan? horaInicio = null;

                if (HoraInicioPicker.SelectedTime.HasValue)
                {
                    horaInicio = HoraInicioPicker.SelectedTime.Value;
                }
                else
                {
                    var quatroHorasHoje = Lancamentos
                        .Where(l => l.TotalHoras.HasValue && Math.Abs(Convert.ToDouble(l.TotalHoras.Value) - 4.0) < 0.01)
                        .OrderBy(l => l.HoraInicio)
                        .ToList();
                    if (duracaoTotal.TotalHours == 4 && quatroHorasHoje.Count == 1)
                    {
                        horaInicio = TimeSpan.FromHours(14);
                    }
                    else if (Lancamentos.Any())
                    {
                        horaInicio = Lancamentos.Max(l => l.HoraFim ?? TimeSpan.FromHours(8));
                        if (horaInicio < TimeSpan.FromHours(8))
                        {
                            horaInicio = TimeSpan.FromHours(8);
                        }
                    }
                    else
                    {
                        horaInicio = TimeSpan.FromHours(8);
                    }
                }

                // Garante nunca criar lançamento antes das 8h se usuário não definiu horário
                if (!HoraInicioPicker.SelectedTime.HasValue && horaInicio < TimeSpan.FromHours(8))
                {
                    horaInicio = TimeSpan.FromHours(8);
                }

                var novoLancamento = new HoraLancamento
                {
                    Data = DiaLancamento.Date.Date,
                    HoraInicio = horaInicio.Value,
                    HoraFim = horaInicio.Value + duracaoTotal,
                    TotalHoras = duracaoTotal.TotalHours,
                    Descricao = descricaoBase,
                    Projeto = projetoSelecionado
                };
                await _horasService.SalvarLancamentoAsync(novoLancamento);

                await CarregarLancamentosAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                ShowValidationError(ex.Message);
            }
        }

        private async Task CarregarLancamentosAsync()
        {
            try
            {
                if (_horasService == null) return;
                var lancamentos = await _horasService.CarregarLancamentosDoDiaAsync(DiaLancamento.Date.Date);

                Lancamentos.Clear();
                foreach (var l in lancamentos)
                    Lancamentos.Add(l);
            }
            catch (Exception ex)
            {
                ShowValidationError($"Erro ao carregar lançamentos: {ex.Message}");
            }
        }

        private void HoraPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            if (HoraInicioPicker.SelectedTime.HasValue && HoraFimPicker.SelectedTime.HasValue)
            {
                var inicio = HoraInicioPicker.SelectedTime.Value;
                var fim = HoraFimPicker.SelectedTime.Value;
                TotalHorasBox.Value = fim >= inicio ? (fim - inicio).TotalHours : 0;
            }
        }

        private void ShowValidationError(string message)
        {
            ValidationInfoBar.Title = "Erro de Validação";
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen = true;
        }
        private async void DiaLancamento_Changed(DatePicker sender, DatePickerSelectedValueChangedEventArgs e)
        {
            await CarregarLancamentosAsync();
        }
        private void ClearForm()
        {
            HoraInicioPicker.SelectedTime = null;
            HoraFimPicker.SelectedTime = null;
            TotalHorasBox.Value = 0;
            DescricaoBox.Text = string.Empty;
            ProjetoComboBox.SelectedIndex = -1;
            ValidationInfoBar.IsOpen = false;
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_horasService == null) return;

            var button = (Button)sender;
            var lancamento = (HoraLancamento)button.Tag;

            await _horasService.DeleteLancamentoAsync(lancamento);
            await CarregarLancamentosAsync();
        }
    }
}