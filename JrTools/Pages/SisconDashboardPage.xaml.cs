using JrTools.Flows;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace JrTools.Pages
{
    // DTO simples para o DataTemplate do ListView
    public class SisconAtividadeItem
    {
        public int Id { get; set; }
        public string Titulo { get; set; }    // "SMS-{id}  {resumo}" — para exibição
        public string Resumo { get; set; }    // só o resumo — para montar descrição do Toggl
        public string TagLib { get; set; }    // "Lib: 009.001.000" ou "V. 09.01"
        public string TagVersao { get; set; } // situação atual
        public string Cliente { get; set; }
    }

    public sealed partial class SisconDashboardPage : Page
    {
        public SisconDashboardPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            this.Loaded += SisconDashboardPage_Loaded;
        }

        private async void SisconDashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CarregarDadosAsync();
        }

        private async void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        {
            await CarregarDadosAsync();
        }

        private async Task CarregarDadosAsync()
        {
            // Reset de estado
            StatusInfoBar.IsOpen = false;
            PanelConteudo.Visibility = Visibility.Collapsed;
            PanelNaoConfigurado.Visibility = Visibility.Collapsed;
            PanelLoading.Visibility = Visibility.Visible;

            try
            {
                var dados = await PerfilPessoalHelper.LerConfiguracoesAsync();

                if (string.IsNullOrWhiteSpace(dados.LoginSiscon) ||
                    string.IsNullOrWhiteSpace(dados.SenhaSiscon))
                {
                    PanelLoading.Visibility = Visibility.Collapsed;
                    PanelNaoConfigurado.Visibility = Visibility.Visible;
                    return;
                }

                var siscon = new SisconService(dados);

                // Carrega em paralelo
                var taskInfo = siscon.GetMinhasInformacoesAsync();
                var taskHoras = siscon.GetMinhasHorasAsync();
                var taskAtividades = siscon.GetMinhasAtividadesAsync();

                await Task.WhenAll(taskInfo, taskHoras, taskAtividades);

                RenderizarInfoUsuario(taskInfo.Result);
                RenderizarHoras(taskHoras.Result);
                RenderizarAtividades(taskAtividades.Result);

                PanelLoading.Visibility = Visibility.Collapsed;
                PanelConteudo.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                PanelLoading.Visibility = Visibility.Collapsed;
                StatusInfoBar.Title = "Erro ao carregar dados";
                StatusInfoBar.Message = ex.Message;
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
            }
        }

        // ── Renderização ────────────────────────────────────────────────────

        private void RenderizarInfoUsuario(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var nome = root.TryGetProperty("nome", out var nomeProp) ? nomeProp.GetString() : "—";
                var qtd = root.TryGetProperty("qtdatividades", out var qtdProp) ? qtdProp.GetInt32().ToString() : "—";
                var apelido = root.TryGetProperty("apelido", out var apelidoProp) ? apelidoProp.GetString() : "";

                TxtNomeUsuario.Text = nome ?? "—";
                TxtQtdAtividades.Text = qtd;
                SubtitleText.Text = $"Olá, {apelido ?? nome} · Siscon";
            }
            catch
            {
                TxtNomeUsuario.Text = "—";
                TxtQtdAtividades.Text = "—";
            }
        }

        private void RenderizarHoras(string json)
        {
            HorasCardsPanel.Children.Clear();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // A API pode retornar array direto ou objeto com propriedade de lista
                JsonElement diasArray;
                if (root.ValueKind == JsonValueKind.Array)
                    diasArray = root;
                else if (root.TryGetProperty("dias", out var d) || root.TryGetProperty("Dias", out d))
                    diasArray = d;
                else
                    return;

                var diasUteis = new List<(string diaNome, string dataLabel, double horas)>();

                foreach (var dia in diasArray.EnumerateArray())
                {
                    // Tenta ler data
                    string dataStr = null;
                    if (dia.TryGetProperty("data", out var dataProp)) dataStr = dataProp.GetString();
                    else if (dia.TryGetProperty("Data", out dataProp)) dataStr = dataProp.GetString();
                    if (dataStr == null) continue;

                    // Tenta ler horas
                    double horas = 0;
                    if (dia.TryGetProperty("horas", out var horasProp)) horas = horasProp.GetDouble();
                    else if (dia.TryGetProperty("Horas", out horasProp)) horas = horasProp.GetDouble();
                    else if (dia.TryGetProperty("totalHoras", out horasProp)) horas = horasProp.GetDouble();

                    // Filtra fins de semana
                    var parts = dataStr.Split('T')[0].Split('-');
                    if (parts.Length < 3) continue;
                    var date = new DateTime(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;

                    string[] nomes = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
                    diasUteis.Add((nomes[(int)date.DayOfWeek], $"{date.Day:D2}/{date.Month:D2}", horas));

                    if (diasUteis.Count == 5) break;
                }

                foreach (var (diaNome, dataLabel, horas) in diasUteis)
                {
                    bool completo = horas >= 8.0;

                    // Cores: verde para completo, vermelho para pendente (usando recursos do sistema)
                    var statusBrush = (SolidColorBrush)Application.Current.Resources[completo ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"];
                    var color = statusBrush.Color;

                    var bgBrush     = new SolidColorBrush(Color.FromArgb(20,  color.R, color.G, color.B));
                    var borderBrush = new SolidColorBrush(Color.FromArgb(60,  color.R, color.G, color.B));
                    var fgBrush     = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));
                    var badgeBg     = new SolidColorBrush(Color.FromArgb(30,  color.R, color.G, color.B));

                    var card = new Border
                    {
                        MinWidth = 80,
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10),
                        Background = bgBrush,
                        BorderBrush = borderBrush,
                        BorderThickness = new Thickness(1),
                        Child = new StackPanel
                        {
                            Spacing = 4,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = diaNome.ToUpper(),
                                    FontSize = 10,
                                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                    Opacity = 0.6,
                                    HorizontalAlignment = HorizontalAlignment.Center
                                },
                                new TextBlock
                                {
                                    Text = dataLabel,
                                    FontSize = 11,
                                    Opacity = 0.8,
                                    HorizontalAlignment = HorizontalAlignment.Center
                                },
                                new Border
                                {
                                    CornerRadius = new CornerRadius(12),
                                    Padding = new Thickness(8, 3, 8, 3),
                                    Background = badgeBg,
                                    Child = new TextBlock
                                    {
                                        Text = $"{horas:0.#}h",
                                        FontSize = 14,
                                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                                        Foreground = fgBrush,
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    }
                                }
                            }
                        }
                    };

                    HorasCardsPanel.Children.Add(card);
                }

                if (diasUteis.Count == 0)
                {
                    HorasCardsPanel.Children.Add(new TextBlock
                    {
                        Text = "Nenhum lançamento de horas encontrado.",
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Opacity = 0.6
                    });
                }
            }
            catch (Exception ex)
            {
                HorasCardsPanel.Children.Add(new TextBlock
                {
                    Text = $"Erro ao carregar horas: {ex.Message}",
                    Opacity = 0.6,
                    FontSize = 12
                });
            }
        }

        private void RenderizarAtividades(string json)
        {
            var itens = new List<SisconAtividadeItem>();

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // A API retorna array direto
                JsonElement array = root.ValueKind == JsonValueKind.Array ? root : default;
                if (array.ValueKind != JsonValueKind.Array)
                {
                    if (root.TryGetProperty("atividades", out var a) || root.TryGetProperty("Atividades", out a))
                        array = a;
                    else
                        array = root;
                }

                foreach (var item in array.EnumerateArray())
                {
                    int id = item.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;

                    // resumo
                    string resumo = item.TryGetProperty("resumo", out var rProp) ? rProp.GetString() ?? "" : "";

                    // releaselib pode ser null — ex: primeira atividade do retorno real
                    string lib = "—";
                    if (item.TryGetProperty("releaselib", out var rlProp) &&
                        rlProp.ValueKind == JsonValueKind.Object &&
                        rlProp.TryGetProperty("nome", out var rlNome))
                        lib = rlNome.GetString() ?? "—";

                    // versao pode ser null
                    string versao = "—";
                    if (item.TryGetProperty("versao", out var vProp) &&
                        vProp.ValueKind == JsonValueKind.Object &&
                        vProp.TryGetProperty("versao", out var vvProp))
                        versao = vvProp.GetString() ?? "—";

                    // situação atual
                    string situacao = "";
                    if (item.TryGetProperty("situacaoatual", out var sitProp) &&
                        sitProp.ValueKind == JsonValueKind.Object &&
                        sitProp.TryGetProperty("situacao", out var sitNome))
                        situacao = sitNome.GetString() ?? "";

                    // cliente fantasia
                    string cliente = "";
                    if (item.TryGetProperty("cliente", out var cliProp) &&
                        cliProp.ValueKind == JsonValueKind.Object &&
                        cliProp.TryGetProperty("fantasia", out var cliFan))
                        cliente = cliFan.GetString() ?? "";

                    itens.Add(new SisconAtividadeItem
                    {
                        Id = id,
                        Resumo = resumo,
                        Titulo = $"SMS-{id}  {resumo}",
                        TagLib = lib != "—" ? $"Lib: {lib}" : $"V. {versao}",
                        TagVersao = situacao,
                        Cliente = cliente
                    });
                }
            }
            catch { /* silencia — lista ficará vazia */ }

            AtividadesListView.ItemsSource = itens;
            TxtSemAtividades.Visibility = itens.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Lançar Horas ────────────────────────────────────────────────────

        private async void BtnLancarHoras_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not SisconAtividadeItem sms) return;

            var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();
            var horasService = new HorasToggle(perfil.ApiToggl);
            var inicioSugerido = await horasService.SugerirProximoHorarioInicioAsync(DateTime.Today);

            // ── Monta o conteúdo do dialog ──────────────────────────────────
            var horaInicioPicker = new TimePicker
            {
                Header = "Hora de início",
                ClockIdentifier = "24HourClock",
                SelectedTime = inicioSugerido,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var horaFimPicker = new TimePicker
            {
                Header = "Hora de fim",
                ClockIdentifier = "24HourClock",
                SelectedTime = null,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var totalHorasBox = new NumberBox
            {
                Header = "— ou informe o total de horas —",
                Minimum = 0,
                Maximum = 24,
                SmallChange = 0.5,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                PlaceholderText = "Ex: 4",
                Value = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var descricaoBox = new TextBox
            {
                Header = "Descrição (opcional)",
                PlaceholderText = $"SMS-{sms.Id} {sms.Resumo}",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var infoBar = new InfoBar
            {
                IsOpen = false,
                Severity = InfoBarSeverity.Error,
                IsClosable = false
            };

            // Recalcula total ao mudar início/fim
            void RecalcularTotal()
            {
                if (horaInicioPicker.SelectedTime.HasValue && horaFimPicker.SelectedTime.HasValue)
                {
                    var diff = horaFimPicker.SelectedTime.Value - horaInicioPicker.SelectedTime.Value;
                    if (diff > TimeSpan.Zero)
                        totalHorasBox.Value = Math.Round(diff.TotalHours, 2);
                }
            }

            // Recalcula fim ao mudar início/total
            void RecalcularFim()
            {
                if (horaInicioPicker.SelectedTime.HasValue && !double.IsNaN(totalHorasBox.Value) && totalHorasBox.Value > 0)
                {
                    horaFimPicker.SelectedTime = horaInicioPicker.SelectedTime.Value.Add(TimeSpan.FromHours(totalHorasBox.Value));
                }
            }

            horaInicioPicker.SelectedTimeChanged += (s, _) => RecalcularTotal();
            horaFimPicker.SelectedTimeChanged += (s, _) => RecalcularTotal();
            totalHorasBox.ValueChanged += (s, _) => RecalcularFim();

            // Cabeçalho da SMS
            var accentBrush = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            var accentColor = accentBrush.Color;

            var headerBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromArgb(20, accentColor.R, accentColor.G, accentColor.B)),
                Child = new TextBlock
                {
                    Text = sms.Titulo,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (SolidColorBrush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                }
            };

            var conteudo = new StackPanel
            {
                Spacing = 12,
                MinWidth = 320,
                Children =
                {
                    headerBorder,
                    new TextBlock
                    {
                        Text = $"Data: {DateTime.Today:dd/MM/yyyy}",
                        FontSize = 12,
                        Opacity = 0.6
                    },
                    new Grid
                    {
                        ColumnSpacing = 12,
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                        },
                        Children =
                        {
                            horaInicioPicker,
                            horaFimPicker
                        }
                    },
                    totalHorasBox,
                    descricaoBox,
                    infoBar
                }
            };

            // Posiciona horaFimPicker na coluna 1
            Grid.SetColumn(horaFimPicker, 1);

            var dialog = new ContentDialog
            {
                Title = "Lançar Horas",
                Content = conteudo,
                PrimaryButtonText = "Lançar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var resultado = await dialog.ShowAsync();
            if (resultado != ContentDialogResult.Primary) return;

            // ── Valida e salva ──────────────────────────────────────────────
            try
            {
                if (string.IsNullOrWhiteSpace(perfil.ApiToggl))
                    throw new Exception("Token do Toggl não configurado. Acesse Configurações → APIs.");

                TimeSpan? inicio = horaInicioPicker.SelectedTime;
                TimeSpan? fim = horaFimPicker.SelectedTime;
                double total = double.IsNaN(totalHorasBox.Value) ? 0 : totalHorasBox.Value;

                // Se só informou total sem início/fim, usa a sugestão (que pode ter mudado se houve novos lançamentos)
                if (total > 0 && !inicio.HasValue)
                {
                    inicio = await horasService.SugerirProximoHorarioInicioAsync(DateTime.Today);
                    fim = inicio.Value.Add(TimeSpan.FromHours(total));
                }
                else if (inicio.HasValue && fim.HasValue)
                {
                    var diff = fim.Value - inicio.Value;
                    if (diff <= TimeSpan.Zero)
                        throw new Exception("O horário de fim deve ser maior que o de início.");
                    total = diff.TotalHours;
                }
                else if (total <= 0)
                {
                    throw new Exception("Informe o período (início/fim) ou o total de horas.");
                }

                // Descrição: usa o que o usuário digitou ou monta padrão SMS-ID resumo
                string descricao = string.IsNullOrWhiteSpace(descricaoBox.Text)
                    ? $"SMS-{sms.Id} {sms.Resumo}"
                    : descricaoBox.Text.Trim();

                // Garante prefixo SMS-
                if (!descricao.StartsWith("SMS-", StringComparison.OrdinalIgnoreCase))
                    descricao = $"SMS-{sms.Id} {descricao}";

                var lancamento = new JrTools.Dto.HoraLancamento
                {
                    Data = DateTime.Today,
                    HoraInicio = inicio,
                    HoraFim = fim,
                    TotalHoras = total,
                    Descricao = descricao
                };

                await horasService.SalvarLancamentoAsync(lancamento);

                // Feedback de sucesso
                StatusInfoBar.Title = "Horas lançadas";
                StatusInfoBar.Message = $"{total:0.##}h lançadas para {descricao}";
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.IsOpen = true;

                // Atualiza o dashboard para refletir as novas horas
                await CarregarDadosAsync();
            }
            catch (Exception ex)
            {
                StatusInfoBar.Title = "Erro ao lançar horas";
                StatusInfoBar.Message = ex.Message;
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.IsOpen = true;
            }
        }

        private void BtnCopiarSms_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int smsId)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(smsId.ToString());
                Clipboard.SetContent(dataPackage);

                NotificationService.Instance.ShowNotification(
                    "Copiado",
                    $"O número da SMS {smsId} foi copiado para a área de transferência."
                );
            }
        }

        // ── Detalhe da SMS ──────────────────────────────────────────────────

        private async void AtividadesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not SisconAtividadeItem item) return;

            try
            {
                var dados = await PerfilPessoalHelper.LerConfiguracoesAsync();
                var siscon = new SisconService(dados);
                var json = await siscon.GetAtividadeDetalheAsync(item.Id);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Monta um texto de detalhe legível
                var sb = new System.Text.StringBuilder();
                foreach (var prop in root.EnumerateObject())
                {
                    sb.AppendLine($"{prop.Name}: {prop.Value}");
                }

                var dialog = new ContentDialog
                {
                    Title = item.Titulo,
                    Content = new ScrollViewer
                    {
                        MaxHeight = 400,
                        Content = new TextBlock
                        {
                            Text = sb.ToString(),
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12
                        }
                    },
                    CloseButtonText = "Fechar",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Erro ao carregar detalhe",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }
}
