using System;
using System.IO;

namespace JrTools.Dto
{
    public class BinarioDelphiItem
    {
        /// <summary>Nome do arquivo com extensão (ex.: "BennerRH.exe").</summary>
        public string Nome { get; init; } = string.Empty;

        /// <summary>Extensão normalizada em lowercase (ex.: ".exe").</summary>
        public string Extensao { get; init; } = string.Empty;

        /// <summary>Tamanho em bytes.</summary>
        public long TamanhoBytes { get; init; }

        /// <summary>Tamanho formatado para exibição (ex.: "1,2 MB").</summary>
        public string TamanhoFormatado { get; init; } = string.Empty;

        /// <summary>Data da última modificação.</summary>
        public DateTime DataModificacao { get; init; }

        /// <summary>Caminho completo do arquivo.</summary>
        public string CaminhoCompleto { get; init; } = string.Empty;

        /// <summary>Nome normalizado em lowercase — chave do índice.</summary>
        public string NomeNormalizado { get; init; } = string.Empty;

        /// <summary>Data de modificação formatada para exibição (ex.: "01/01/2024 10:30").</summary>
        public string DataModificacaoFormatada => DataModificacao.ToString("dd/MM/yyyy HH:mm");

        /// <summary>
        /// Cria um <see cref="BinarioDelphiItem"/> a partir de um <see cref="FileInfo"/>.
        /// </summary>
        public static BinarioDelphiItem FromFileInfo(FileInfo fileInfo)
        {
            var tamanhoBytes = fileInfo.Length;
            return new BinarioDelphiItem
            {
                Nome = fileInfo.Name,
                Extensao = fileInfo.Extension.ToLowerInvariant(),
                TamanhoBytes = tamanhoBytes,
                TamanhoFormatado = FormatarTamanho(tamanhoBytes),
                DataModificacao = fileInfo.LastWriteTime,
                CaminhoCompleto = fileInfo.FullName,
                NomeNormalizado = fileInfo.Name.ToLowerInvariant()
            };
        }

        /// <summary>
        /// Formata o tamanho em bytes para exibição legível.
        /// </summary>
        private static string FormatarTamanho(long bytes)
        {
            const long kb = 1024;
            const long mb = 1024 * 1024;

            if (bytes < kb)
                return $"{bytes} B";
            if (bytes < mb)
                return $"{bytes / (double)kb:N1} KB";
            return $"{bytes / (double)mb:N1} MB";
        }
    }
}
