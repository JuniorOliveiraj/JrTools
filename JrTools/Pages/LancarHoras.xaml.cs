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
        private HoraLancamento? _lancamentoSelecionado;

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
            try
            {
                if (_horasService == null) return;

                if (string.IsNullOrEmpty(DescricaoBox.Text) && (ProjetoComboBox.SelectedItem == null || ProjetoComboBox.SelectedItem.ToString() == "Nenhum"))
                {
                    ShowValidationError("A Descrição ou o projeto devem ser preenchidos");
                    return;
                }

                string? projetoSelecionado = (ProjetoComboBox.SelectedItem?.ToString() == "Nenhum") ? string.Empty : ProjetoComboBox.SelectedItem?.ToString();
                string descricaoBase = _horasService.GerarDescricaoFinal(DescricaoBox.Text, projetoSelecionado);

                // Lê os horários informados
                TimeSpan? horaInicio = HoraInicioPicker.SelectedTime;
                TimeSpan? horaFim = HoraFimPicker.SelectedTime;

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
                    // Início padrão
                    TimeSpan inicioPadrao = TimeSpan.FromHours(8);

                    // Busca o último lançamento do dia atual, se existir
                    var ultimoLancamento = Lancamentos
                        .Where(l => l.HoraInicio.HasValue || l.HoraFim.HasValue)
                        .OrderBy(l =>
                        {
                            // Define um "fim" calculado para ordenar corretamente
                            if (l.HoraFim.HasValue)
                                return l.HoraFim.Value;
                            if (l.HoraInicio.HasValue && l.TotalHoras.HasValue)
                                return l.HoraInicio.Value + TimeSpan.FromHours(l.TotalHoras.Value);
                            return l.HoraInicio ?? TimeSpan.Zero;
                        })
                        .LastOrDefault();

                    if (ultimoLancamento != null)
                    {
                        if (ultimoLancamento.HoraFim.HasValue)
                            inicioPadrao = ultimoLancamento.HoraFim.Value;
                        else if (ultimoLancamento.HoraInicio.HasValue && ultimoLancamento.TotalHoras.HasValue)
                            inicioPadrao = ultimoLancamento.HoraInicio.Value + TimeSpan.FromHours(ultimoLancamento.TotalHoras.Value);
                    }

                    horaInicio = inicioPadrao;
                    horaFim = horaInicio.Value.Add(TimeSpan.FromHours(TotalHorasBox.Value));

                    // Reflete o padrão também na interface
                    HoraInicioPicker.SelectedTime = horaInicio;
                    HoraFimPicker.SelectedTime = horaFim;
                }
                // Caso haja início e apenas a duração, garante o cálculo do fim
                else if (horaInicio.HasValue && !horaFim.HasValue && TotalHorasBox.Value > 0)
                {
                    horaFim = horaInicio.Value.Add(TimeSpan.FromHours(TotalHorasBox.Value));
                    HoraFimPicker.SelectedTime = horaFim;
                }

                var lancamento = new HoraLancamento
                {
                    Id = _lancamentoSelecionado?.Id ?? 0,
                    Data = DiaLancamento.Date.Date,
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
        }

        private async Task CarregarLancamentosAsync()
        {
            try
            {
                if (_horasService == null) return;
                var lancamentos = await _horasService.CarregarLancamentosDoDiaAsync(DiaLancamento.Date.Date);

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

        private async void DiaLancamento_Changed(DatePicker sender, DatePickerSelectedValueChangedEventArgs e)
        {
            await CarregarLancamentosAsync();
            LimparFormulario();
        }

        private void LimparFormulario()
        {
            _lancamentoSelecionado = null;
            LancamentosListView.SelectedItem = null;
            FormTitle.Text = "Lançamento de Horas";

            HoraInicioPicker.SelectedTime = null;
            HoraFimPicker.SelectedTime = null;
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
            HoraInicioPicker.SelectedTime = lancamento.HoraInicio;
            HoraFimPicker.SelectedTime = lancamento.HoraFim;
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
            if (HoraInicioPicker.SelectedTime.HasValue && TotalHorasBox.Value > 0)
            {
                HoraFimPicker.SelectedTime = HoraInicioPicker.SelectedTime.Value.Add(TimeSpan.FromHours(TotalHorasBox.Value));
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LimparFormulario();
        }
    }
}
