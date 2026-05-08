using System.Collections.Generic;
using System.IO;
using JrTools.Services;

namespace JrTools.Tests.Helpers
{
    /// <summary>
    /// Implementação fake de <see cref="IDirectoryReader"/> para uso em testes.
    /// Retorna uma lista de <see cref="FileInfo"/> configurada no construtor,
    /// ignorando o parâmetro <c>diretorio</c>.
    /// </summary>
    public class FakeDirectoryReader : IDirectoryReader
    {
        private readonly IEnumerable<FileInfo> _files;

        /// <summary>
        /// Inicializa o leitor fake com a lista de arquivos a retornar.
        /// </summary>
        /// <param name="files">Arquivos que serão retornados por <see cref="EnumerarArquivos"/>.</param>
        public FakeDirectoryReader(IEnumerable<FileInfo> files)
        {
            _files = files;
        }

        /// <inheritdoc />
        public IEnumerable<FileInfo> EnumerarArquivos(string diretorio)
        {
            return _files;
        }

        /// <summary>
        /// Cria objetos <see cref="FileInfo"/> a partir de nomes de arquivo,
        /// usando o diretório temporário do sistema como caminho base.
        /// Os arquivos não precisam existir em disco — apenas os metadados de nome/extensão são usados.
        /// </summary>
        /// <param name="nomes">Nomes de arquivo (ex.: "BennerRH.exe", "BennerCore.dll").</param>
        /// <returns>Lista de <see cref="FileInfo"/> com os caminhos construídos.</returns>
        public static List<FileInfo> CriarArquivosFake(IEnumerable<string> nomes)
        {
            var result = new List<FileInfo>();
            foreach (var nome in nomes)
            {
                result.Add(new FileInfo(Path.Combine(Path.GetTempPath(), nome)));
            }
            return result;
        }
    }
}
