using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;

namespace JrTools.ViewModels
{
    public class BinarioDelphiExplorerViewModel : INotifyPropertyChanged
    {
        // ── Serviço de índice ──────────────────────────────────────────────────
        private readonly BinarioDelphiIndexService _indexService;

        // ── Serviços abstraídos (substituíveis em testes) ──────────────────────
        private readonly IProcessLauncher _processLauncher;
        private readonly IClipboardService _clipboardService;

        // ── Estado interno ─────────────────────────────────────────────────────
        private Dictionary<string, BinarioDelphiItem>? _indiceAtual;
        private DispatcherQueue? _dispatcher;
        private CancellationTokenSource? _cts;

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Propriedades bindable ──────────────────────────────────────────────

        /// <summary>Coleção filtrada exibida na ListView.</summary>
        public ObservableCollection<BinarioDelphiItem> ItensFiltrados { get; } = new();

        private BinarioDelphiItem? _itemSelecionado;
        /// <summary>Item selecionado na lista. Ao ser definido, dispara o carregamento de detalhes.</summary>
        public BinarioDelphiItem? ItemSelecionado
        {
            get => _itemSelecionado;
            set
            {
                if (_itemSelecionado == value) return;
                _itemSelecionado = value;
                OnPropertyChanged();
                if (value != null)
                    _ = CarregarDetalheAsync(value);
            }
        }

        private BinarioDelphiDetalhe? _detalheAtual;
        /// <summary>Detalhes do item selecionado, carregados sob demanda.</summary>
        public BinarioDelphiDetalhe? DetalheAtual
        {
            get => _detalheAtual;
            private set { _detalheAtual = value; OnPropertyChanged(); OnPropertyChanged(nameof(DetalheDisponivel)); }
        }

        /// <summary>Indica se há um detalhe carregado. Usado para controlar visibilidade do painel de detalhes.</summary>
        public bool DetalheDisponivel => _detalheAtual != null;

        private string _textoFiltroNome = string.Empty;
        /// <summary>Texto de filtro por nome. Ao ser alterado, aplica o filtro automaticamente.</summary>
        public string TextoFiltroNome
        {
            get => _textoFiltroNome;
            set
            {
                if (_textoFiltroNome == value) return;
                _textoFiltroNome = value;
                OnPropertyChanged();
                AplicarFiltro();
            }
        }

        private string? _extensaoFiltro;
        /// <summary>Extensão selecionada para filtro. null = "Todas". Ao ser alterado, aplica o filtro.</summary>
        public string? ExtensaoFiltro
        {
            get => _extensaoFiltro;
            set
            {
                if (_extensaoFiltro == value) return;
                _extensaoFiltro = value;
                OnPropertyChanged();
                AplicarFiltro();
            }
        }

        /// <summary>Extensões únicas disponíveis no índice.</summary>
        public ObservableCollection<string> ExtensoesDisponiveis { get; } = new();

        private string _diretorioAtual = string.Empty;
        /// <summary>Diretório atualmente indexado.</summary>
        public string DiretorioAtual
        {
            get => _diretorioAtual;
            private set { _diretorioAtual = value; OnPropertyChanged(); }
        }

        private int _totalNoIndice;
        /// <summary>Total de itens no índice (sem filtro).</summary>
        public int TotalNoIndice
        {
            get => _totalNoIndice;
            private set { _totalNoIndice = value; OnPropertyChanged(); }
        }

        private int _totalFiltrado;
        /// <summary>Total de itens visíveis após aplicação do filtro.</summary>
        public int TotalFiltrado
        {
            get => _totalFiltrado;
            private set { _totalFiltrado = value; OnPropertyChanged(); OnPropertyChanged(nameof(ListaVaziaComIndice)); }
        }

        private bool _carregandoIndice;
        /// <summary>Indica que o índice está sendo construído.</summary>
        public bool CarregandoIndice
        {
            get => _carregandoIndice;
            private set { _carregandoIndice = value; OnPropertyChanged(); }
        }

        private bool _carregandoDetalhe;
        /// <summary>Indica que os detalhes do item selecionado estão sendo carregados.</summary>
        public bool CarregandoDetalhe
        {
            get => _carregandoDetalhe;
            private set { _carregandoDetalhe = value; OnPropertyChanged(); }
        }

        private string? _mensagemErro;
        /// <summary>Mensagem de erro a ser exibida na UI.</summary>
        public string? MensagemErro
        {
            get => _mensagemErro;
            private set { _mensagemErro = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemErro)); }
        }

        /// <summary>Indica se há uma mensagem de erro ativa. Usado para binding ao InfoBar.IsOpen.</summary>
        public bool TemErro => _mensagemErro != null;

        private bool _indiceDisponivel;
        /// <summary>Indica se o índice já foi construído e está disponível.</summary>
        public bool IndiceDisponivel
        {
            get => _indiceDisponivel;
            private set { _indiceDisponivel = value; OnPropertyChanged(); OnPropertyChanged(nameof(ListaVaziaComIndice)); }
        }

        /// <summary>Indica se a lista filtrada está vazia e o índice está disponível. Usado para exibir mensagem "Nenhum binário encontrado".</summary>
        public bool ListaVaziaComIndice => _totalFiltrado == 0 && _indiceDisponivel;

        // ── Comandos ──────────────────────────────────────────────────────────

        /// <summary>Invalida o índice atual e reconstrói a partir do disco.</summary>
        public ICommand AtualizarIndiceCommand { get; private set; } = null!;

        /// <summary>Abre a pasta do item selecionado no Windows Explorer, destacando o arquivo.</summary>
        public ICommand AbrirNoExplorerCommand { get; private set; } = null!;

        /// <summary>Copia o caminho completo do item selecionado para a área de transferência.</summary>
        public ICommand CopiarCaminhoCommand { get; private set; } = null!;

        // ── Construtor ────────────────────────────────────────────────────────

        public BinarioDelphiExplorerViewModel() : this(new BinarioDelphiIndexService()) { }

        /// <summary>
        /// Construtor interno para injeção de dependência em testes.
        /// </summary>
        internal BinarioDelphiExplorerViewModel(
            BinarioDelphiIndexService indexService,
            IProcessLauncher? processLauncher = null,
            IClipboardService? clipboardService = null)
        {
            _indexService = indexService ?? throw new ArgumentNullException(nameof(indexService));
            _processLauncher = processLauncher ?? new ProcessLauncherImpl();
            _clipboardService = clipboardService ?? new ClipboardServiceImpl();
            ConfigurarComandos();
        }

        private void ConfigurarComandos()
        {
            AtualizarIndiceCommand = new RelayCommand(async () =>
            {
                _indexService.InvalidarIndice();
                await InicializarAsync();
            });

            AbrirNoExplorerCommand = new RelayCommand(() =>
            {
                var item = ItemSelecionado;
                if (item == null) return;

                var caminho = item.CaminhoCompleto;
                try
                {
                    _processLauncher.Launch("explorer.exe", $"/select,\"{caminho}\"");
                }
                catch (Exception ex)
                {
                    MensagemErro = $"Erro ao abrir no Explorer: {ex.Message}";
                }
            }, () => ItemSelecionado != null);

            CopiarCaminhoCommand = new RelayCommand(() =>
            {
                var item = ItemSelecionado;
                if (item == null) return;

                try
                {
                    _clipboardService.SetText(item.CaminhoCompleto);
                }
                catch (Exception ex)
                {
                    MensagemErro = $"Erro ao copiar caminho: {ex.Message}";
                }
            }, () => ItemSelecionado != null);
        }

        // ── Métodos públicos ──────────────────────────────────────────────────

        /// <summary>
        /// Configura o índice diretamente para uso em testes.
        /// Popula <see cref="_indiceAtual"/>, <see cref="TotalNoIndice"/>, <see cref="IndiceDisponivel"/>
        /// e <see cref="ExtensoesDisponiveis"/>, depois chama <see cref="AplicarFiltro"/>.
        /// </summary>
        internal void SetIndiceForTesting(Dictionary<string, JrTools.Dto.BinarioDelphiItem> indice)
        {
            _indiceAtual = indice;
            TotalNoIndice = indice.Count;
            IndiceDisponivel = true;

            ExtensoesDisponiveis.Clear();
            foreach (var ext in _indexService.ExtensoesUnicas.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
                ExtensoesDisponiveis.Add(ext);

            AplicarFiltro();
        }

        /// <summary>
        /// Captura o <see cref="DispatcherQueue"/> da thread de UI atual.
        /// Deve ser chamado a partir da thread de UI (ex.: no construtor da Page ou em OnNavigatedTo).
        /// </summary>
        public void InitializeDispatcher()
        {
            if (_dispatcher == null)
                _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        /// <summary>
        /// Inicializa o ViewModel: lê a configuração, detecta mudança de diretório e constrói o índice se necessário.
        /// </summary>
        public async Task InicializarAsync()
        {
            // 1. Ler configuração
            var config = await ConfigHelper.LerConfiguracoesAsync();
            var diretorio = config?.DiretorioBinarios;

            // 2. Validar diretório configurado
            if (string.IsNullOrWhiteSpace(diretorio))
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    MensagemErro = "DiretorioBinarios não configurado.";
                    IndiceDisponivel = false;
                });
                return;
            }

            // 3. Detectar mudança de diretório
            if (!string.Equals(diretorio, DiretorioAtual, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(DiretorioAtual))
            {
                _indexService.InvalidarIndice();
            }

            // 4. Cache hit — índice já disponível para o mesmo diretório
            if (_indexService.IndiceDisponivel)
            {
                _dispatcher?.TryEnqueue(() => AplicarFiltro());
                return;
            }

            // 5. Construir índice
            _dispatcher?.TryEnqueue(() =>
            {
                CarregandoIndice = true;
                MensagemErro = null;
            });

            // Cancelar operação anterior se houver
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            var progresso = new Progress<string>(_ => { /* progresso opcional */ });

            try
            {
                var indice = await _indexService.ConstruirIndiceAsync(diretorio, progresso, ct);
                _indiceAtual = indice;

                _dispatcher?.TryEnqueue(() =>
                {
                    DiretorioAtual = diretorio;
                    TotalNoIndice = indice.Count;
                    IndiceDisponivel = true;

                    // Popular extensões disponíveis
                    ExtensoesDisponiveis.Clear();
                    foreach (var ext in _indexService.ExtensoesUnicas.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
                        ExtensoesDisponiveis.Add(ext);

                    AplicarFiltro();
                });
            }
            catch (OperationCanceledException)
            {
                // Cancelamento esperado — não exibir erro
            }
            catch (Exception ex)
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    MensagemErro = $"Erro ao construir índice: {ex.Message}";
                    IndiceDisponivel = false;
                });
            }
            finally
            {
                _dispatcher?.TryEnqueue(() => CarregandoIndice = false);
            }
        }

        /// <summary>
        /// Aplica os filtros de nome e extensão sobre o índice em memória e atualiza <see cref="ItensFiltrados"/>.
        /// </summary>
        public void AplicarFiltro()
        {
            if (!_indexService.IndiceDisponivel || _indiceAtual == null)
            {
                ItensFiltrados.Clear();
                TotalFiltrado = 0;
                return;
            }

            IEnumerable<BinarioDelphiItem> resultado = _indiceAtual.Values;

            // Filtro por nome (case-insensitive Contains)
            if (!string.IsNullOrEmpty(_textoFiltroNome))
            {
                var filtroLower = _textoFiltroNome.ToLowerInvariant();
                resultado = resultado.Where(i => i.NomeNormalizado.Contains(filtroLower));
            }

            // Filtro por extensão
            if (_extensaoFiltro != null)
                resultado = resultado.Where(i => string.Equals(i.Extensao, _extensaoFiltro, StringComparison.OrdinalIgnoreCase));

            // Ordenar por nome (case-insensitive)
            var lista = resultado.OrderBy(i => i.Nome, StringComparer.OrdinalIgnoreCase).ToList();

            ItensFiltrados.Clear();
            foreach (var item in lista)
                ItensFiltrados.Add(item);

            TotalFiltrado = ItensFiltrados.Count;
        }

        /// <summary>
        /// Carrega os detalhes do item informado de forma assíncrona.
        /// </summary>
        public async Task CarregarDetalheAsync(BinarioDelphiItem item)
        {
            _dispatcher?.TryEnqueue(() => CarregandoDetalhe = true);

            try
            {
                var detalhe = await _indexService.CarregarDetalheAsync(item);

                _dispatcher?.TryEnqueue(() =>
                {
                    DetalheAtual = detalhe;
                    CarregandoDetalhe = false;
                });
            }
            catch (Exception ex)
            {
                _dispatcher?.TryEnqueue(() =>
                {
                    DetalheAtual = new BinarioDelphiDetalhe
                    {
                        CaminhoCompleto = item.CaminhoCompleto,
                        ErroAoCarregar = true,
                        MensagemErro = ex.Message
                    };
                    CarregandoDetalhe = false;
                });
            }
        }

        // ── Implementações padrão dos serviços abstraídos ─────────────────────

        /// <summary>
        /// Implementação real de <see cref="IProcessLauncher"/> que usa <see cref="Process.Start"/>.
        /// </summary>
        private sealed class ProcessLauncherImpl : IProcessLauncher
        {
            public void Launch(string fileName, string arguments)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                });
            }
        }

        /// <summary>
        /// Implementação real de <see cref="IClipboardService"/> que usa a API WinRT.
        /// </summary>
        private sealed class ClipboardServiceImpl : IClipboardService
        {
            public void SetText(string text)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text);
                Clipboard.SetContent(dataPackage);
            }

            public string? GetText()
            {
                // Leitura síncrona do clipboard não é suportada diretamente via WinRT;
                // retorna null nesta implementação (uso apenas em testes via fake).
                return null;
            }
        }
    }
}
