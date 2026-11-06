using System;

namespace JrTools.Dto
{
    /// <summary>
    /// Representa os possíveis status de sincronização de um artefato entre o disco e o banco de dados.
    /// </summary>
    public enum StatusArtefato
    {
        NaoVerificado = 0,
        Instalado = 1,     // "Equal"
        Novo = 2,          // "FileOnly"
        Modificado = 3,    // "Diferent" / "Different"
        Excluido = 4       // "DatabaseOnly"
    }

    public static class StatusArtefatoExtensions
    {
        /// <summary>
        /// Retorna verdadeiro se o artefato precisa ser instalado ou atualizado (Novo , Modificado e Excluido).
        /// </summary>
        public static bool IsPendente(this StatusArtefato status) =>
            status == StatusArtefato.Novo || status == StatusArtefato.Modificado || status == StatusArtefato.Excluido;

        /// <summary>
        /// Retorna a descrição amigável para exibição na interface.
        /// </summary>
        public static string ToDescricao(this StatusArtefato status) => status switch
        {
            StatusArtefato.Novo => "Novo",
            StatusArtefato.Modificado => "Modificado",
            StatusArtefato.Instalado => "Instalado",
            StatusArtefato.Excluido => "Excluído",
            _ => "Não Verificado"
        };

        /// <summary>
        /// Converte com segurança a string retornada pelo comparador para o enum fortemente tipado.
        /// </summary>
        public static StatusArtefato ParseStatus(string? statusStr)
        {
            if (string.IsNullOrWhiteSpace(statusStr)) return StatusArtefato.NaoVerificado;
            if (statusStr.Equals("Equal", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Instalado", StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Instalado;
            if (statusStr.Equals("FileOnly", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Novo", StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Novo;
            if (statusStr.Equals("Diferent", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Different", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Modificado", StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Modificado;
            if (statusStr.Equals("DatabaseOnly", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Excluido", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Excluído", StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Excluido;
            return StatusArtefato.NaoVerificado;
        }
    }

    public class ArtefatoDto
    {
        public string Identificador { get; set; } = string.Empty;
        public string NomeArquivo { get; set; } = string.Empty;
        public string Guia { get; set; } = string.Empty; // ex: Menus, Pages, Views
        public string Camada { get; set; } = string.Empty; // ex: 20, 30, 40, 50
        public string CaminhoCompleto { get; set; } = string.Empty;
        public StatusArtefato Status { get; set; } = StatusArtefato.NaoVerificado;
        public bool IsSelecionado { get; set; }

        public bool IsPendente => Status.IsPendente();
        public string StatusFormatado => Status.ToDescricao();

        public string CamadaFormatada => $"Camada {Camada}";
        public string DescricaoCompleta => $"{Identificador} [{Guia}] [{StatusFormatado}] (Camada {Camada})";
    }
}
