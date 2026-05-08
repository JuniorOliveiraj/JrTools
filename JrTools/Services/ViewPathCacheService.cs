using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JrTools.Services
{
    /// <summary>
    /// Serviço de cache para carregar e armazenar informações de páginas e views.
    ///
    /// RESPONSABILIDADES:
    /// 1. Carregar XMLs de páginas (/Pages/*.xml) e extrair:
    ///    - URL da página
    ///    - Título da página (extraído durante o parse inicial)
    ///    - Widgets com EntityViewName e/ou FormUrl
    ///
    /// 2. Carregar XMLs de views (/Views/*.20.xml) e extrair:
    ///    - Nome da view
    ///    - Comandos com URL (excluindo CommandGroups)
    ///    - Comandos aninhados dentro de CommandGroups com GroupTitle
    ///
    /// IMPORTANTE - CommandGroups:
    /// - CommandGroups têm xsi:type="CommandGroup" e elemento &lt;Items&gt;
    /// - CommandGroups NÃO devem ser adicionados como comandos (não têm URL válida)
    /// - Apenas os comandos DENTRO do CommandGroup devem ser adicionados, com GroupTitle preenchido
    ///
    /// CACHE:
    /// - Carrega os XMLs uma vez e mantém em memória
    /// - Retorna o mesmo cache em chamadas subsequentes
    /// </summary>
    public class ViewPathCacheService
    {
        private Dictionary<string, PageInfo>? _pagesCache;
        private Dictionary<string, ViewInfo>? _viewsCache;
        private string? _pagesCachePath;
        private string? _viewsCachePath;

        private static readonly Regex _entityViewNameRegex =
            new(@"<EntityViewName>([^<]+)</EntityViewName>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _formUrlRegex =
            new(@"<FormUrl>([^<]+)</FormUrl>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Informações de uma página extraídas do XML.
        /// </summary>
        public class PageInfo
        {
            public string Url { get; set; } = string.Empty;
            /// <summary>Título da página extraído durante o parse inicial.</summary>
            public string Title { get; set; } = string.Empty;
            public List<WidgetInfo> Widgets { get; set; } = new();
        }

        /// <summary>
        /// Informações de um widget dentro de uma página.
        /// </summary>
        public class WidgetInfo
        {
            public string Title { get; set; } = string.Empty;
            public string EntityViewName { get; set; } = string.Empty;
            /// <summary>FormUrl extraído uma única vez via regex durante o parse.</summary>
            public string FormUrl { get; set; } = string.Empty;
        }

        /// <summary>
        /// Informações de uma view extraídas do XML.
        /// </summary>
        public class ViewInfo
        {
            public string ViewName { get; set; } = string.Empty;
            public List<CommandInfo> Commands { get; set; } = new();
        }

        /// <summary>
        /// Informações de um comando dentro de uma view.
        /// </summary>
        public class CommandInfo
        {
            public string Title { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            /// <summary>
            /// Título do grupo pai (CommandGroup), se o comando estiver dentro de um grupo.
            /// Exemplo: Se o comando "Riscos" está dentro do grupo "Quadro", GroupTitle = "Quadro"
            /// </summary>
            public string? GroupTitle { get; set; }
        }

        /// <summary>
        /// Carrega e cacheia informações de todas as páginas.
        /// Extrai URL, título da página e widgets com EntityViewName e/ou FormUrl.
        /// Mantém apenas widgets que possuem EntityViewName ou FormUrl não vazios.
        /// </summary>
        public Dictionary<string, PageInfo> GetPagesCache(string pastaPages)
        {
            if (_pagesCache != null && string.Equals(_pagesCachePath, pastaPages, StringComparison.OrdinalIgnoreCase))
                return _pagesCache;

            _pagesCache = new Dictionary<string, PageInfo>(StringComparer.OrdinalIgnoreCase);
            _pagesCachePath = pastaPages;

            if (!Directory.Exists(pastaPages))
                return _pagesCache;

            _pagesCache = Directory.EnumerateFiles(pastaPages, "*.xml")
                .Select(arquivo =>
                {
                    try
                    {
                        var doc = XDocument.Load(arquivo);

                        var url = doc.Descendants()
                                     .FirstOrDefault(e => e.Name.LocalName == "Url")?.Value
                                  ?? Path.GetFileNameWithoutExtension(arquivo);

                        var title = doc.Descendants()
                                       .FirstOrDefault(e => e.Name.LocalName == "Title")?.Value
                                   ?? string.Empty;

                        var widgets = doc.Descendants()
                            .Where(e => e.Name.LocalName == "PageWidgetPortable")
                            .Select(w =>
                            {
                                var widgetTitle = w.Element(XName.Get("Title", w.Name.NamespaceName))?.Value ?? "";
                                var xmlAttrs = w.Element(XName.Get("XmlAttributes", w.Name.NamespaceName))?.Value ?? "";

                                var viewNameMatch = _entityViewNameRegex.Match(xmlAttrs);
                                var viewName = viewNameMatch.Success ? viewNameMatch.Groups[1].Value : "";

                                var formUrlMatch = _formUrlRegex.Match(xmlAttrs);
                                var formUrl = formUrlMatch.Success ? formUrlMatch.Groups[1].Value : "";

                                return new WidgetInfo
                                {
                                    Title = widgetTitle,
                                    EntityViewName = viewName,
                                    FormUrl = formUrl
                                };
                            })
                            .Where(w => !string.IsNullOrEmpty(w.EntityViewName) || !string.IsNullOrEmpty(w.FormUrl))
                            .ToList();

                        return new { Arquivo = arquivo, PageInfo = new PageInfo { Url = url, Title = title, Widgets = widgets } };
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x != null)
                .ToDictionary(x => x!.Arquivo, x => x!.PageInfo, StringComparer.OrdinalIgnoreCase);

            return _pagesCache;
        }

        /// <summary>
        /// Carrega e cacheia informações de todas as views.
        /// Extrai comandos com URL, excluindo CommandGroups.
        /// Para comandos dentro de CommandGroups, preenche o GroupTitle.
        ///
        /// LÓGICA DE PARSING:
        /// 1. Para cada elemento &lt;Command&gt;:
        ///    a. Verifica se tem xsi:type="CommandGroup"
        ///    b. Se SIM e tem &lt;Items&gt;:
        ///       - Processa apenas os comandos aninhados
        ///       - Preenche GroupTitle com o título do grupo
        ///    c. Se NÃO é CommandGroup:
        ///       - Adiciona o comando normalmente (sem GroupTitle)
        ///
        /// PROBLEMA A EVITAR:
        /// - NÃO adicionar o CommandGroup como comando (ele não tem URL válida)
        /// - Isso causava duplicatas: "Comando Riscos" e "Grupo Quadro > Comando Riscos"
        ///
        /// OTIMIZAÇÃO:
        /// - Só adiciona views que realmente têm comandos (ignora views vazias)
        /// </summary>
        public Dictionary<string, ViewInfo> GetViewsCache(string pastaViews)
        {
            if (_viewsCache != null && string.Equals(_viewsCachePath, pastaViews, StringComparison.OrdinalIgnoreCase))
                return _viewsCache;

            if (!Directory.Exists(pastaViews))
            {
                _viewsCache = new Dictionary<string, ViewInfo>(StringComparer.OrdinalIgnoreCase);
                _viewsCachePath = pastaViews;
                return _viewsCache;
            }

            _viewsCachePath = pastaViews;
            _viewsCache = Directory.EnumerateFiles(pastaViews, "*.20.xml")
                .AsParallel()
                .Select(arquivo =>
                {
                    try
                    {
                        var doc = XDocument.Load(arquivo);
                        var viewName = doc.Descendants()
                            .FirstOrDefault(e => e.Name.LocalName == "Name")?.Value ?? "";

                        if (string.IsNullOrEmpty(viewName))
                            return null;

                        var commands = ParseCommands(doc).ToList();

                        return commands.Any()
                            ? new { ViewName = viewName, ViewInfo = new ViewInfo { ViewName = viewName, Commands = commands } }
                            : null;
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(x => x != null)
                .ToDictionary(x => x!.ViewName, x => x!.ViewInfo, StringComparer.OrdinalIgnoreCase);

            return _viewsCache;
        }

        private IEnumerable<CommandInfo> ParseCommands(XDocument doc)
        {
            var commandsElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Commands");
            if (commandsElement == null)
                return Enumerable.Empty<CommandInfo>();

            return commandsElement.Elements()
                .Where(e => e.Name.LocalName == "Command")
                .SelectMany(cmd =>
                {
                    var title = cmd.Attribute("title")?.Value ?? "";
                    var xsiType = cmd.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"))?.Value;
                    var isCommandGroup = xsiType == "CommandGroup";
                    var items = cmd.Element(XName.Get("Items", cmd.Name.NamespaceName));

                    if (items != null && isCommandGroup)
                    {
                        return ParseNestedCommands(items, title);
                    }

                    if (!isCommandGroup)
                    {
                        var outputDef = cmd.Element(XName.Get("OutputDefinition", cmd.Name.NamespaceName));
                        if (outputDef != null)
                        {
                            var url = outputDef.Element(XName.Get("Url", outputDef.Name.NamespaceName))?.Value ?? "";
                            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
                            {
                                return new[] { new CommandInfo { Title = title, Url = url } };
                            }
                        }
                    }

                    return Enumerable.Empty<CommandInfo>();
                });
        }

        private IEnumerable<CommandInfo> ParseNestedCommands(XElement items, string groupTitle)
        {
            return items.Elements()
                .Where(e => e.Name.LocalName == "Command")
                .Select(nestedCmd =>
                {
                    var nestedTitle = nestedCmd.Attribute("title")?.Value ?? "";
                    var nestedOutputDef = nestedCmd.Element(XName.Get("OutputDefinition", nestedCmd.Name.NamespaceName));

                    if (nestedOutputDef != null)
                    {
                        var nestedUrl = nestedOutputDef.Element(XName.Get("Url", nestedOutputDef.Name.NamespaceName))?.Value ?? "";
                        if (!string.IsNullOrEmpty(nestedUrl) && !string.IsNullOrEmpty(nestedTitle))
                        {
                            return new CommandInfo
                            {
                                Title = nestedTitle,
                                Url = nestedUrl,
                                GroupTitle = !string.IsNullOrEmpty(groupTitle) ? groupTitle : null
                            };
                        }
                    }
                    return null;
                })
                .Where(cmd => cmd != null)
                .Select(cmd => cmd!);
        }
    }
}
