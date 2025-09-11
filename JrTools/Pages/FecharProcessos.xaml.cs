using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Pages
{
 


    public sealed partial class FecharProcessos : Page
    {
        private bool _valoresCarregados = false;
        private const int MAX_TERMINAL_LENGTH = 15000;
        private CancellationTokenSource? _processoLoopCts;
        private CancellationTokenSource? _quantidadeLoopCts;

        // Apenas edite esta lista para adicionar novos processos
        private readonly ProcessoConfigDto[] _processos =
        {
            new ProcessoConfigDto("BPrv230", true),  // começa ativo
            new ProcessoConfigDto("CS1", true),
            new ProcessoConfigDto("Builder", false),
            new ProcessoConfigDto("w3wp", true)
        };

        public FecharProcessos()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += Page_Loaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_valoresCarregados)
                return;

            _valoresCarregados = true;

            CriarToggleButtonsDinamicamente();

            _quantidadeLoopCts = new CancellationTokenSource();
            _ = AtualizarQuantidadesLoop(_quantidadeLoopCts.Token);
        }

        private void CriarToggleButtonsDinamicamente()
        {
            foreach (var proc in _processos)
            {
                var toggle = new ToggleButton
                {
                    FontSize = 16,
                    Margin = new Thickness(10),
                    Width = 150,
                    Height = 150,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    IsChecked = proc.AtivoPorPadrao,
                    Content = new TextBlock
                    {
                        Text = $"{proc.Nome}\n0",
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    }
                };

                ProcessosItemsControl.Items.Add(toggle);
            }
        }


        // Botão principal para manter tudo fechado
        private async void ManterTudoFechadoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ManterTudoFechadoToggleButton.IsChecked == true)
            {
                _processoLoopCts = new CancellationTokenSource();
                await IniciarLoopDeKillTodos(_processoLoopCts.Token);
            }
            else
            {
                _processoLoopCts?.Cancel();
            }
        }

        private async Task IniciarLoopDeKillTodos(CancellationToken token)
        {
            while (!token.IsCancellationRequested && ManterTudoFechadoToggleButton.IsChecked == true)
            {
                var progresso = new Progress<string>(msg => AppendTerminalLog(msg));

                foreach (var item in ProcessosItemsControl.Items)
                {
                    if (item is ToggleButton toggle && toggle != ManterTudoFechadoToggleButton)
                    {
                        string nomeProcesso = ExtrairNomeDoToggle(toggle);

                        if (toggle.IsChecked == true)
                        {
                            await GarantirProcessoFechado(nomeProcesso, toggle, progresso);
                        }
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task GarantirProcessoFechado(string nomeProcesso, ToggleButton toggle, IProgress<string> progresso)
        {
            int tentativas = 0;
            while (await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso) > 0)
            {
                progresso.Report($"Processo {nomeProcesso} ainda ativo, tentando fechar...");
                await ProcessKiller.KillByNameAsync(nomeProcesso, progresso);
                tentativas++;
                if (tentativas > 10)
                {
                    progresso.Report($"Não foi possível encerrar o processo {nomeProcesso}");
                    break;
                }
                await Task.Delay(300);
            }

            int qtd = await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso);
            AtualizarTextoToggle(toggle, nomeProcesso, qtd);
        }

        private async Task AtualizarQuantidadesLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var item in ProcessosItemsControl.Items)
                {
                    if (item is ToggleButton toggle && toggle != ManterTudoFechadoToggleButton)
                    {
                        string nomeProcesso = ExtrairNomeDoToggle(toggle);
                        int qtd = await ProcessKiller.QuantidadeDeProcessos(nomeProcesso);
                        AtualizarTextoToggle(toggle, nomeProcesso, qtd);
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private static string ExtrairNomeDoToggle(ToggleButton toggle)
        {
            if (toggle.Content is TextBlock tb)
                return tb.Text.Split('\n')[0];
            return toggle.Content?.ToString() ?? "";
        }

        private static void AtualizarTextoToggle(ToggleButton toggle, string nomeProcesso, int quantidade)
        {
            toggle.Content = new TextBlock
            {
                Text = $"{nomeProcesso}\n{quantidade}",
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _quantidadeLoopCts?.Cancel();
            base.OnNavigatedFrom(e);
        }

        private void AppendTerminalLog(string mensagem)
        {
            TerminalOutput.Text += mensagem + "\n";

            if (TerminalOutput.Text.Length > MAX_TERMINAL_LENGTH)
            {
                TerminalOutput.Text = TerminalOutput.Text.Substring(TerminalOutput.Text.Length - MAX_TERMINAL_LENGTH);
            }

            TerminalScrollViewer.ChangeView(null, TerminalScrollViewer.ScrollableHeight, null);
        }
    }
}
