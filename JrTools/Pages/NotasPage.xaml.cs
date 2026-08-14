using JrTools.Dto;
using JrTools.Services.Db;
using Markdig;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace JrTools.Pages
{
    public sealed partial class NotasPage : Page
    {
        private List<NotaDto> _todasNotas = new();
        private string _topicoFiltro = "";
        private NotaDto? _notaAtual;

        // WebView2 criado sob demanda — evita crash de init automática
        private WebView2? _webView;

        // Auto-save: dispara 1.5s após última alteração
        private readonly DispatcherTimer _autoSaveTimer;
        private bool _alteracoesPendentes = false;

        // Evita que eventos TextChanged disparem durante carregamento dos campos
        private bool _suspendEvents = false;

        private bool _jaCarregou = false;

        public NotasPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            // Definir DEPOIS do InitializeComponent — evita SelectionChanged prematuro
            OrdenarCombo.SelectedIndex = 0;

            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _autoSaveTimer.Tick += async (_, _) =>
            {
                _autoSaveTimer.Stop();
                await SalvarNotaAtualAsync();
            };

            this.Loaded += NotasPage_Loaded;
        }

        private async void NotasPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_jaCarregou) return;
            _jaCarregou = true;
            await CarregarNotasAsync();
        }

        private async Task CarregarNotasAsync()
        {
            var data = await NotasHelper.LerAsync();
            _todasNotas = data.Notas;
            AtualizarChipsTopicos();
            AtualizarGridCards();
        }

        // ── Chips de Tópicos ──────────────────────────────────────────────────

        private void AtualizarChipsTopicos()
        {
            TopicosPanel.Children.Clear();

            var topicos = new[] { "" }
                .Concat(_todasNotas.Select(n => n.Topico).Distinct().OrderBy(t => t));

            foreach (var topico in topicos)
            {
                var isAtivo = _topicoFiltro == topico;
                var chip = new Button
                {
                    Content = string.IsNullOrEmpty(topico) ? "Todos" : topico,
                    Padding = new Thickness(14, 5, 14, 5),
                    CornerRadius = new CornerRadius(20),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 4, 0),
                    Tag = topico
                };

                if (isAtivo)
                    chip.Style = Application.Current.Resources["AccentButtonStyle"] as Style;

                var t = topico;
                chip.Click += (_, _) =>
                {
                    _topicoFiltro = t;
                    AtualizarChipsTopicos();
                    AtualizarGridCards();
                };

                TopicosPanel.Children.Add(chip);
            }
        }

        // ── Grid de Cards ─────────────────────────────────────────────────────

        private void AtualizarGridCards()
        {
            if (NotasGridView == null) return;

            var filtrado = _todasNotas.AsEnumerable();

            if (!string.IsNullOrEmpty(_topicoFiltro))
                filtrado = filtrado.Where(n => n.Topico == _topicoFiltro);

            var busca = BuscaInput?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(busca))
                filtrado = filtrado.Where(n =>
                    n.Titulo.Contains(busca, StringComparison.OrdinalIgnoreCase) ||
                    n.Conteudo.Contains(busca, StringComparison.OrdinalIgnoreCase));

            filtrado = (OrdenarCombo?.SelectedIndex ?? 0) switch
            {
                0 => filtrado.OrderByDescending(n => n.EditadoEm),
                1 => filtrado.OrderBy(n => n.EditadoEm),
                2 => filtrado.OrderBy(n => n.Titulo, StringComparer.OrdinalIgnoreCase),
                3 => filtrado.OrderByDescending(n => n.Titulo, StringComparer.OrdinalIgnoreCase),
                _ => filtrado
            };

            var lista = filtrado.ToList();
            NotasGridView.ItemsSource = lista;

            var total = _todasNotas.Count;
            ContadorText.Text = total == 1 ? "1 nota" : total > 1 ? $"{total} notas" : "";

            ListaEmptyState.Visibility = lista.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            NotasGridView.Visibility = lista.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Navegação Cards → Detalhe ─────────────────────────────────────────

        private void NotaCard_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is NotaDto nota)
                AbrirDetalhe(nota);
        }

        private void NovaNota_Click(object sender, RoutedEventArgs e)
        {
            var nova = new NotaDto
            {
                Topico = string.IsNullOrEmpty(_topicoFiltro) ? "Geral" : _topicoFiltro
            };
            _todasNotas.Insert(0, nova);
            _ = SalvarAsync();
            AtualizarGridCards();
            AbrirDetalhe(nova);
        }

        private void AbrirDetalhe(NotaDto nota)
        {
            _notaAtual = nota;
            _alteracoesPendentes = false;

            // Preenche campos sem disparar auto-save
            _suspendEvents = true;
            DetailTituloInput.Text = nota.Titulo;
            DetailTopicoInput.Text = nota.Topico;
            DetailConteudoInput.Text = nota.Conteudo;
            _suspendEvents = false;

            StatusSalvoText.Text = "Salvo ✓";

            // Garante modo editor (não preview)
            if (PreviewToggle.IsChecked == true)
            {
                PreviewToggle.IsChecked = false;
                ModoEdicao();
            }

            CardsView.Visibility = Visibility.Collapsed;
            DetailView.Visibility = Visibility.Visible;

            // Foco no título se a nota for nova, no conteúdo se já tiver título
            if (string.IsNullOrEmpty(nota.Titulo))
                DetailTituloInput.Focus(FocusState.Programmatic);
            else
                DetailConteudoInput.Focus(FocusState.Programmatic);
        }

        // ── Voltar ao grid ────────────────────────────────────────────────────

        private async void Voltar_Click(object sender, RoutedEventArgs e)
        {
            _autoSaveTimer.Stop();
            await SalvarNotaAtualAsync();

            DetailView.Visibility = Visibility.Collapsed;
            CardsView.Visibility = Visibility.Visible;

            AtualizarChipsTopicos();
            AtualizarGridCards();
        }

        // ── Edição inline (auto-save) ─────────────────────────────────────────

        private void DetailTitulo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suspendEvents || _notaAtual == null) return;
            _notaAtual.Titulo = DetailTituloInput.Text;
            IniciarAutoSave();
        }

        private void DetailTopico_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suspendEvents || _notaAtual == null) return;
            _notaAtual.Topico = string.IsNullOrWhiteSpace(DetailTopicoInput.Text)
                ? "Geral"
                : DetailTopicoInput.Text.Trim();
            IniciarAutoSave();
        }

        private void DetailConteudo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suspendEvents || _notaAtual == null) return;
            _notaAtual.Conteudo = DetailConteudoInput.Text;
            IniciarAutoSave();
        }

        private void IniciarAutoSave()
        {
            _alteracoesPendentes = true;
            StatusSalvoText.Text = "...";
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private async Task SalvarNotaAtualAsync()
        {
            if (_notaAtual == null || !_alteracoesPendentes) return;
            _notaAtual.EditadoEm = DateTime.Now;
            _alteracoesPendentes = false;
            await SalvarAsync();
            StatusSalvoText.Text = "Salvo ✓";
        }

        // ── Preview Markdown ──────────────────────────────────────────────────

        private async void PreviewToggle_Checked(object sender, RoutedEventArgs e)
        {
            DetailConteudoInput.Visibility = Visibility.Collapsed;
            PreviewContainer.Visibility = Visibility.Visible;

            try
            {
                await EnsureWebViewAsync();
                _webView!.NavigateToString(GerarHtml(_notaAtual?.Conteudo ?? ""));
            }
            catch
            {
                PreviewToggle.IsChecked = false;
                ModoEdicao();
            }
        }

        private void PreviewToggle_Unchecked(object sender, RoutedEventArgs e)
            => ModoEdicao();

        private void ModoEdicao()
        {
            DetailConteudoInput.Visibility = Visibility.Visible;
            PreviewContainer.Visibility = Visibility.Collapsed;
        }

        private async Task EnsureWebViewAsync()
        {
            if (_webView != null) return;
            _webView = new WebView2 { DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0) };
            PreviewContainer.Children.Add(_webView);
            await _webView.EnsureCoreWebView2Async();
        }

        private static string GerarHtml(string markdown)
        {
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var html = Markdown.ToHtml(markdown, pipeline);
            return $$"""
                <!DOCTYPE html><html><head><meta charset="utf-8">
                <style>
                  :root { color-scheme: light dark; }
                  * { box-sizing: border-box; margin: 0; padding: 0; }
                  body { font-family: 'Segoe UI', system-ui, sans-serif; font-size: 14px; color: CanvasText; background: transparent; padding: 4px; line-height: 1.75; }
                  h1 { font-size: 22px; border-bottom: 1px solid ButtonFace; padding-bottom: 8px; margin: 24px 0 12px; }
                  h2 { font-size: 18px; margin: 20px 0 10px; }
                  h3 { font-size: 15px; margin: 16px 0 8px; }
                  h1:first-child, h2:first-child, h3:first-child { margin-top: 0; }
                  p { margin: 10px 0; }
                  a { color: LinkText; text-decoration: none; }
                  a:hover { text-decoration: underline; }
                  code { font-family: 'Cascadia Code','Consolas',monospace; background: ButtonFace; padding: 2px 6px; border-radius: 4px; font-size: 13px; }
                  pre { background: ButtonFace; border: 1px solid ButtonShadow; border-radius: 8px; padding: 14px; overflow-x: auto; margin: 12px 0; }
                  pre code { background: transparent; padding: 0; }
                  blockquote { border-left: 3px solid LinkText; margin: 14px 0; padding: 8px 16px; color: GrayText; background: ButtonFace; border-radius: 0 6px 6px 0; }
                  ul, ol { padding-left: 22px; margin: 10px 0; }
                  li { margin: 4px 0; }
                  table { border-collapse: collapse; width: 100%; margin: 14px 0; }
                  th, td { border: 1px solid ButtonShadow; padding: 8px 12px; text-align: left; }
                  th { background: ButtonFace; }
                  hr { border: none; border-top: 1px solid ButtonShadow; margin: 20px 0; }
                  img { max-width: 100%; border-radius: 6px; }
                  del { color: GrayText; text-decoration: line-through; }
                </style></head>
                <body>{{html}}</body></html>
                """;
        }

        // ── Excluir nota ──────────────────────────────────────────────────────

        private async void ExcluirDetail_Click(object sender, RoutedEventArgs e)
        {
            if (_notaAtual == null) return;

            var confirmar = new ContentDialog
            {
                Title = "Excluir nota",
                Content = $"Deseja excluir \"{(string.IsNullOrEmpty(_notaAtual.Titulo) ? "Sem título" : _notaAtual.Titulo)}\"?",
                PrimaryButtonText = "Excluir",
                SecondaryButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot
            };

            if (await confirmar.ShowAsync() != ContentDialogResult.Primary) return;

            _autoSaveTimer.Stop();
            _todasNotas.RemoveAll(n => n.Id == _notaAtual.Id);
            _notaAtual = null;
            await SalvarAsync();

            DetailView.Visibility = Visibility.Collapsed;
            CardsView.Visibility = Visibility.Visible;
            AtualizarChipsTopicos();
            AtualizarGridCards();
        }

        // ── Filtros ───────────────────────────────────────────────────────────

        private void Busca_TextChanged(object sender, TextChangedEventArgs e)
            => AtualizarGridCards();

        private void Ordenar_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => AtualizarGridCards();

        // ── Persistência ──────────────────────────────────────────────────────

        private async Task SalvarAsync()
            => await NotasHelper.SalvarAsync(new NotasDataObject { Notas = _todasNotas });
    }
}
