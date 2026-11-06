using System;

namespace JrTools.Enums
{
    /// <summary>
    /// Representa os tipos de artefatos WES reconhecidos pelo sistema.
    /// </summary>
    public enum TipoArtefato
    {
        Outro = 0,
        Menu = 1,
        Page = 2,
        View = 3,
        Task = 4,
        Role = 5,
        Script = 6,
        Filter = 7,
        Template = 8,
        Widget = 9,
        DataSource = 10,
        DynamicQuery = 11
    }

    public static class TipoArtefatoExtensions
    {
        public static string ToNomePasta(this TipoArtefato tipo) => tipo switch
        {
            TipoArtefato.Menu => "Menus",
            TipoArtefato.Page => "Pages",
            TipoArtefato.View => "Views",
            TipoArtefato.Task => "Tasks",
            TipoArtefato.Role => "Roles",
            TipoArtefato.Script => "Scripts",
            TipoArtefato.Filter => "Filters",
            TipoArtefato.Template => "Templates",
            TipoArtefato.Widget => "Widgets",
            TipoArtefato.DataSource => "DataSources",
            TipoArtefato.DynamicQuery => "DynamicQueries",
            _ => "Outros"
        };

        public static string ToDescricao(this TipoArtefato tipo) => tipo switch
        {
            TipoArtefato.Menu => "Menus",
            TipoArtefato.Page => "Páginas",
            TipoArtefato.View => "Visões",
            TipoArtefato.Task => "Tarefas",
            TipoArtefato.Role => "Papéis",
            TipoArtefato.Script => "Scripts",
            TipoArtefato.Filter => "Filtros",
            TipoArtefato.Template => "Templates",
            TipoArtefato.Widget => "Widgets",
            TipoArtefato.DataSource => "Fontes de Dados",
            TipoArtefato.DynamicQuery => "Consultas Dinâmicas",
            _ => "Outros"
        };

        public static TipoArtefato ParseTipo(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return TipoArtefato.Outro;
            string s = nome.Trim();
            if (s.StartsWith("Menu", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Menu;
            if (s.StartsWith("Page", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Página", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Page;
            if (s.StartsWith("View", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Vis", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.View;
            if (s.StartsWith("Task", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Tarefa", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Task;
            if (s.StartsWith("Role", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Pap", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Role;
            if (s.StartsWith("Script", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Script;
            if (s.StartsWith("Filter", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Filtro", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Filter;
            if (s.StartsWith("Template", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Template;
            if (s.StartsWith("Widget", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.Widget;
            if (s.StartsWith("DataSource", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Fonte", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.DataSource;
            if (s.StartsWith("DynamicQuer", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Consulta", StringComparison.OrdinalIgnoreCase)) return TipoArtefato.DynamicQuery;
            return TipoArtefato.Outro;
        }
    }
}
