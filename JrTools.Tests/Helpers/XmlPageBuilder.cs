using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Helper para gerar XMLs de página válidos em memória para testes de propriedade.
    /// Gera XMLs com estrutura: &lt;Url&gt;, &lt;Title&gt;, &lt;PageWidgetPortable&gt; com &lt;XmlAttributes&gt;
    /// contendo EntityViewName e/ou FormUrl.
    /// </summary>
    public class XmlPageBuilder
    {
        private string _url = string.Empty;
        private string _title = string.Empty;
        private readonly List<WidgetData> _widgets = new();

        public class WidgetData
        {
            public string Title { get; set; } = string.Empty;
            public string? EntityViewName { get; set; }
            public string? FormUrl { get; set; }
        }

        public XmlPageBuilder WithUrl(string url)
        {
            _url = url;
            return this;
        }

        public XmlPageBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public XmlPageBuilder AddWidget(string title, string? entityViewName = null, string? formUrl = null)
        {
            _widgets.Add(new WidgetData
            {
                Title = title,
                EntityViewName = entityViewName,
                FormUrl = formUrl
            });
            return this;
        }

        public XmlPageBuilder AddWidgets(IEnumerable<WidgetData> widgets)
        {
            _widgets.AddRange(widgets);
            return this;
        }

        /// <summary>
        /// Constrói o XML da página com a estrutura esperada pelo ViewPathCacheService.
        /// </summary>
        public string Build()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Page xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">");
            
            // Url e Title
            sb.AppendLine($"  <Url>{EscapeXml(_url)}</Url>");
            sb.AppendLine($"  <Title>{EscapeXml(_title)}</Title>");
            
            // Widgets
            if (_widgets.Any())
            {
                sb.AppendLine("  <Widgets>");
                foreach (var widget in _widgets)
                {
                    sb.AppendLine("    <PageWidgetPortable>");
                    sb.AppendLine($"      <Title>{EscapeXml(widget.Title)}</Title>");
                    
                    // XmlAttributes contendo EntityViewName e/ou FormUrl
                    var xmlAttrs = BuildXmlAttributes(widget);
                    sb.AppendLine($"      <XmlAttributes>{EscapeXml(xmlAttrs)}</XmlAttributes>");
                    
                    sb.AppendLine("    </PageWidgetPortable>");
                }
                sb.AppendLine("  </Widgets>");
            }
            
            sb.AppendLine("</Page>");
            return sb.ToString();
        }

        private string BuildXmlAttributes(WidgetData widget)
        {
            var parts = new List<string>();
            
            if (!string.IsNullOrEmpty(widget.EntityViewName))
            {
                parts.Add($"<EntityViewName>{widget.EntityViewName}</EntityViewName>");
            }
            
            if (!string.IsNullOrEmpty(widget.FormUrl))
            {
                parts.Add($"<FormUrl>{widget.FormUrl}</FormUrl>");
            }
            
            return string.Join("", parts);
        }

        private string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Cria um builder com valores padrão para testes rápidos.
        /// </summary>
        public static XmlPageBuilder CreateDefault()
        {
            return new XmlPageBuilder()
                .WithUrl("TestPage")
                .WithTitle("Test Page Title");
        }
    }
}
