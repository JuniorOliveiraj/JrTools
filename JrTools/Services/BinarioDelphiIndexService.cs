using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JrTools.Dto;

namespace JrTools.Services
{
    /// <summary>
    /// Serviço responsável por construir e manter o índice em memória dos binários Delphi.
    /// O índice é construído uma única vez por sessão e invalidado explicitamente quando necessário.
    /// </summary>
    public class BinarioDelphiIndexService
    {
        private readonly IDirectoryReader _directoryReader;

        /// <summary>Índice em memória. Null indica que ainda não foi construído.</summary>
        private Dictionary<string, BinarioDelphiItem>? _indice;

        /// <summary>Diretório para o qual o índice foi construído.</summary>
        private string? _diretorioIndexado;

        /// <summary>Conjunto de extensões únicas presentes no índice.</summary>
        private HashSet<string> _extensoesUnicas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Construtor para injeção de dependência (testabilidade).
        /// </summary>
        /// <param name="directoryReader">Implementação de leitura de diretório.</param>
        public BinarioDelphiIndexService(IDirectoryReader directoryReader)
        {
            _directoryReader = directoryReader ?? throw new ArgumentNullException(nameof(directoryReader));
        }

        /// <summary>
        /// Construtor padrão que usa a implementação real do sistema de arquivos.
        /// </summary>
        public BinarioDelphiIndexService()
            : this(new DirectoryReaderImpl())
        {
        }

        /// <summary>
        /// Indica se o índice já foi construído para o diretório atual.
        /// </summary>
        public bool IndiceDisponivel => _indice != null;

        /// <summary>
        /// Conjunto de extensões únicas presentes no índice construído.
        /// </summary>
        public IReadOnlySet<string> ExtensoesUnicas => _extensoesUnicas;

        /// <summary>
        /// Constrói o índice a partir do diretório informado.
        /// Se o índice já foi construído para o mesmo diretório, retorna o cache imediatamente.
        /// </summary>
        /// <param name="diretorio">Caminho completo do diretório a ser indexado.</param>
        /// <param name="progresso">Callback opcional para reportar progresso.</param>
        /// <param name="ct">Token de cancelamento.</param>
        /// <returns>Dicionário com chave <c>NomeNormalizado</c> e valor <see cref="BinarioDelphiItem"/>.</returns>
        public Task<Dictionary<string, BinarioDelphiItem>> ConstruirIndiceAsync(
            string diretorio,
            IProgress<string>? progresso = null,
            CancellationToken ct = default)
        {
            // Retorna cache se o índice já foi construído para o mesmo diretório
            if (_indice != null && _diretorioIndexado == diretorio)
                return Task.FromResult(_indice);

            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                var novoIndice = new Dictionary<string, BinarioDelphiItem>(StringComparer.OrdinalIgnoreCase);
                var novasExtensoes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var arquivos = _directoryReader.EnumerarArquivos(diretorio);
                int contador = 0;

                foreach (var fileInfo in arquivos)
                {
                    ct.ThrowIfCancellationRequested();

                    var item = BinarioDelphiItem.FromFileInfo(fileInfo);
                    novoIndice[item.NomeNormalizado] = item;
                    novasExtensoes.Add(item.Extensao);

                    contador++;

                    // Reporta progresso a cada 100 arquivos
                    if (contador % 100 == 0)
                        progresso?.Report($"Indexando... {contador} arquivos processados.");
                }

                progresso?.Report($"Índice concluído: {contador} arquivos indexados.");

                _extensoesUnicas = novasExtensoes;
                _diretorioIndexado = diretorio;
                _indice = novoIndice;

                return _indice;
            }, ct);
        }

        /// <summary>
        /// Descarta o índice atual, forçando reconstrução na próxima chamada de <see cref="ConstruirIndiceAsync"/>.
        /// </summary>
        public void InvalidarIndice()
        {
            _indice = null;
            _diretorioIndexado = null;
        }

        /// <summary>
        /// Carrega os detalhes de um binário específico sob demanda via <see cref="FileVersionInfo"/>.
        /// A leitura é feita em background para não bloquear a thread de UI.
        /// </summary>
        /// <param name="item">Item do índice cujos detalhes serão carregados.</param>
        /// <param name="ct">Token de cancelamento.</param>
        /// <returns><see cref="BinarioDelphiDetalhe"/> com as informações de versão do arquivo.</returns>
        public Task<BinarioDelphiDetalhe> CarregarDetalheAsync(
            BinarioDelphiItem item,
            CancellationToken ct = default)
        {
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(item.CaminhoCompleto);

                    return new BinarioDelphiDetalhe
                    {
                        CaminhoCompleto = item.CaminhoCompleto,
                        VersaoArquivo = versionInfo.FileVersion,
                        VersaoProduto = versionInfo.ProductVersion,
                        DescricaoArquivo = versionInfo.FileDescription,
                        Empresa = versionInfo.CompanyName,
                        ErroAoCarregar = false,
                        MensagemErro = null
                    };
                }
                catch (Exception ex)
                {
                    return new BinarioDelphiDetalhe
                    {
                        CaminhoCompleto = item.CaminhoCompleto,
                        ErroAoCarregar = true,
                        MensagemErro = ex.Message
                    };
                }
            }, ct);
        }

        /// <summary>
        /// Implementação real de <see cref="IDirectoryReader"/> que usa o sistema de arquivos.
        /// </summary>
        private sealed class DirectoryReaderImpl : IDirectoryReader
        {
            /// <inheritdoc/>
            public IEnumerable<FileInfo> EnumerarArquivos(string diretorio)
            {
                IEnumerable<string> caminhos;

                try
                {
                    caminhos = Directory.EnumerateFiles(diretorio);
                }
                catch (DirectoryNotFoundException)
                {
                    yield break;
                }

                foreach (var caminho in caminhos)
                    yield return new FileInfo(caminho);
            }
        }
    }
}
