using JrTools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
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

        public FecharProcessos()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += Page_Loaded;

        }

        // Evento quando o botão "ManterTudoFechadoToggleButton" é alterado
        private async void ManterTudoFechadoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ManterTudoFechadoToggleButton.IsChecked == true)
            {
                // Criar um novo CancellationTokenSource
                _processoLoopCts = new CancellationTokenSource();
                await IniciarLoopDeKillTodos(_processoLoopCts.Token);
            }
            else
            {
                // Cancelar o loop
                _processoLoopCts?.Cancel();
            }
        }

        private async Task IniciarLoopDeKillTodos(CancellationToken token)
        {
            string[] processos = { "BPrv230", "CS1", "Builder"};

            while (!token.IsCancellationRequested && ManterTudoFechadoToggleButton.IsChecked == true)
            {
                var progresso = new Progress<string>(msg => AppendTerminalLog(msg));

                foreach (var nomeProcesso in processos)
                {
                    ToggleButton? toggle = nomeProcesso switch
                    {
                        "BPrv230" => BPrv230ToggleButton,
                        "CS1" => CS1ToggleButton,
                        "Builder" => BuilderToggleButton,
                        _ => null
                    };

                    if (toggle != null && toggle.IsChecked == true)
                    {
                        await ProcessKiller.KillByNameAsync(nomeProcesso, progresso);

                        // Atualiza a quantidade imediatamente
                        int qtd = await ProcessKiller.QuantidadeDeProcessos(nomeProcesso, progresso);
                        toggle.Content = new TextBlock
                        {
                            Text = $"{nomeProcesso}\n{qtd}",
                            TextAlignment = TextAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        };
                    }
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), token); // espera antes da próxima rodada
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }


        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_valoresCarregados)
                return;

            _valoresCarregados = true;

            _quantidadeLoopCts = new CancellationTokenSource();
            _ = AtualizarQuantidadesLoop(_quantidadeLoopCts.Token);
        }



        private async Task AtualizarQuantidadesLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var itemsControl = FindChild<ItemsControl>(ExpanderDotNet);

                if (itemsControl?.ItemsPanelRoot is Panel panel)
                {
                    foreach (var child in panel.Children)
                    {
                        if (child is ToggleButton toggle && toggle != ManterTudoFechadoToggleButton)
                        {
                            string contentOriginal;

                            if (toggle.Content is TextBlock tb)
                                contentOriginal = tb.Text.Split('\n')[0]; // pega só o nome do processo
                            else
                                contentOriginal = toggle.Content?.ToString() ?? "";

                            int result = await ProcessKiller.QuantidadeDeProcessos(contentOriginal);

                            toggle.Content = new TextBlock
                            {
                                Text = $"{contentOriginal}\n{result}",
                                TextAlignment = TextAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                TextWrapping = TextWrapping.Wrap
                            };
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


        // Se você quiser parar o loop quando sair da página:
        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            _quantidadeLoopCts?.Cancel();
            base.OnNavigatedFrom(e);
        }


        // Método genérico para encontrar um filho de determinado tipo no visual tree
        private T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t)
                    return t;

                var result = FindChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
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
