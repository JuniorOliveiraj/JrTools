using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    /// <summary>
    /// Gerencia o espelhamento bidirecional de diretórios em tempo real
    /// </summary>
    public class PastaEspelhador : IDisposable
    {
        #region Constantes
        private const int DEBOUNCE_DELAY_MS = 100;
        private const int RETRY_DELAY_MS = 500;
        private const int MAX_RETRIES = 3;
        #endregion

        #region Campos Privados
        private FileSystemWatcher? _watcher;
        private bool _executando;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        // Controle de eventos duplicados
        private readonly Dictionary<string, DateTime> _ultimosEventos = new();
        #endregion

        #region Propriedades
        public bool EstaExecutando => _executando;
        #endregion

        #region Métodos Públicos
        /// <summary>
        /// Inicia o espelhamento assíncrono entre dois diretórios
        /// </summary>
        public async Task IniciarEspelhamentoAsync(
            string origem,
            string destino,
            IProgress<string>? progresso = null,
            CancellationToken cancellationToken = default)
        {
            if (_executando)
            {
                progresso?.Report("O espelhamento já está em execução.");
                return;
            }

            ValidarDiretorios(origem, destino);

            try
            {
                _executando = true;
                progresso?.Report($"Iniciando espelhamento: {origem} → {destino}");

                // Fase 1: Sincronização inicial
                await SincronizarDiretoriosAsync(origem, destino, progresso, cancellationToken);

                // Fase 2: Monitoramento contínuo
                IniciarMonitoramento(origem, destino, progresso);

                progresso?.Report("Espelhamento ativo. Monitorando alterações...");

                // Manter a thread ativa enquanto o espelhamento está rodando
                while (_executando && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                progresso?.Report("Espelhamento cancelado.");
                throw;
            }
            catch (Exception ex)
            {
                progresso?.Report($"ERRO: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Para o espelhamento
        /// </summary>
        public void Parar()
        {
            if (!_executando)
                return;

            lock (_lock)
            {
                _executando = false;

                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
        }
        #endregion

        #region Validação
        private static void ValidarDiretorios(string origem, string destino)
        {
            if (string.IsNullOrWhiteSpace(origem))
                throw new ArgumentException("Diretório de origem não pode ser vazio.", nameof(origem));

            if (string.IsNullOrWhiteSpace(destino))
                throw new ArgumentException("Diretório de destino não pode ser vazio.", nameof(destino));

            if (!Directory.Exists(origem))
                throw new DirectoryNotFoundException($"Diretório de origem não encontrado: {origem}");
        }
        #endregion

        #region Sincronização Inicial
        private async Task SincronizarDiretoriosAsync(
            string origem,
            string destino,
            IProgress<string>? progresso,
            CancellationToken cancellationToken)
        {
            // Criar diretório de destino se não existir
            if (!Directory.Exists(destino))
            {
                Directory.CreateDirectory(destino);
                progresso?.Report($"Diretório de destino criado: {destino}");
            }

            // Etapa 1: Limpar arquivos que não existem mais na origem
            await LimparArquivosInexistentesAsync(origem, destino, progresso, cancellationToken);

            // Etapa 2: Copiar/Atualizar arquivos da origem para o destino
            await CopiarArquivosAtualizadosAsync(origem, destino, progresso, cancellationToken);

            progresso?.Report("Sincronização inicial concluída.");
        }

        private async Task LimparArquivosInexistentesAsync(
            string origem,
            string destino,
            IProgress<string>? progresso,
            CancellationToken cancellationToken)
        {
            progresso?.Report("Limpando arquivos obsoletos no destino...");

            await Task.Run(() =>
            {
                // Limpar arquivos
                var arquivosDestino = Directory.GetFiles(destino, "*.*", SearchOption.AllDirectories);
                foreach (var arquivo in arquivosDestino)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var caminhoRelativo = Path.GetRelativePath(destino, arquivo);
                    var arquivoOrigem = Path.Combine(origem, caminhoRelativo);

                    if (!File.Exists(arquivoOrigem))
                    {
                        TentarDeletarArquivo(arquivo, progresso);
                    }
                }

                // Limpar diretórios vazios
                var diretoriosDestino = Directory.GetDirectories(destino, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length); // Do mais profundo para o mais raso

                foreach (var dir in diretoriosDestino)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var caminhoRelativo = Path.GetRelativePath(destino, dir);
                    var dirOrigem = Path.Combine(origem, caminhoRelativo);

                    if (!Directory.Exists(dirOrigem))
                    {
                        TentarDeletarDiretorio(dir, progresso);
                    }
                }
            }, cancellationToken);
        }

        private async Task CopiarArquivosAtualizadosAsync(
            string origem,
            string destino,
            IProgress<string>? progresso,
            CancellationToken cancellationToken)
        {
            progresso?.Report("Copiando arquivos atualizados...");

            await Task.Run(() =>
            {
                // Criar estrutura de diretórios
                var diretoriosOrigem = Directory.GetDirectories(origem, "*", SearchOption.AllDirectories);
                foreach (var dirOrigem in diretoriosOrigem)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var caminhoRelativo = Path.GetRelativePath(origem, dirOrigem);
                    var dirDestino = Path.Combine(destino, caminhoRelativo);

                    if (!Directory.Exists(dirDestino))
                    {
                        Directory.CreateDirectory(dirDestino);
                    }
                }

                // Copiar arquivos
                var arquivosOrigem = Directory.GetFiles(origem, "*.*", SearchOption.AllDirectories);
                int contador = 0;
                int total = arquivosOrigem.Length;

                foreach (var arquivoOrigem in arquivosOrigem)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var caminhoRelativo = Path.GetRelativePath(origem, arquivoOrigem);
                    var arquivoDestino = Path.Combine(destino, caminhoRelativo);

                    if (PrecisaCopiar(arquivoOrigem, arquivoDestino))
                    {
                        TentarCopiarArquivo(arquivoOrigem, arquivoDestino, progresso);
                    }

                    contador++;
                    if (contador % 100 == 0) // Relatar progresso a cada 100 arquivos
                    {
                        progresso?.Report($"Progresso: {contador}/{total} arquivos processados");
                    }
                }
            }, cancellationToken);
        }

        private static bool PrecisaCopiar(string origem, string destino)
        {
            if (!File.Exists(destino))
                return true;

            var infoOrigem = new FileInfo(origem);
            var infoDestino = new FileInfo(destino);

            // Copiar se o tamanho ou data de modificação forem diferentes
            return infoOrigem.Length != infoDestino.Length ||
                   infoOrigem.LastWriteTimeUtc != infoDestino.LastWriteTimeUtc;
        }
        #endregion

        #region Monitoramento em Tempo Real
        private void IniciarMonitoramento(string origem, string destino, IProgress<string>? progresso)
        {
            _watcher = new FileSystemWatcher(origem)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size
            };

            _watcher.Created += (s, e) => ProcessarEvento("CRIADO", e, origem, destino, progresso);
            _watcher.Changed += (s, e) => ProcessarEvento("MODIFICADO", e, origem, destino, progresso);
            _watcher.Deleted += (s, e) => ProcessarEvento("DELETADO", e, origem, destino, progresso);
            _watcher.Renamed += (s, e) => ProcessarRenomeacao(e, origem, destino, progresso);
            _watcher.Error += (s, e) => progresso?.Report($"ERRO no monitoramento: {e.GetException()?.Message}");

            _watcher.EnableRaisingEvents = true;
        }

        private void ProcessarEvento(
            string tipoEvento,
            FileSystemEventArgs e,
            string origem,
            string destino,
            IProgress<string>? progresso)
        {
            // Debounce: evitar processar o mesmo evento múltiplas vezes
            if (!DeveProcessarEvento(e.FullPath))
                return;

            Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    // Pequeno delay para garantir que o arquivo terminou de ser escrito
                    await Task.Delay(DEBOUNCE_DELAY_MS);

                    var caminhoRelativo = Path.GetRelativePath(origem, e.FullPath);
                    var caminhoDestino = Path.Combine(destino, caminhoRelativo);

                    switch (tipoEvento)
                    {
                        case "CRIADO":
                        case "MODIFICADO":
                            await ProcessarCriacaoOuModificacaoAsync(e.FullPath, caminhoDestino, progresso);
                            break;

                        case "DELETADO":
                            ProcessarDelecao(caminhoDestino, progresso);
                            break;
                    }

                    progresso?.Report($"{tipoEvento}: {e.Name}");
                }
                catch (Exception ex)
                {
                    progresso?.Report($"Erro ao processar {tipoEvento} '{e.Name}': {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }

        private bool DeveProcessarEvento(string caminho)
        {
            lock (_ultimosEventos)
            {
                var agora = DateTime.Now;

                if (_ultimosEventos.TryGetValue(caminho, out var ultimoEvento))
                {
                    // Ignorar eventos duplicados dentro de 200ms
                    if ((agora - ultimoEvento).TotalMilliseconds < 200)
                        return false;
                }

                _ultimosEventos[caminho] = agora;

                // Limpar eventos antigos (mais de 5 segundos)
                var eventosAntigos = _ultimosEventos
                    .Where(kvp => (agora - kvp.Value).TotalSeconds > 5)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var chave in eventosAntigos)
                {
                    _ultimosEventos.Remove(chave);
                }

                return true;
            }
        }

        private async Task ProcessarCriacaoOuModificacaoAsync(
            string origem,
            string destino,
            IProgress<string>? progresso)
        {
            if (File.Exists(origem))
            {
                var diretorioDestino = Path.GetDirectoryName(destino);
                if (!string.IsNullOrEmpty(diretorioDestino))
                {
                    Directory.CreateDirectory(diretorioDestino);
                }

                await TentarCopiarArquivoAsync(origem, destino, progresso);
            }
            else if (Directory.Exists(origem))
            {
                Directory.CreateDirectory(destino);
            }
        }

        private void ProcessarDelecao(string destino, IProgress<string>? progresso)
        {
            if (File.Exists(destino))
            {
                TentarDeletarArquivo(destino, progresso);
            }
            else if (Directory.Exists(destino))
            {
                TentarDeletarDiretorio(destino, progresso);
            }
        }

        private void ProcessarRenomeacao(
            RenamedEventArgs e,
            string origem,
            string destino,
            IProgress<string>? progresso)
        {
            Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    var caminhoAntigoRelativo = Path.GetRelativePath(origem, e.OldFullPath);
                    var caminhoNovoRelativo = Path.GetRelativePath(origem, e.FullPath);

                    var caminhoAntigoDestino = Path.Combine(destino, caminhoAntigoRelativo);
                    var caminhoNovoDestino = Path.Combine(destino, caminhoNovoRelativo);

                    if (File.Exists(caminhoAntigoDestino))
                    {
                        var diretorioDestino = Path.GetDirectoryName(caminhoNovoDestino);
                        if (!string.IsNullOrEmpty(diretorioDestino))
                        {
                            Directory.CreateDirectory(diretorioDestino);
                        }

                        File.Move(caminhoAntigoDestino, caminhoNovoDestino, true);
                        progresso?.Report($"RENOMEADO: {e.OldName} → {e.Name}");
                    }
                    else if (Directory.Exists(caminhoAntigoDestino))
                    {
                        Directory.Move(caminhoAntigoDestino, caminhoNovoDestino);
                        progresso?.Report($"PASTA RENOMEADA: {e.OldName} → {e.Name}");
                    }
                }
                catch (Exception ex)
                {
                    progresso?.Report($"Erro ao renomear '{e.OldName}': {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }
        #endregion

        #region Operações de Arquivo com Retry
        private async Task TentarCopiarArquivoAsync(
            string origem,
            string destino,
            IProgress<string>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    // Aguardar o arquivo estar disponível
                    await Task.Delay(tentativa * 50);

                    using var streamOrigem = new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var streamDestino = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None);
                    await streamOrigem.CopyToAsync(streamDestino);

                    // Copiar atributos do arquivo
                    File.SetLastWriteTimeUtc(destino, File.GetLastWriteTimeUtc(origem));
                    File.SetAttributes(destino, File.GetAttributes(origem));

                    return;
                }
                catch (IOException) when (tentativa < MAX_RETRIES)
                {
                    // Arquivo pode estar em uso, tentar novamente
                    await Task.Delay(RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    progresso?.Report($"Erro ao copiar '{Path.GetFileName(origem)}': {ex.Message}");
                    return;
                }
            }

            progresso?.Report($"Falha ao copiar '{Path.GetFileName(origem)}' após {MAX_RETRIES} tentativas");
        }

        private void TentarCopiarArquivo(string origem, string destino, IProgress<string>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    var diretorioDestino = Path.GetDirectoryName(destino);
                    if (!string.IsNullOrEmpty(diretorioDestino))
                    {
                        Directory.CreateDirectory(diretorioDestino);
                    }

                    File.Copy(origem, destino, true);
                    File.SetLastWriteTimeUtc(destino, File.GetLastWriteTimeUtc(origem));

                    return;
                }
                catch (IOException) when (tentativa < MAX_RETRIES)
                {
                    Thread.Sleep(RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    progresso?.Report($"Erro ao copiar '{Path.GetFileName(origem)}': {ex.Message}");
                    return;
                }
            }
        }

        private void TentarDeletarArquivo(string caminho, IProgress<string>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    if (File.Exists(caminho))
                    {
                        File.SetAttributes(caminho, FileAttributes.Normal);
                        File.Delete(caminho);
                        progresso?.Report($"DELETADO: {Path.GetFileName(caminho)}");
                    }
                    return;
                }
                catch (IOException) when (tentativa < MAX_RETRIES)
                {
                    Thread.Sleep(RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    progresso?.Report($"Erro ao deletar '{Path.GetFileName(caminho)}': {ex.Message}");
                    return;
                }
            }
        }

        private void TentarDeletarDiretorio(string caminho, IProgress<string>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    if (Directory.Exists(caminho))
                    {
                        // Tentar deletar apenas se estiver vazio
                        if (!Directory.EnumerateFileSystemEntries(caminho).Any())
                        {
                            Directory.Delete(caminho, false);
                            progresso?.Report($"PASTA DELETADA: {Path.GetFileName(caminho)}");
                        }
                    }
                    return;
                }
                catch (IOException) when (tentativa < MAX_RETRIES)
                {
                    Thread.Sleep(RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    progresso?.Report($"Erro ao deletar pasta '{Path.GetFileName(caminho)}': {ex.Message}");
                    return;
                }
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            Parar();
            _semaphore?.Dispose();
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}