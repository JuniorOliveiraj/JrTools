using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using JrTools.Services;
using JrTools.Tests.Helpers;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Testes de propriedade para <see cref="ViewPathCacheService"/>.
    /// </summary>
    [Properties(MaxTest = 100)]
    public class ViewPathCacheServiceTests
    {
        // -----------------------------------------------------------------------
        // Geradores auxiliares
        // -----------------------------------------------------------------------

        /// <summary>
        /// Gera uma string não vazia e não nula para usar como identificador.
        /// Filtra caracteres de controle e caracteres problemáticos para XML/filenames.
        /// </summary>
        private static Gen<string> GenNonEmptyString() =>
            Arb.Generate<NonEmptyString>()
                .Select(nes => nes.Get)
                .Select(s => new string(s.Where(c => 
                    !char.IsControl(c) && 
                    c != '<' && c != '>' && c != '"' && c != '\'' && 
                    c != '&' && c != '/' && c != '\\' && c != ':' && 
                    c != '*' && c != '?' && c != '|').ToArray()))
                .Where(s => !string.IsNullOrWhiteSpace(s));

        /// <summary>
        /// Gera dados de widget com pelo menos EntityViewName ou FormUrl não vazio.
        /// </summary>
        private static Gen<XmlPageBuilder.WidgetData> GenWidgetData() =>
            Gen.OneOf(
                // Widget com EntityViewName apenas
                GenNonEmptyString().Select(viewName => new XmlPageBuilder.WidgetData
                {
                    Title = $"Widget_{viewName}",
                    EntityViewName = viewName,
                    FormUrl = null
                }),
                // Widget com FormUrl apenas
                GenNonEmptyString().Select(formUrl => new XmlPageBuilder.WidgetData
                {
                    Title = $"Widget_{formUrl}",
                    EntityViewName = null,
                    FormUrl = formUrl
                }),
                // Widget com ambos
                GenNonEmptyString().SelectMany(viewName =>
                    GenNonEmptyString().Select(formUrl => new XmlPageBuilder.WidgetData
                    {
                        Title = $"Widget_{viewName}",
                        EntityViewName = viewName,
                        FormUrl = formUrl
                    }))
            );

        /// <summary>
        /// Gera uma página XML completa com Url, Title e widgets variados.
        /// </summary>
        private static Gen<(string Url, string Title, List<XmlPageBuilder.WidgetData> Widgets, string Xml)> GenPageXml() =>
            GenNonEmptyString().SelectMany(url =>
                GenNonEmptyString().SelectMany(title =>
                    Gen.ListOf(GenWidgetData()).Select(widgets =>
                    {
                        var widgetList = widgets.ToList();
                        var builder = new XmlPageBuilder()
                            .WithUrl(url)
                            .WithTitle(title)
                            .AddWidgets(widgetList);
                        
                        var xml = builder.Build();
                        return (url, title, widgetList, xml);
                    })));

        /// <summary>
        /// Gera uma lista de páginas XML com URLs únicos (case-insensitive).
        /// </summary>
        private static Gen<List<(string Url, string Title, List<XmlPageBuilder.WidgetData> Widgets, string Xml)>> GenUniquePageXmls() =>
            Gen.NonEmptyListOf(GenPageXml())
                .Select(pages =>
                {
                    // Garantir URLs únicos (case-insensitive)
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var uniquePages = new List<(string Url, string Title, List<XmlPageBuilder.WidgetData> Widgets, string Xml)>();
                    
                    foreach (var page in pages)
                    {
                        if (seen.Add(page.Url))
                        {
                            uniquePages.Add(page);
                        }
                    }
                    
                    return uniquePages;
                })
                .Where(pages => pages.Count > 0);

        // -----------------------------------------------------------------------
        // Helpers para criar arquivos temporários
        // -----------------------------------------------------------------------

        private static (string tempDir, List<string> arquivos) CriarArquivosXml(
            List<(string Url, string Title, List<XmlPageBuilder.WidgetData> Widgets, string Xml)> pages)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "JrToolsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var arquivos = new List<string>();
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                // Sanitize the URL to create a valid filename - just use the index
                var fileName = $"page_{i}.xml";
                var filePath = Path.Combine(tempDir, fileName);
                File.WriteAllText(filePath, page.Xml);
                arquivos.Add(filePath);
            }

            return (tempDir, arquivos);
        }

        private static void LimparDiretorioTemp(string tempDir)
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { /* ignora erros de limpeza */ }
        }

        // -----------------------------------------------------------------------
        // Property 8: Parsing de XMLs extrai campos corretos
        // Validates: Requirements 10.1, 10.2, 10.3, 10.4
        // -----------------------------------------------------------------------

        // Feature: view-path-explorer, Property 8: Parsing de XMLs extrai campos corretos
        [Property]
        public Property Property8_ParsingDeXmlsExtraiCamposCorretos()
        {
            return Prop.ForAll(
                GenUniquePageXmls().ToArbitrary(),
                pages =>
                {
                    var (tempDir, _) = CriarArquivosXml(pages);
                    try
                    {
                        var service = new ViewPathCacheService();
                        var cache = service.GetPagesCache(tempDir);

                        // Verificar que temos o número correto de páginas
                        if (cache.Count != pages.Count)
                            return false.ToProperty().Label($"Expected {pages.Count} pages, got {cache.Count}");

                        // Para cada página gerada, verificar que os campos foram extraídos corretamente
                        foreach (var (url, title, widgets, _) in pages)
                        {
                            // Encontrar a PageInfo correspondente no cache
                            var pageInfo = cache.Values.FirstOrDefault(p => 
                                p.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

                            if (pageInfo == null)
                                return false.ToProperty().Label($"Page with URL '{url}' not found in cache");

                            // Verificar Url
                            if (!pageInfo.Url.Equals(url, StringComparison.OrdinalIgnoreCase))
                                return false.ToProperty().Label($"URL mismatch: expected '{url}', got '{pageInfo.Url}'");

                            // Verificar Title
                            if (!pageInfo.Title.Equals(title, StringComparison.Ordinal))
                                return false.ToProperty().Label($"Title mismatch: expected '{title}', got '{pageInfo.Title}'");

                            // Verificar Widgets - apenas widgets com EntityViewName ou FormUrl não vazios devem estar presentes
                            var expectedWidgets = widgets.Where(w => 
                                !string.IsNullOrEmpty(w.EntityViewName) || !string.IsNullOrEmpty(w.FormUrl)).ToList();

                            if (pageInfo.Widgets.Count != expectedWidgets.Count)
                                return false.ToProperty().Label(
                                    $"Widget count mismatch for page '{url}': expected {expectedWidgets.Count}, got {pageInfo.Widgets.Count}");

                            // Verificar que todos os widgets retornados têm EntityViewName ou FormUrl não vazio
                            foreach (var widget in pageInfo.Widgets)
                            {
                                if (string.IsNullOrEmpty(widget.EntityViewName) && string.IsNullOrEmpty(widget.FormUrl))
                                    return false.ToProperty().Label(
                                        $"Widget '{widget.Title}' has both EntityViewName and FormUrl empty");
                            }

                            // Verificar que cada widget esperado está presente
                            foreach (var expectedWidget in expectedWidgets)
                            {
                                var foundWidget = pageInfo.Widgets.FirstOrDefault(w =>
                                    w.Title.Equals(expectedWidget.Title, StringComparison.Ordinal));

                                if (foundWidget == null)
                                    return false.ToProperty().Label(
                                        $"Widget '{expectedWidget.Title}' not found in page '{url}'");

                                // Verificar EntityViewName
                                var expectedViewName = expectedWidget.EntityViewName ?? string.Empty;
                                if (!foundWidget.EntityViewName.Equals(expectedViewName, StringComparison.Ordinal))
                                    return false.ToProperty().Label(
                                        $"EntityViewName mismatch for widget '{expectedWidget.Title}': expected '{expectedViewName}', got '{foundWidget.EntityViewName}'");

                                // Verificar FormUrl
                                var expectedFormUrl = expectedWidget.FormUrl ?? string.Empty;
                                if (!foundWidget.FormUrl.Equals(expectedFormUrl, StringComparison.Ordinal))
                                    return false.ToProperty().Label(
                                        $"FormUrl mismatch for widget '{expectedWidget.Title}': expected '{expectedFormUrl}', got '{foundWidget.FormUrl}'");
                            }
                        }

                        return true.ToProperty();
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 9: Reindexação do mesmo diretório produz índices equivalentes
        // Validates: Requirement 10.5
        // -----------------------------------------------------------------------

        // Feature: view-path-explorer, Property 9: Reindexação do mesmo diretório produz índices equivalentes
        [Property]
        public Property Property9_ReindexacaoProducIndicesEquivalentes()
        {
            return Prop.ForAll(
                GenUniquePageXmls().ToArbitrary(),
                pages =>
                {
                    var (tempDir, _) = CriarArquivosXml(pages);
                    try
                    {
                        // Instanciar dois ViewPathCacheService independentes
                        var service1 = new ViewPathCacheService();
                        var service2 = new ViewPathCacheService();

                        // Chamar GetPagesCache em ambos com o mesmo diretório temporário
                        var cache1 = service1.GetPagesCache(tempDir);
                        var cache2 = service2.GetPagesCache(tempDir);

                        // Verificar que as chaves são equivalentes
                        if (cache1.Count != cache2.Count)
                            return false.ToProperty().Label($"Cache count mismatch: cache1 has {cache1.Count} entries, cache2 has {cache2.Count} entries");

                        var keys1 = cache1.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
                        var keys2 = cache2.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

                        for (int i = 0; i < keys1.Count; i++)
                        {
                            if (!keys1[i].Equals(keys2[i], StringComparison.OrdinalIgnoreCase))
                                return false.ToProperty().Label($"Key mismatch at index {i}: '{keys1[i]}' vs '{keys2[i]}'");
                        }

                        // Verificar que os valores são equivalentes
                        foreach (var key in keys1)
                        {
                            var page1 = cache1[key];
                            var page2 = cache2[key];

                            // Verificar Url
                            if (!page1.Url.Equals(page2.Url, StringComparison.OrdinalIgnoreCase))
                                return false.ToProperty().Label($"URL mismatch for key '{key}': '{page1.Url}' vs '{page2.Url}'");

                            // Verificar Title
                            if (!page1.Title.Equals(page2.Title, StringComparison.Ordinal))
                                return false.ToProperty().Label($"Title mismatch for key '{key}': '{page1.Title}' vs '{page2.Title}'");

                            // Verificar Widgets count
                            if (page1.Widgets.Count != page2.Widgets.Count)
                                return false.ToProperty().Label(
                                    $"Widget count mismatch for key '{key}': {page1.Widgets.Count} vs {page2.Widgets.Count}");

                            // Verificar cada widget
                            for (int i = 0; i < page1.Widgets.Count; i++)
                            {
                                var widget1 = page1.Widgets[i];
                                var widget2 = page2.Widgets[i];

                                if (!widget1.Title.Equals(widget2.Title, StringComparison.Ordinal))
                                    return false.ToProperty().Label(
                                        $"Widget title mismatch at index {i} for key '{key}': '{widget1.Title}' vs '{widget2.Title}'");

                                if (!widget1.EntityViewName.Equals(widget2.EntityViewName, StringComparison.Ordinal))
                                    return false.ToProperty().Label(
                                        $"Widget EntityViewName mismatch at index {i} for key '{key}': '{widget1.EntityViewName}' vs '{widget2.EntityViewName}'");

                                if (!widget1.FormUrl.Equals(widget2.FormUrl, StringComparison.Ordinal))
                                    return false.ToProperty().Label(
                                        $"Widget FormUrl mismatch at index {i} for key '{key}': '{widget1.FormUrl}' vs '{widget2.FormUrl}'");
                            }
                        }

                        return true.ToProperty();
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        // -----------------------------------------------------------------------
        // Property 11: XMLs inválidos não interrompem a indexação
        // Validates: Requirement 7.1
        // -----------------------------------------------------------------------

        /// <summary>
        /// Gera um par de inteiros (válidos, inválidos) onde ambos >= 0 e a soma > 0.
        /// </summary>
        private static Gen<(int validos, int invalidos)> GenValidosInvalidos() =>
            Gen.Choose(0, 20).SelectMany(validos =>
                Gen.Choose(0, 20).Select(invalidos => (validos, invalidos)))
               .Where(tuple => tuple.validos + tuple.invalidos > 0);

        // Feature: view-path-explorer, Property 11: XMLs inválidos não interrompem a indexação
        [Property]
        public Property Property11_XmlsInvalidosNaoInterrompemIndexacao()
        {
            return Prop.ForAll(
                GenValidosInvalidos().ToArbitrary(),
                tuple =>
                {
                    var (validos, invalidos) = tuple;
                    
                    var tempDir = Path.Combine(Path.GetTempPath(), "JrToolsTests_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        // Criar XMLs válidos
                        var pagesValidas = new List<(string Url, string Title, List<XmlPageBuilder.WidgetData> Widgets, string Xml)>();
                        for (int i = 0; i < validos; i++)
                        {
                            var url = $"ValidPage_{i}";
                            var title = $"Valid Page {i}";
                            var widgets = new List<XmlPageBuilder.WidgetData>
                            {
                                new XmlPageBuilder.WidgetData
                                {
                                    Title = $"Widget_{i}",
                                    EntityViewName = $"IWView_{i}",
                                    FormUrl = null
                                }
                            };
                            var xml = new XmlPageBuilder()
                                .WithUrl(url)
                                .WithTitle(title)
                                .AddWidgets(widgets)
                                .Build();
                            
                            pagesValidas.Add((url, title, widgets, xml));
                            
                            var filePath = Path.Combine(tempDir, $"valid_{i}.xml");
                            File.WriteAllText(filePath, xml);
                        }

                        // Criar arquivos inválidos - usar conteúdo que realmente falha no parse XML
                        var invalidContents = new[]
                        {
                            "<?xml version=\"1.0\"?><Page><Url>test", // XML incompleto - tag não fechada
                            "<?xml version=\"1.0\"?><Page><Url>test</Url><Title>Test", // Tags não fechadas
                            "This is not XML at all!", // Não é XML
                            "", // Vazio
                            "<?xml version=\"1.0\"?><Page>\x00\x01\x02</Page>", // Caracteres inválidos
                            "<<<<<invalid", // Completamente malformado
                            "<?xml version=\"1.0\"?><Page><Url>test</Url></Page><ExtraRoot>", // Múltiplas raízes
                        };

                        for (int i = 0; i < invalidos; i++)
                        {
                            // Usar diferentes tipos de conteúdo inválido de forma cíclica
                            var conteudoInvalido = invalidContents[i % invalidContents.Length];
                            var filePath = Path.Combine(tempDir, $"invalid_{i}.xml");
                            File.WriteAllText(filePath, conteudoInvalido);
                        }

                        // Executar GetPagesCache - não deve lançar exceção
                        var service = new ViewPathCacheService();
                        var cache = service.GetPagesCache(tempDir);

                        // Verificar que retorna exatamente 'validos' entradas
                        if (cache.Count != validos)
                            return false.ToProperty().Label(
                                $"Expected {validos} valid entries, got {cache.Count}. " +
                                $"(validos={validos}, invalidos={invalidos})");

                        // Verificar que todas as páginas válidas estão presentes
                        foreach (var (url, title, widgets, _) in pagesValidas)
                        {
                            var pageInfo = cache.Values.FirstOrDefault(p => 
                                p.Url.Equals(url, StringComparison.OrdinalIgnoreCase));

                            if (pageInfo == null)
                                return false.ToProperty().Label(
                                    $"Valid page with URL '{url}' not found in cache. " +
                                    $"(validos={validos}, invalidos={invalidos})");

                            // Verificação básica de que os dados foram extraídos corretamente
                            if (!pageInfo.Title.Equals(title, StringComparison.Ordinal))
                                return false.ToProperty().Label(
                                    $"Title mismatch for page '{url}': expected '{title}', got '{pageInfo.Title}'");
                        }

                        return true.ToProperty();
                    }
                    catch (Exception ex)
                    {
                        // Se GetPagesCache lançar exceção, a propriedade falha
                        return false.ToProperty().Label(
                            $"GetPagesCache threw exception with validos={validos}, invalidos={invalidos}: {ex.Message}");
                    }
                    finally
                    {
                        LimparDiretorioTemp(tempDir);
                    }
                });
        }

        [Fact]
        public void GetPagesCache_QuandoDiretorioMuda_RecarregaCache()
        {
            var dir1 = Path.Combine(Path.GetTempPath(), "JrToolsTests_" + Guid.NewGuid().ToString("N"));
            var dir2 = Path.Combine(Path.GetTempPath(), "JrToolsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);

            try
            {
                File.WriteAllText(
                    Path.Combine(dir1, "page1.xml"),
                    XmlPageBuilder.CreateDefault()
                        .WithUrl("Page1")
                        .WithTitle("Page 1")
                        .AddWidget("Widget 1", entityViewName: "IWPage1")
                        .Build());

                File.WriteAllText(
                    Path.Combine(dir2, "page2.xml"),
                    XmlPageBuilder.CreateDefault()
                        .WithUrl("Page2")
                        .WithTitle("Page 2")
                        .AddWidget("Widget 2", entityViewName: "IWPage2")
                        .Build());

                var service = new ViewPathCacheService();

                var cache1 = service.GetPagesCache(dir1);
                var cache2 = service.GetPagesCache(dir2);

                Assert.Contains(cache1.Values, page => page.Url == "Page1");
                Assert.DoesNotContain(cache2.Values, page => page.Url == "Page1");
                Assert.Contains(cache2.Values, page => page.Url == "Page2");
            }
            finally
            {
                LimparDiretorioTemp(dir1);
                LimparDiretorioTemp(dir2);
            }
        }
    }
}
