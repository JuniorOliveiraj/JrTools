using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    /// <summary>
    /// Gerencia o espelhamento bidirecional de diretórios em tempo real
    /// </summary>
    public class PastaEspelhador : IDisposable, IAsyncDisposable
    {
        #region Constantes
        private const int DEBOUNCE_DELAY_MS = 500;
        private const int RETRY_DELAY_MS = 500;
        private const int MAX_RETRIES = 3;
        private const int VERIFICACAO_PERIODICA_MS = 10000;
        #endregion

        #region Campos Privados
        private FileSystemWatcher? _watcher;
        private volatile bool _executando;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _tarefaMonitoramento;
        private bool _disposed;

        private readonly Dictionary<string, DateTime> _ultimosEventos = new();
        private readonly Dictionary<string, string> _hashsArquivos = new();
        #endregion

        #region Propriedades
        public bool EstaExecutando => _executando;
        public bool IsDisposed => _disposed;
        #endregion


        /// <summary>
        /// Inicia o espelhamento assíncrono entre dois diretórios
        /// </summary>
        public async Task IniciarEspelhamentoAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_executando)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Mensagem = "O espelhamento já está em execução."
                });
                return;
            }

            ValidarDiretorios(origem, destino);

            try
            {
                // Limpar estado anterior se houver
                await LimparEstadoAnteriorAsync();

                _executando = true;
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Fase = FaseEspelhamento.Analise,
                    Percentual = 0,
                    Status = "Iniciando espelhamento",
                    Mensagem = $"Iniciando espelhamento: {origem} → {destino}"
                });

                // Fase 1: Sincronização inicial
                await SincronizarDiretoriosAsync(origem, destino, progresso, _cancellationTokenSource.Token);

                // Fase 2: Iniciar monitoramento em tempo real
                IniciarMonitoramento(origem, destino, progresso);

                // Fase 3: Iniciar verificação periódica
                _tarefaMonitoramento = Task.Run(async () =>
                {
                    try 
                    {
                        await MonitoramentoContinuoAsync(origem, destino, progresso, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Esperado quando cancelado
                    }
                    catch (Exception ex)
                    {
                        ReportarProgresso(progresso, new ProgressoEspelhamento
                        {
                            Mensagem = $"❌ Erro fatal no monitoramento: {ex.Message}"
                        });
                        throw;
                    }
                }, _cancellationTokenSource.Token);

                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Fase = FaseEspelhamento.MonitoramentoContinuo,
                    Percentual = 100,
                    Status = "Monitoramento ativo",
                    Mensagem = "✓ Espelhamento ativo. Monitorando alterações em tempo real..."
                });

                while (_executando && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Mensagem = "Espelhamento cancelado."
                });
                throw;
            }
            catch (Exception ex)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Mensagem = $"ERRO: {ex.Message}"
                });
                throw;
            }
        }

        public async Task PararAsync(TimeSpan? timeout = null)
        {
            if (!_executando)
                return;

            var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);

            lock (_lock)
            {
                if (!_executando)
                    return;

                _executando = false;
                _cancellationTokenSource?.Cancel();
            }

            try
            {
                if (_tarefaMonitoramento != null)
                {
                    // Aguardar a tarefa completar ou timeout
                    using var timeoutCts = new CancellationTokenSource(timeoutValue);
                    await Task.WhenAny(_tarefaMonitoramento, Task.Delay(timeoutValue, timeoutCts.Token));
                }
            }
            catch (Exception)
            {
                // Ignorar erros ao aguardar finalização
            }
            finally
            {
                await LimparRecursosAsync();
            }
        }

        private async Task LimparEstadoAnteriorAsync()
        {
            if (_executando)
            {
                await PararAsync();
            }

            _ultimosEventos.Clear();
            _hashsArquivos.Clear();
        }

        private async Task LimparRecursosAsync()
        {
            try
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Created -= null;
                    _watcher.Changed -= null;
                    _watcher.Deleted -= null;
                    _watcher.Renamed -= null;
                    _watcher.Error -= null;
                    _watcher.Dispose();
                    _watcher = null;
                }

                if (_cancellationTokenSource != null)
                {
                    await _cancellationTokenSource.CancelAsync();
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }

                _ultimosEventos.Clear();
                _hashsArquivos.Clear();
            }
            catch
            {
                // Ignora erros na limpeza
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PastaEspelhador));
            }
        }

        #region IDisposable e IAsyncDisposable
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            await PararAsync();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            PararAsync().GetAwaiter().GetResult();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    _semaphore?.Dispose();
                    _cancellationTokenSource?.Dispose();
                    _watcher?.Dispose();
                    _tarefaMonitoramento?.Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            _disposed = true;
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

        #region Helpers de Progresso
        private void ReportarProgresso(IProgress<ProgressoEspelhamento>? progresso, ProgressoEspelhamento info)
        {
            progresso?.Report(info);
        }
        #endregion

        #region Monitoramento Contínuo
        private async Task MonitoramentoContinuoAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso,
            CancellationToken cancellationToken)
        {
            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Mensagem = "⏱ Verificação periódica iniciada (a cada 10 segundos)"
            });

            while (!cancellationToken.IsCancellationRequested && _executando)
            {
                try
                {
                    await Task.Delay(VERIFICACAO_PERIODICA_MS, cancellationToken);

                    if (!_executando) break;

                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = "🔍 Verificando alterações..."
                    });

                    await VerificarAlteracoesAsync(origem, destino, progresso, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"Erro na verificação periódica: {ex.Message}"
                    });
                }
            }
        }

        private async Task VerificarAlteracoesAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso,
            CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                int arquivosVerificados = 0;
                int arquivosAtualizados = 0;

                var arquivosOrigem = Directory.GetFiles(origem, "*.*", SearchOption.AllDirectories);

                foreach (var arquivoOrigem in arquivosOrigem)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var caminhoRelativo = Path.GetRelativePath(origem, arquivoOrigem);
                    var arquivoDestino = Path.Combine(destino, caminhoRelativo);

                    if (await ArquivoPrecisaSerAtualizadoAsync(arquivoOrigem, arquivoDestino))
                    {
                        await TentarCopiarArquivoAsync(arquivoOrigem, arquivoDestino, progresso);
                        arquivosAtualizados++;
                    }

                    arquivosVerificados++;
                }

                string mensagem = arquivosAtualizados > 0
                    ? $"✓ Verificação concluída: {arquivosAtualizados} arquivo(s) atualizado(s) de {arquivosVerificados} verificados"
                    : $"✓ Verificação concluída: Nenhuma alteração detectada ({arquivosVerificados} arquivos)";

                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Mensagem = mensagem
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<bool> ArquivoPrecisaSerAtualizadoAsync(string origem, string destino)
        {
            if (!File.Exists(destino))
                return true;

            var infoOrigem = new FileInfo(origem);
            var infoDestino = new FileInfo(destino);

            if (infoOrigem.Length != infoDestino.Length)
                return true;

            try
            {
                string hashOrigem = await CalcularHashArquivoAsync(origem);
                string hashDestino = await CalcularHashArquivoAsync(destino);

                if (hashOrigem != hashDestino)
                {
                    _hashsArquivos[origem] = hashOrigem;
                    return true;
                }

                _hashsArquivos[origem] = hashOrigem;
                return false;
            }
            catch
            {
                return infoOrigem.LastWriteTimeUtc != infoDestino.LastWriteTimeUtc;
            }
        }

        private async Task<string> CalcularHashArquivoAsync(string caminho)
        {
            if (_hashsArquivos.TryGetValue(caminho, out var hashCached))
            {
                var info = new FileInfo(caminho);
                if (info.Length == new FileInfo(caminho).Length)
                {
                    return hashCached;
                }
            }

            using var md5 = MD5.Create();
            using var stream = File.OpenRead(caminho);
            var hash = await md5.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        #endregion

        #region Sincronização Inicial
        private async Task SincronizarDiretoriosAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(destino))
            {
                Directory.CreateDirectory(destino);
                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Mensagem = $"Diretório de destino criado: {destino}"
                });
            }

            // Etapa 1: Análise
            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Fase = FaseEspelhamento.Analise,
                Percentual = 5,
                Status = "Analisando diretórios",
                Detalhes = "Contando arquivos..."
            });

            // Etapa 2: Limpar arquivos obsoletos
            await LimparArquivosInexistentesAsync(origem, destino, progresso, cancellationToken);

            // Etapa 3: Copiar/Atualizar arquivos
            await CopiarArquivosAtualizadosAsync(origem, destino, progresso, cancellationToken);

            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Fase = FaseEspelhamento.Copia,
                Percentual = 100,
                Status = "Sincronização concluída",
                Mensagem = "✓ Sincronização inicial concluída."
            });
        }

        private async Task LimparArquivosInexistentesAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso,
            CancellationToken cancellationToken)
        {
            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Fase = FaseEspelhamento.Limpeza,
                Percentual = 10,
                Status = "Limpando arquivos obsoletos",
                Mensagem = "🧹 Limpando arquivos obsoletos no destino..."
            });

            await Task.Run(() =>
            {
                int arquivosRemovidos = 0;
                var arquivosDestino = Directory.GetFiles(destino, "*.*", SearchOption.AllDirectories);

                foreach (var arquivo in arquivosDestino)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var caminhoRelativo = Path.GetRelativePath(destino, arquivo);
                    var arquivoOrigem = Path.Combine(origem, caminhoRelativo);

                    if (!File.Exists(arquivoOrigem))
                    {
                        TentarDeletarArquivo(arquivo, progresso);
                        arquivosRemovidos++;
                    }
                }

                var diretoriosDestino = Directory.GetDirectories(destino, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length);

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

                if (arquivosRemovidos > 0)
                {
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Fase = FaseEspelhamento.Limpeza,
                        Percentual = 30,
                        Mensagem = $"✓ Limpeza concluída: {arquivosRemovidos} arquivo(s) removido(s)"
                    });
                }
            }, cancellationToken);
        }

        private async Task CopiarArquivosAtualizadosAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso,
            CancellationToken cancellationToken)
        {
            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Fase = FaseEspelhamento.Copia,
                Percentual = 35,
                Status = "Copiando arquivos",
                Mensagem = "📋 Copiando arquivos atualizados..."
            });

            await Task.Run(async () =>
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

                // Copiar arquivos com progresso
                var arquivosOrigem = Directory.GetFiles(origem, "*.*", SearchOption.AllDirectories);
                int contador = 0;
                int copiados = 0;
                int total = arquivosOrigem.Length;

                foreach (var arquivoOrigem in arquivosOrigem)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var caminhoRelativo = Path.GetRelativePath(origem, arquivoOrigem);
                    var arquivoDestino = Path.Combine(destino, caminhoRelativo);

                    if (await ArquivoPrecisaSerAtualizadoAsync(arquivoOrigem, arquivoDestino))
                    {
                        await TentarCopiarArquivoAsync(arquivoOrigem, arquivoDestino, null);
                        copiados++;
                    }

                    contador++;

                    // Atualizar progresso a cada 10 arquivos ou no último
                    if (contador % 10 == 0 || contador == total)
                    {
                        double percentual = 35 + (contador / (double)total * 65);

                        ReportarProgresso(progresso, new ProgressoEspelhamento
                        {
                            Fase = FaseEspelhamento.Copia,
                            Percentual = percentual,
                            Status = "Copiando arquivos",
                            ArquivosProcessados = contador,
                            TotalArquivos = total,
                            ArquivosCopiadados = copiados
                        });
                    }
                }

                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Fase = FaseEspelhamento.Copia,
                    Percentual = 100,
                    Status = "Cópia concluída",
                    Mensagem = $"✓ Cópia concluída: {copiados} arquivo(s) copiado(s) de {total}"
                });
            }, cancellationToken);
        }
        #endregion

        #region Monitoramento em Tempo Real
        private void IniciarMonitoramento(string origem, string destino, IProgress<ProgressoEspelhamento>? progresso)
        {
            _watcher = new FileSystemWatcher(origem)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
            };

            _watcher.Created += (s, e) => ProcessarEvento("CRIADO", e, origem, destino, progresso);
            _watcher.Changed += (s, e) => ProcessarEvento("MODIFICADO", e, origem, destino, progresso);
            _watcher.Deleted += (s, e) => ProcessarEvento("DELETADO", e, origem, destino, progresso);
            _watcher.Renamed += (s, e) => ProcessarRenomeacao(e, origem, destino, progresso);
            _watcher.Error += (s, e) => ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Mensagem = $"❌ ERRO no monitoramento: {e.GetException()?.Message}"
            });

            _watcher.EnableRaisingEvents = true;

            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Mensagem = "👁 Monitoramento em tempo real ativado"
            });
        }

        private void ProcessarEvento(
            string tipoEvento,
            FileSystemEventArgs e,
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso)
        {
            if (!DeveProcessarEvento(e.FullPath))
                return;

            _ = Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
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
                            _hashsArquivos.Remove(e.FullPath);
                            break;
                    }

                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"🔄 {tipoEvento}: {e.Name}"
                    });
                }
                catch (Exception ex)
                {
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"❌ Erro ao processar {tipoEvento} '{e.Name}': {ex.Message}"
                    });
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
                    if ((agora - ultimoEvento).TotalMilliseconds < 1000)
                        return false;
                }

                _ultimosEventos[caminho] = agora;

                var eventosAntigos = _ultimosEventos
                    .Where(kvp => (agora - kvp.Value).TotalSeconds > 10)
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
            IProgress<ProgressoEspelhamento>? progresso)
        {
            if (File.Exists(origem))
            {
                if (await ArquivoPrecisaSerAtualizadoAsync(origem, destino))
                {
                    var diretorioDestino = Path.GetDirectoryName(destino);
                    if (!string.IsNullOrEmpty(diretorioDestino))
                    {
                        Directory.CreateDirectory(diretorioDestino);
                    }

                    await TentarCopiarArquivoAsync(origem, destino, progresso);
                }
            }
            else if (Directory.Exists(origem))
            {
                Directory.CreateDirectory(destino);
            }
        }

        private void ProcessarDelecao(string destino, IProgress<ProgressoEspelhamento>? progresso)
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
            IProgress<ProgressoEspelhamento>? progresso)
        {
            _ = Task.Run(async () =>
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

                        if (_hashsArquivos.ContainsKey(e.OldFullPath))
                        {
                            var hash = _hashsArquivos[e.OldFullPath];
                            _hashsArquivos.Remove(e.OldFullPath);
                            _hashsArquivos[e.FullPath] = hash;
                        }

                        ReportarProgresso(progresso, new ProgressoEspelhamento
                        {
                            Mensagem = $"📝 RENOMEADO: {e.OldName} → {e.Name}"
                        });
                    }
                    else if (Directory.Exists(caminhoAntigoDestino))
                    {
                        Directory.Move(caminhoAntigoDestino, caminhoNovoDestino);
                        ReportarProgresso(progresso, new ProgressoEspelhamento
                        {
                            Mensagem = $"📁 PASTA RENOMEADA: {e.OldName} → {e.Name}"
                        });
                    }
                }
                catch (Exception ex)
                {
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"❌ Erro ao renomear '{e.OldName}': {ex.Message}"
                    });
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
            IProgress<ProgressoEspelhamento>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    await Task.Delay(tentativa * 50);

                    var diretorioDestino = Path.GetDirectoryName(destino);
                    if (!string.IsNullOrEmpty(diretorioDestino))
                    {
                        Directory.CreateDirectory(diretorioDestino);
                    }

                    using var streamOrigem = new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var streamDestino = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None);
                    await streamOrigem.CopyToAsync(streamDestino);

                    File.SetLastWriteTimeUtc(destino, File.GetLastWriteTimeUtc(origem));
                    File.SetAttributes(destino, File.GetAttributes(origem));

                    string hash = await CalcularHashArquivoAsync(origem);
                    _hashsArquivos[origem] = hash;

                    return;
                }
                catch (IOException) when (tentativa < MAX_RETRIES)
                {
                    await Task.Delay(RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"❌ Erro ao copiar '{Path.GetFileName(origem)}': {ex.Message}"
                    });
                    return;
                }
            }

            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Mensagem = $"❌ Falha ao copiar '{Path.GetFileName(origem)}' após {MAX_RETRIES} tentativas"
            });
        }

        private void TentarDeletarArquivo(string caminho, IProgress<ProgressoEspelhamento>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    if (File.Exists(caminho))
                    {
                        File.SetAttributes(caminho, FileAttributes.Normal);
                        File.Delete(caminho);
                        ReportarProgresso(progresso, new ProgressoEspelhamento
                        {
                            Mensagem = $"🗑 DELETADO: {Path.GetFileName(caminho)}"
                        });
                    }
                    return;
                }
                catch (IOException) when (tentativa < MAX_RETRIES)
                {
                    Thread.Sleep(RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"❌ Erro ao deletar '{Path.GetFileName(caminho)}': {ex.Message}"
                    });
                    return;
                }
            }
        }

        private void TentarDeletarDiretorio(string caminho, IProgress<ProgressoEspelhamento>? progresso)
        {
            for (int tentativa = 1; tentativa <= MAX_RETRIES; tentativa++)
            {
                try
                {
                    if (Directory.Exists(caminho))
                    {
                        if (!Directory.EnumerateFileSystemEntries(caminho).Any())
                        {
                            Directory.Delete(caminho, false);
                            ReportarProgresso(progresso, new ProgressoEspelhamento
                            {
                                Mensagem = $"🗑 PASTA DELETADA: {Path.GetFileName(caminho)}"
                            });
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
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Mensagem = $"❌ Erro ao deletar pasta '{Path.GetFileName(caminho)}': {ex.Message}"
                    });
                    return;
                }
            }
        }
        #endregion
    }

}