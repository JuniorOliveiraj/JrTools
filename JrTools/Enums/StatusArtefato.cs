namespace JrTools.Enums
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
        /// Retorna verdadeiro se o artefato precisa ser instalado ou atualizado (Novo ou Modificado).
        /// </summary>
        public static bool IsPendente(this StatusArtefato status) =>
            status == StatusArtefato.Novo || status == StatusArtefato.Modificado;

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
            if (statusStr.Equals("Equal", System.StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Instalado", System.StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Instalado;
            if (statusStr.Equals("FileOnly", System.StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Novo", System.StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Novo;
            if (statusStr.Equals("Diferent", System.StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Different", System.StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Modificado", System.StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Modificado;
            if (statusStr.Equals("DatabaseOnly", System.StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Excluido", System.StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Excluído", System.StringComparison.OrdinalIgnoreCase)) return StatusArtefato.Excluido;
            return StatusArtefato.NaoVerificado;
        }
    }
}
