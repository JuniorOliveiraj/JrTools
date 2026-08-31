using JrTools.Dto;
using JrTools.Flows;
using JrTools.Services.Db;
using JrTools.Utils;
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
        private HoraLancamento? _lancamentoSelecionado;

        public LancarHoras()
        {
            InitializeComponent();
            LancamentosListView.ItemsSource = Lancamentos;
            ProjetoComboBox.ItemsSource = Projetos;
            Loaded += LancarHoras_Loaded;
            CamposHoraMinutoHelper.AplicarFormatacaoDoisDigitos(
                HoraInicioHoraBox, HoraInicioMinutoBox,
                HoraFimHoraBox, HoraFimMinutoBox);
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

            await CarregarLancamentosAsync();
            LimparFormulario();
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
                    var nomeProjeto = novoProjetoTextBox.Text;
                    await HorasToggle.AdicionarProjetoAsync(nomeProjeto, Projetos);
                    if (!Projetos.Contains(nomeProjeto))
                        Projetos.Add(nomeProjeto);
                    ProjetoComboBox.SelectedItem = nomeProjeto;
                }
                catch (Exception ex)
                {
                    ShowValidationError(ex.Message);
                }
            }
        }

        private async void SalvarLancamentoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_horasService == null) return;

            SalvandoRing.IsActive = true;
            SaveButton.IsEnabled = false;
            ClearButton.IsEnabled = false;

            try
            {
                if (string.IsNullOrEmpty(DescricaoBox.Text) && (ProjetoComboBox.SelectedItem == null || ProjetoComboBox.SelectedItem.ToString() == "Nenhum"))
                {
                    ShowValidationError("A Descrição ou o projeto devem ser preenchidos");
                    return;
                }

                string? projetoSelecionado = (ProjetoComboBox.SelectedItem?.ToString() == "Nenhum") ? string.Empty : ProjetoComboBox.SelectedItem?.ToString();
                string descricaoBase = _horasService.GerarDescricaoFinal(DescricaoBox.Text, projetoSelecionado);

                // Lê os horários informados
                TimeSpan? horaInicio = CamposHoraMinutoHelper.ObterOuNull(HoraInicioHoraBox, HoraInicioMinutoBox);
                TimeSpan? horaFim = CamposHoraMinutoHelper.ObterOuNull(HoraFimHoraBox, HoraFimMinutoBox);

                // Se não há duração, mas há início e fim, calcula a duração automaticamente
                if (TotalHorasBox.Value <= 0 && horaInicio.HasValue && horaFim.HasValue)
                {
                    var diff = horaFim.Value - horaInicio.Value;
                    if (diff <= TimeSpan.Zero)
                    {
                        ShowValidationError("O horário de fim deve ser maior que o de início.");
                        return;
                    }

                    TotalHorasBox.Value = diff.TotalHours;
                }

                if (TotalHorasBox.Value <= 0)
                {
                    ShowValidationError("A duração deve ser maior que zero.");
                    return;
                }

                // Caso o usuário informe somente as horas (ex: 4h) sem início/fim,
                // define o início após o último lançamento do dia (ou 08:00 se não houver nenhum)
                if (!horaInicio.HasValue && !horaFim.HasValue && TotalHorasBox.Value > 0)
                {
                    horaInicio = _horasService.SugerirProximoHorarioInicio(Lancamentos);
                    horaFim = horaInicio.Value.Add(TimeSpan.FromHours(TotalHorasBox.Value));

                    // Reflete o padrão também na interface
                    CamposHoraMinutoHelper.Definir(HoraInicioHoraBox, HoraInicioMinutoBox, horaInicio);
                    CamposHoraMinutoHelper.Definir(HoraFimHoraBox, HoraFimMinutoBox, horaFim);
                }
                // Caso haja início e apenas a duração, garante o cálculo do fim
                else if (horaInicio.HasValue && !horaFim.HasValue && TotalHorasBox.Value > 0)
                {
                    horaFim = horaInicio.Value.Add(TimeSpan.FromHours(TotalHorasBox.Value));
                    CamposHoraMinutoHelper.Definir(HoraFimHoraBox, HoraFimMinutoBox, horaFim);
                }

                var lancamento = new HoraLancamento
                {
                    Id = _lancamentoSelecionado?.Id ?? 0,
                    Data = DiaLancamento.Date?.Date,
                    HoraInicio = horaInicio,
                    HoraFim = horaFim,
                    TotalHoras = TotalHorasBox.Value,
                    Descricao = descricaoBase,
                    Projeto = projetoSelecionado
                };

                if (_lancamentoSelecionado == null)
                    await _horasService.SalvarLancamentoAsync(lancamento);
                else
                    await _horasService.AtualizarLancamentoAsync(lancamento);

                await CarregarLancamentosAsync();
                LimparFormulario();
            }
            catch (Exception ex)
            {
                ShowValidationError(ex.Message);
            }
            finally
            {
                SalvandoRing.IsActive = false;
                SaveButton.IsEnabled = true;
                ClearButton.IsEnabled = true;
            }
        }

        private async Task CarregarLancamentosAsync()
        {
            try
            {
                if (_horasService == null) return;
                var data = DiaLancamento.Date?.Date ?? DateTime.Today;
                var lancamentos = await _horasService.CarregarLancamentosDoDiaAsync(data);

                Lancamentos.Clear();
                foreach (var l in lancamentos.OrderBy(l => l.HoraInicio))
                    Lancamentos.Add(l);
            }
            catch (Exception ex)
            {
                ShowValidationError($"Erro ao carregar lançamentos: {ex.Message}");
            }
        }

        private void ShowValidationError(string message)
        {
            ValidationInfoBar.Title = "Erro de Validação";
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen = true;
        }

        private async void DiaLancamento_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            await CarregarLancamentosAsync();
            LimparFormulario();
        }

        private void LimparFormulario()
        {
            _lancamentoSelecionado = null;
            LancamentosListView.SelectedItem = null;
            FormTitle.Text = "Lançamento de Horas";

            CamposHoraMinutoHelper.Definir(HoraInicioHoraBox, HoraInicioMinutoBox, null);
            CamposHoraMinutoHelper.Definir(HoraFimHoraBox, HoraFimMinutoBox, null);
            TotalHorasBox.Value = 0;
            DescricaoBox.Text = string.Empty;
            ProjetoComboBox.SelectedItem = Projetos.FirstOrDefault(p => p == "Nenhum");
            ValidationInfoBar.IsOpen = false;

            SaveButton.Content = "Salvar Lançamento";
            ClearButton.Visibility = Visibility.Collapsed;
        }

        private void PreencherFormularioComLancamento(HoraLancamento lancamento)
        {
            FormTitle.Text = "Editar Lançamento";
            CamposHoraMinutoHelper.Definir(HoraInicioHoraBox, HoraInicioMinutoBox, lancamento.HoraInicio);
            CamposHoraMinutoHelper.Definir(HoraFimHoraBox, HoraFimMinutoBox, lancamento.HoraFim);
            TotalHorasBox.Value = lancamento.TotalHoras ?? 0;
            DescricaoBox.Text = lancamento.Descricao;
            ProjetoComboBox.SelectedItem = lancamento.Projeto ?? Projetos.FirstOrDefault(p => p == "Nenhum");

            SaveButton.Content = "Salvar Alterações";
            ClearButton.Visibility = Visibility.Visible;
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_horasService == null) return;

            var button = (Button)sender;
            if (button.Tag is HoraLancamento lancamento)
            {
                await _horasService.DeleteLancamentoAsync(lancamento);
                await CarregarLancamentosAsync();
                LimparFormulario(); 
            }
        }

        private void LancamentosListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _lancamentoSelecionado = LancamentosListView.SelectedItem as HoraLancamento;
            if (_lancamentoSelecionado != null)
            {
                PreencherFormularioComLancamento(_lancamentoSelecionado);
            }
        }

        private void TotalHorasBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var horaInicio = CamposHoraMinutoHelper.ObterOuNull(HoraInicioHoraBox, HoraInicioMinutoBox);
            if (horaInicio.HasValue && TotalHorasBox.Value > 0)
            {
                var horaFim = horaInicio.Value.Add(TimeSpan.FromHours(TotalHorasBox.Value));
                CamposHoraMinutoHelper.Definir(HoraFimHoraBox, HoraFimMinutoBox, horaFim);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LimparFormulario();
        }
    }
}
