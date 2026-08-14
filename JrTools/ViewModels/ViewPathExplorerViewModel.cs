using JrTools.Dto;
using JrTools.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;

namespace JrTools.ViewModels
{
    public class ViewPathExplorerViewModel : INotifyPropertyChanged
    {
        // ── Serviços abstraídos (substituíveis em testes) ──────────────────────
        private readonly IViewPathMapper _mapper;
        private readonly IClipboardService _clipboardService;
        private readonly IConfigHelper _configHelper;

        // ── Estado interno ─────────────────────────────────────────────────────
        private DispatcherQueue? _dispatcher;

        // ── INotifyPropertyChanged ─────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Propriedades bindable ──────────────────────────────────────────────

        /// <summary>Lista de projetos disponíveis (subpastas de DiretorioEspecificos).</summary>
        public ObservableCollection<PastaInformacoesDto> Projetos { get; } = new();

        private PastaInformacoesDto? _projetoSelecionado;
        /// <summary>Projeto selecionado no ComboBox.</summary>
        public PastaInformacoesDto? ProjetoSelecionado
        {
            get => _projetoSelecionado;
            set
            {
                if (_projetoSelecionado == value) return;
                _projetoSelecionado = value;
                OnPropertyChanged();
                ((RelayCommand)BuscarCommand).RaiseCanExecuteChanged();

                // Persistir seleção de forma fire-and-forget, ignorando falhas (Req 8.1)
                _ = SalvarProjetoSelecionadoAsync(value?.Nome);
            }
        }

        private string _textoViews = string.Empty;
        /// <summary>Texto multilinha com os nomes das views a buscar (uma por linha).</summary>
        public string TextoViews
        {
            get => _textoViews;
            set
            {
                if (_textoViews == value) return;
                _textoViews = value;
                OnPropertyChanged();
                ((RelayCommand)BuscarCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>Resultados da busca, agrupados por view.</summary>
        public ObservableCollection<ViewResultadoItem> Resultados { get; } = new();

        private bool _isCarregando;
        /// <summary>Indica que uma busca está em andamento.</summary>
        public bool IsCarregando
        {
            get => _isCarregando;
            private set
            {
                if (_isCarregando == value) return;
                _isCarregando = value;
                OnPropertyChanged();
                ((RelayCommand)BuscarCommand).RaiseCanExecuteChanged();
            }
        }

        private string? _mensagemErro;
        /// <summary>Mensagem de erro a ser exibida na UI.</summary>
        public string? MensagemErro
        {
            get => _mensagemErro;
            private set
            {
                _mensagemErro = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TemErro));
            }
        }

        /// <summary>Indica se há uma mensagem de erro ativa. Usado para binding ao InfoBar.IsOpen.</summary>
        public bool TemErro => _mensagemErro != null;

        // ── Comandos ──────────────────────────────────────────────────────────

        /// <summary>Inicia a busca de caminhos para as views informadas.</summary>
        public ICommand BuscarCommand { get; }

        /// <summary>Copia todos os resultados formatados para a área de transferência.</summary>
        public ICommand CopiarTudoCommand { get; }

        // ── Construtores ──────────────────────────────────────────────────────

        /// <summary>Construtor público para uso em produção.</summary>
        public ViewPathExplorerViewModel()
            : this(new ViewPathMapperAdapter(), new ClipboardServiceImpl(), new ConfigHelperImpl()) { }

        /// <summary>Construtor interno para injeção de dependência em testes.</summary>
        internal ViewPathExplorerViewModel(IViewPathMapper mapper, IClipboardService clipboard, IConfigHelper configHelper)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _clipboardService = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
            _configHelper = configHelper ?? throw new ArgumentNullException(nameof(configHelper));

            BuscarCommand = new RelayCommand(
                execute: () => _ = ExecutarBuscaAsync(),
                canExecute: () =>
                    ProjetoSelecionado != null
                    && NormalizarEntrada(TextoViews).Count > 0
                    && !IsCarregando);

            CopiarTudoCommand = new RelayCommand(
                execute: () =>
                {
                    try
                    {
                        var texto = FormatarResultadosParaCopia(Resultados);
                        _clipboardService.SetText(texto);
                    }
                    catch (Exception ex)
                    {
                        MensagemErro = $"Erro ao copiar: {ex.Message}";
                    }
                },
                canExecute: () => Resultados.Count > 0);
        }

        // ── Métodos públicos ──────────────────────────────────────────────────

        /// <summary>
        /// Captura o <see cref="DispatcherQueue"/> da thread de UI atual.
        /// Deve ser chamado a partir da thread de UI (ex.: em OnNavigatedTo).
        /// </summary>
        public void InitializeDispatcher()
        {
            if (_dispatcher == null)
                _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        /// <summary>
        /// Inicializa o ViewModel: lê configuração, popula lista de projetos e restaura seleção anterior.
        /// </summary>
        public async Task InicializarAsync()
        {
            // 1. Ler configuração (Req 1.1)
            var config = await _configHelper.LerConfiguracoesAsync();
            var diretorio = config?.DiretorioEspecificos;

            // 2. Validar diretório configurado (Req 1.2)
            if (string.IsNullOrWhiteSpace(diretorio))
            {
                MensagemErro = "DiretorioEspecificos não configurado.";
                return;
            }

            // 3. Popular lista de projetos (Req 1.1, 1.3)
            var pastas = Folders.ListarPastas(diretorio);
            Projetos.Clear();
            foreach (var pasta in pastas)
                Projetos.Add(pasta);

            // 4. Restaurar projeto selecionado anteriormente (Req 8.2, 8.3)
            // Se o projeto salvo não existir mais, deixa sem seleção e sem erro
            if (!string.IsNullOrWhiteSpace(config?.ProjetoSelecionado))
            {
                var projetoRestaurado = Projetos.FirstOrDefault(
                    p => string.Equals(p.Nome, config.ProjetoSelecionado, StringComparison.OrdinalIgnoreCase));

                // Atribuir diretamente ao campo para não disparar o save fire-and-forget
                _projetoSelecionado = projetoRestaurado;
                OnPropertyChanged(nameof(ProjetoSelecionado));
                ((RelayCommand)BuscarCommand).RaiseCanExecuteChanged();
            }
        }

        // ── Métodos internos (visíveis para testes via InternalsVisibleTo) ─────

        /// <summary>
        /// Normaliza a entrada multilinha: split por '\n', trim, remove vazios e duplicatas case-insensitive.
        /// </summary>
        /// <param name="texto">Texto multilinha com nomes de views.</param>
        /// <returns>Coleção de nomes de views normalizados, sem duplicatas.</returns>
        internal IReadOnlyCollection<string> NormalizarEntrada(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return Array.Empty<string>();

            return texto
                .Split('\n')
                .Select(linha => linha.Trim())
                .Where(linha => !string.IsNullOrWhiteSpace(linha))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Formata os resultados para cópia como texto simples.
        /// Cada view é seguida de seus caminhos, separados por quebras de linha.
        /// </summary>
        internal string FormatarResultadosParaCopia(IEnumerable<ViewResultadoItem> resultados)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in resultados)
            {
                sb.AppendLine(item.NomeView);
                if (item.SemCaminhos)
                {
                    sb.AppendLine("  Nenhum caminho encontrado");
                }
                else
                {
                    foreach (var caminho in item.Caminhos)
                        sb.AppendLine($"  {caminho}");
                }
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        // ── Métodos privados ──────────────────────────────────────────────────

        /// <summary>
        /// Executa a busca de caminhos de forma assíncrona.
        /// </summary>
        private async Task ExecutarBuscaAsync()
        {
            var projeto = ProjetoSelecionado;
            if (projeto == null) return;

            var viewsNormalizadas = NormalizarEntrada(TextoViews);
            if (viewsNormalizadas.Count == 0) return;

            // Verificar se a pasta Pages existe (Req 3.5)
            var diretorioArtifacts = ViewPathProjectPathResolver.ResolveArtifactsRoot(projeto.Caminho);
            var pastaPages = System.IO.Path.Combine(diretorioArtifacts, "Pages");
            if (!System.IO.Directory.Exists(pastaPages))
            {
                MensagemErro = $"A pasta 'Pages' não foi encontrada no projeto '{projeto.Nome}' nem em uma pasta 'Artifacts'. Nenhum XML de página disponível.";
                return;
            }

            IsCarregando = true;
            MensagemErro = null;
            Resultados.Clear();
            ((RelayCommand)CopiarTudoCommand).RaiseCanExecuteChanged();

            try
            {
                var diretorio = diretorioArtifacts;
                var resultado = await Task.Run(() =>
                    _mapper.MapearCaminhosEmLote(viewsNormalizadas, diretorio));

                var itens = resultado
                    .Select(kv => new ViewResultadoItem
                    {
                        NomeView = kv.Key,
                        Caminhos = kv.Value
                    })
                    .ToList();

                void AtualizarUI()
                {
                    Resultados.Clear();
                    foreach (var item in itens)
                        Resultados.Add(item);
                    ((RelayCommand)CopiarTudoCommand).RaiseCanExecuteChanged();
                }

                if (_dispatcher != null)
                    _dispatcher.TryEnqueue(AtualizarUI);
                else
                    AtualizarUI();
            }
            catch (Exception ex)
            {
                void SetErro()
                {
                    MensagemErro = $"Erro ao buscar caminhos: {ex.Message}";
                }

                if (_dispatcher != null)
                    _dispatcher.TryEnqueue(SetErro);
                else
                    SetErro();
            }
            finally
            {
                void SetCarregando()
                {
                    IsCarregando = false;
                }

                if (_dispatcher != null)
                    _dispatcher.TryEnqueue(SetCarregando);
                else
                    SetCarregando();
            }
        }
        /// <summary>
        /// Salva o nome do projeto selecionado de forma fire-and-forget, ignorando falhas (Req 8.1).
        /// </summary>
        private async Task SalvarProjetoSelecionadoAsync(string? nomeProjeto)
        {
            try
            {
                var config = await _configHelper.LerConfiguracoesAsync();
                config.ProjetoSelecionado = nomeProjeto;
                await _configHelper.SalvarConfiguracoesAsync(config);
            }
            catch
            {
                // Ignorar falhas ao salvar — não bloqueia o fluxo principal (Req 8.1, design)
            }
        }

        // ── Implementação interna do ClipboardService ─────────────────────────

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

        // ── Implementação interna do ConfigHelper ─────────────────────────────

        /// <summary>
        /// Implementação real de <see cref="IConfigHelper"/> que delega para a classe estática <see cref="ConfigHelper"/>.
        /// </summary>
        private sealed class ConfigHelperImpl : IConfigHelper
        {
            public Task<ConfiguracoesdataObject> LerConfiguracoesAsync() =>
                ConfigHelper.LerConfiguracoesAsync();

            public Task SalvarConfiguracoesAsync(ConfiguracoesdataObject config) =>
                ConfigHelper.SalvarConfiguracoesAsync(config);
        }
    }
}
