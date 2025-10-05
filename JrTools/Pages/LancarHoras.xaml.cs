using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Pages
{
    public sealed partial class LancarHoras : Page
    {
        private readonly ObservableCollection<HoraLancamento> Lancamentos = new();
        private readonly ObservableCollection<string> Projetos = new();

        public LancarHoras()
        {
            InitializeComponent();

            LancamentosListView.ItemsSource = Lancamentos;
            ProjetoComboBox.ItemsSource = Projetos;

            Loaded += LancarHoras_Loaded;
        }

        private async void LancarHoras_Loaded(object sender, RoutedEventArgs e)
        {
            Projetos.Clear();
            Projetos.Add("Nenhum");

            var projetosSalvos = await ProjetosHelper.LerProjetosAsync();
            foreach (var projeto in projetosSalvos)
            {
                if (!Projetos.Contains(projeto))
                    Projetos.Add(projeto);
            }
            ProjetoComboBox.SelectedItem = "Nenhum";

            await CarregarLancamentosDoDiaAsync();
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

            dialog.PrimaryButtonClick += (s, args) =>
            {
                var nomeProjeto = novoProjetoTextBox.Text;
                if (string.IsNullOrWhiteSpace(nomeProjeto) || !nomeProjeto.StartsWith("@") || nomeProjeto.Contains(' '))
                {
                    args.Cancel = true;
                    novoProjetoTextBox.Text = "";
                    novoProjetoTextBox.PlaceholderText = "Inválido! Use o formato @ProjetoSemEspaco";
                }
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var nomeProjeto = novoProjetoTextBox.Text;
                if (!Projetos.Contains(nomeProjeto))
                {
                    Projetos.Add(nomeProjeto);
                    await ProjetosHelper.SalvarProjetosAsync(Projetos.Where(p => p != "Nenhum"));
                }
                ProjetoComboBox.SelectedItem = nomeProjeto;
            }
        }

        private async void SalvarLancamentoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
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
                string? projetoSelecionado = ProjetoComboBox.SelectedItem?.ToString() == "Nenhum" ? string.Empty : ProjetoComboBox.SelectedItem?.ToString();
                string descricaoFinal = GerarDescricaoFinal(DescricaoBox.Text, projetoSelecionado);

                TimeSpan? horaInicio = HoraInicioPicker.SelectedTime;
                TimeSpan duracao = TimeSpan.FromHours(TotalHorasBox.Value);


                horaInicio = ObterHoraInicioDisponivel(horaInicio, duracao);

                var novoLancamento = new HoraLancamento
                {
                    Data = DateTime.Today,
                    HoraInicio = horaInicio.Value,
                    HoraFim = horaInicio.Value + duracao,
                    TotalHoras = duracao.TotalHours,
                    Descricao = descricaoFinal,
                    Projeto = projetoSelecionado
                };

                await SalvarToggle(novoLancamento);
            }
            catch (InvalidOperationException ex)
            {
                ShowValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowValidationError($"Erro ao salvar lançamento: {ex.Message}");
            }
        }

        private TimeSpan ObterHoraInicioDisponivel(TimeSpan? horaInicio, TimeSpan duracao)
        {

            TimeSpan inicioAlmoco = TimeSpan.FromHours(12);
            TimeSpan fimAlmoco = TimeSpan.FromHours(13);

            TimeSpan inicio = horaInicio ?? (Lancamentos.Any() ? Lancamentos.Max(l => l.HoraFim ?? l.HoraInicio.Value) : TimeSpan.FromHours(8));

            TimeSpan fim = inicio + duracao;

            while (Lancamentos.Any(l => (l.HoraFim ?? l.HoraInicio) > inicio && l.HoraInicio < fim)
                   || (inicio < fimAlmoco && fim > inicioAlmoco)) // considera o horário de almoço
            {
                var conflitos = Lancamentos.Where(l => (l.HoraFim ?? l.HoraInicio) > inicio && l.HoraInicio < fim).ToList();

                if (conflitos.Any())
                {
                    inicio = conflitos.Max(l => l.HoraFim ?? l.HoraInicio.Value);
                }
                else if (inicio < fimAlmoco && fim > inicioAlmoco)
                {
                    inicio = fimAlmoco; 
                }

                fim = inicio + duracao;
            }

            return inicio;
        }


        private async Task SalvarToggle(HoraLancamento lancamento)
        {
            var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();
            string token = perfil.ApiToggl;
            if (string.IsNullOrWhiteSpace(token))
            {
                ShowValidationError("Token do Toggl não configurado. Vá para Configurações e adicione seu token.");
                return;
            }

            var toggl = new TogglClient(token);
            var me = await toggl.GetMeAsync();
            long workspaceId = me.GetProperty("default_workspace_id").GetInt64();

            var result = await toggl.CreateTimeEntryAsync(workspaceId, lancamento);

            Console.WriteLine($"🟢 Lançamento criado: {result.GetProperty("id").GetInt64()}");

            await CarregarLancamentosDoDiaAsync();
            ClearForm();
        }

        private string GerarDescricaoFinal(string? descricaoInput, string? projeto)
        {
            descricaoInput = descricaoInput?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(descricaoInput) && string.IsNullOrWhiteSpace(projeto))
                throw new InvalidOperationException("Descrição ou projeto é obrigatória.");

            string descricaoFinal = descricaoInput;
            var regexSMS = new System.Text.RegularExpressions.Regex(@"^(SMS-(\d+)|(\d+))\s*-?");
            var match = regexSMS.Match(descricaoInput);

            if (match.Success && match.Groups[3].Success && !match.Groups[1].Value.StartsWith("SMS-"))
                descricaoFinal = "SMS-" + match.Groups[3].Value + descricaoInput.Substring(match.Length);

            if (!string.IsNullOrEmpty(projeto))
                descricaoFinal = projeto + " " + descricaoFinal.Trim();
            else if (!match.Success)
                throw new InvalidOperationException("Descrição inválida. Deve começar com SMS-XXXXXX ou número da SMS.");

            return descricaoFinal.Trim();
        }

        private void HoraPicker_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            if (HoraInicioPicker.SelectedTime.HasValue && HoraFimPicker.SelectedTime.HasValue)
            {
                var inicio = HoraInicioPicker.SelectedTime.Value;
                var fim = HoraFimPicker.SelectedTime.Value;

                if (fim >= inicio)
                    TotalHorasBox.Value = (fim - inicio).TotalHours;
                else
                    TotalHorasBox.Value = 0;
            }
        }

        private void ShowValidationError(string message)
        {
            ValidationInfoBar.Title = "Erro de Validação";
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen = true;
        }

        private async Task CarregarLancamentosDoDiaAsync()
        {
            try
            {
                var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();
                string token = perfil.ApiToggl;
                if (string.IsNullOrWhiteSpace(token)) return;

                var toggl = new TogglClient(token);
                var entries = await toggl.GetTodayTimeEntriesAsync();

                Lancamentos.Clear();

                foreach (var entry in entries.EnumerateArray())
                {
                    var startUtc = entry.GetProperty("start").GetDateTime();
                    DateTime localStart = startUtc.ToLocalTime();

                    DateTime? localEnd = null;
                    if (entry.TryGetProperty("stop", out var stopProp) && stopProp.ValueKind != JsonValueKind.Null)
                        localEnd = stopProp.GetDateTime().ToLocalTime();

                    double totalHoras = 0;
                    if (entry.TryGetProperty("duration", out var durProp))
                    {
                        var dur = durProp.GetDouble();
                        if (dur > 0) totalHoras = dur / 3600.0;
                    }

                    Lancamentos.Add(new HoraLancamento
                    {
                        HoraInicio = localStart.TimeOfDay,
                        HoraFim = localEnd?.TimeOfDay,
                        TotalHoras = totalHoras,
                        Descricao = entry.GetProperty("description").GetString(),
                        Projeto = entry.TryGetProperty("project_id", out var projProp) && !projProp.ValueKind.Equals(JsonValueKind.Null)
                                    ? projProp.GetInt64().ToString()
                                    : null
                    });
                }
            }
            catch (Exception ex)
            {
                ShowValidationError($"Erro ao carregar lançamentos: {ex.Message}");
            }
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
    }
}
