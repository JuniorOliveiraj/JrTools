using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static JrTools.Services.ViewPathCacheService;

namespace JrTools.Services
{
    /// <summary>
    /// Serviço responsável por mapear os caminhos de navegação das views no sistema.
    ///
    /// OBJETIVO:
    /// Descobrir TODOS os caminhos possíveis para chegar em uma view, considerando:
    /// - Páginas com widgets (AjaxForm, SimpleGrid, EditableGrid, Menu, Dashboard)
    /// - FormUrl em grids que abrem forms
    /// - Caminhos: Página → Grid → Form → Página → Grid → Form...
    ///
    /// ALGORITMO:
    ///
    /// Para encontrar caminhos para VIEW_TARGET:
    ///
    /// 1. Buscar todas as páginas que contêm widgets com EntityViewName = VIEW_TARGET (O(1) via índice invertido)
    /// 2. Para cada página encontrada, buscar recursivamente:
    ///    a. Páginas que têm widgets com FormUrl apontando para essa página (O(1) via índice invertido)
    /// 3. Evitar loops infinitos com HashSet de nós visitados
    ///
    /// FORMATO DE SAÍDA:
    /// - Título da Página > Widget [VIEW_NAME]
    /// - Página Pai > Widget Pai > Página > Widget [VIEW_NAME]
    /// </summary>
    public class ViewPathMapperService
    {
        private readonly ViewPathCacheService _cacheService = new();

        // Cache de páginas: chave = caminho do arquivo
        private Dictionary<string, PageInfo> _pagesCache = new();

        // Índice invertido: EntityViewName → lista de páginas que contêm essa view
        private Dictionary<string, List<PageInfo>> _pagesByEntityView = new(StringComparer.OrdinalIgnoreCase);

        // Índice invertido: FormUrl (limpa) → lista de páginas que têm widgets com esse FormUrl
        private Dictionary<string, List<PageInfo>> _pagesByFormUrl = new(StringComparer.OrdinalIgnoreCase);

        // Guarda de estado para evitar reinicialização desnecessária
        private bool _initialized = false;
        private string? _diretorioIndexado = null;

        /// <summary>
        /// Garante que o serviço está inicializado para o diretório informado.
        /// Se já foi inicializado para o mesmo diretório, não faz nada.
        /// </summary>
        public void EnsureInitialized(string diretorio)
        {
            var diretorioArtifacts = ViewPathProjectPathResolver.ResolveArtifactsRoot(diretorio);

            if (_initialized && _diretorioIndexado == diretorioArtifacts)
                return;

            var pastaPages = Path.Combine(diretorioArtifacts, "Pages");
            _pagesCache = _cacheService.GetPagesCache(pastaPages);

            BuildPageIndexes();

            _initialized = true;
            _diretorioIndexado = diretorioArtifacts;
        }

        /// <summary>
        /// Constrói os índices invertidos _pagesByEntityView e _pagesByFormUrl
        /// a partir do cache de páginas já carregado.
        /// </summary>
        private void BuildPageIndexes()
        {
            _pagesByEntityView = new Dictionary<string, List<PageInfo>>(StringComparer.OrdinalIgnoreCase);
            _pagesByFormUrl = new Dictionary<string, List<PageInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (_, pageInfo) in _pagesCache)
            {
                foreach (var widget in pageInfo.Widgets)
                {
                    // Índice por EntityViewName
                    if (!string.IsNullOrEmpty(widget.EntityViewName))
                    {
                        if (!_pagesByEntityView.TryGetValue(widget.EntityViewName, out var listByView))
                        {
                            listByView = new List<PageInfo>();
                            _pagesByEntityView[widget.EntityViewName] = listByView;
                        }
                        if (!listByView.Contains(pageInfo))
                            listByView.Add(pageInfo);
                    }

                    // Índice por FormUrl (normalizada)
                    if (!string.IsNullOrEmpty(widget.FormUrl))
                    {
                        var formUrlLimpa = LimparUrl(widget.FormUrl);
                        if (!string.IsNullOrEmpty(formUrlLimpa))
                        {
                            if (!_pagesByFormUrl.TryGetValue(formUrlLimpa, out var listByFormUrl))
                            {
                                listByFormUrl = new List<PageInfo>();
                                _pagesByFormUrl[formUrlLimpa] = listByFormUrl;
                            }
                            if (!listByFormUrl.Contains(pageInfo))
                                listByFormUrl.Add(pageInfo);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Mapeia os caminhos de navegação para uma coleção de views em lote.
        /// Chama EnsureInitialized uma única vez antes de processar todas as views.
        /// </summary>
        /// <param name="visoes">Nomes das views a mapear.</param>
        /// <param name="diretorio">Diretório raiz contendo a pasta Pages.</param>
        /// <returns>Dicionário: nome da view → lista de caminhos de navegação.</returns>
        public Dictionary<string, List<string>> MapearCaminhosEmLote(
            IReadOnlyCollection<string> visoes,
            string diretorio)
        {
            EnsureInitialized(diretorio);

            var resultado = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var visao in visoes)
            {
                var visitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var caminhos = BuscarCaminhosRecursivo(visao, visitados, new List<string>());

                caminhos = caminhos
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(c => c.Split('>').Length)
                    .ThenBy(c => c)
                    .ToList();

                resultado[visao] = caminhos;
            }

            return resultado;
        }

        /// <summary>
        /// Busca recursivamente todos os caminhos para uma view.
        /// </summary>
        private List<string> BuscarCaminhosRecursivo(
            string viewName,
            HashSet<string> visitados,
            List<string> caminhoAtual)
        {
            var resultado = new List<string>();

            var chaveVisita = $"VIEW:{viewName}";
            if (visitados.Contains(chaveVisita))
                return resultado;

            visitados.Add(chaveVisita);

            // Lookup O(1) via índice invertido
            var paginasComView = EncontrarPaginasComView(viewName);

            foreach (var pageInfo in paginasComView)
            {
                var novoCaminho = new List<string>(caminhoAtual);
                novoCaminho.Add($"[{viewName}]");

                var caminhosParaPagina = BuscarCaminhosParaPagina(pageInfo, visitados, novoCaminho);
                resultado.AddRange(caminhosParaPagina);
            }

            visitados.Remove(chaveVisita);
            return resultado;
        }

        /// <summary>
        /// Busca todos os caminhos que levam a uma página específica.
        /// </summary>
        private List<string> BuscarCaminhosParaPagina(
            PageInfo pageInfo,
            HashSet<string> visitados,
            List<string> caminhoAtual)
        {
            var resultado = new List<string>();
            var chaveVisita = $"PAGE:{pageInfo.Url}";

            if (visitados.Contains(chaveVisita))
                return resultado;

            visitados.Add(chaveVisita);

            // Buscar páginas pai via FormUrl (O(1) via índice invertido)
            var paginasComFormUrl = BuscarPaginasComFormUrl(pageInfo.Url);

            foreach (var (parentPageInfo, widgetTitle) in paginasComFormUrl)
            {
                var novoCaminho = new List<string>(caminhoAtual);

                if (!string.IsNullOrEmpty(widgetTitle))
                    novoCaminho.Insert(0, widgetTitle);

                var caminhosParaPaginaPai = BuscarCaminhosParaPagina(parentPageInfo, visitados, novoCaminho);
                resultado.AddRange(caminhosParaPaginaPai);
            }

            // Se não encontrou nenhum caminho pai, retornar o caminho atual com título da página
            if (resultado.Count == 0 && caminhoAtual.Count > 0)
            {
                var tituloPagina = pageInfo.Title;
                if (!string.IsNullOrEmpty(tituloPagina))
                {
                    var caminhoComTitulo = new List<string> { tituloPagina };
                    caminhoComTitulo.AddRange(caminhoAtual);
                    resultado.Add(string.Join(" > ", caminhoComTitulo));
                }
                else
                {
                    resultado.Add(string.Join(" > ", caminhoAtual));
                }
            }

            visitados.Remove(chaveVisita);
            return resultado;
        }

        /// <summary>
        /// Encontra todas as páginas que contêm um widget com EntityViewName especificado.
        /// Lookup O(1) via índice invertido _pagesByEntityView.
        /// </summary>
        private List<PageInfo> EncontrarPaginasComView(string viewName)
        {
            if (_pagesByEntityView.TryGetValue(viewName, out var paginas))
                return paginas;

            return new List<PageInfo>();
        }

        /// <summary>
        /// Busca páginas que têm widgets com FormUrl apontando para uma URL específica.
        /// Lookup O(1) via índice invertido _pagesByFormUrl.
        /// </summary>
        private List<(PageInfo PageInfo, string WidgetTitle)> BuscarPaginasComFormUrl(string targetUrl)
        {
            var resultado = new List<(PageInfo, string)>();
            var urlLimpa = LimparUrl(targetUrl);

            if (string.IsNullOrEmpty(urlLimpa))
                return resultado;

            if (!_pagesByFormUrl.TryGetValue(urlLimpa, out var paginas))
                return resultado;

            // Para cada página, encontrar o widget que tem esse FormUrl e retornar seu título
            foreach (var pageInfo in paginas)
            {
                foreach (var widget in pageInfo.Widgets)
                {
                    if (!string.IsNullOrEmpty(widget.FormUrl) &&
                        LimparUrl(widget.FormUrl).Equals(urlLimpa, StringComparison.OrdinalIgnoreCase))
                    {
                        resultado.Add((pageInfo, widget.Title));
                        break; // Um widget por página é suficiente
                    }
                }
            }

            return resultado;
        }

        /// <summary>
        /// Limpa uma URL removendo query strings, fragments e normalizando o formato.
        /// </summary>
        private string LimparUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            return url.Split('?')[0].Split('#')[0]
                .Replace("~/", "")
                .Replace("\\", "/")
                .ToLowerInvariant()
                .Trim();
        }
    }
}
