using System.Collections.Generic;
using System.IO;

namespace JrTools.Services
{
    /// <summary>
    /// Abstração sobre o sistema de arquivos para enumeração de arquivos em um diretório.
    /// Permite substituição por implementações fake em testes.
    /// </summary>
    public interface IDirectoryReader
    {
        /// <summary>
        /// Enumera os arquivos presentes no diretório informado.
        /// </summary>
        /// <param name="diretorio">Caminho completo do diretório a ser lido.</param>
        /// <returns>Sequência de <see cref="FileInfo"/> para cada arquivo encontrado.</returns>
        IEnumerable<FileInfo> EnumerarArquivos(string diretorio);
    }
}
