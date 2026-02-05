using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Workflows
{
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

        public async Task IniciarEspelhamentoAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_executando)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = "O espelhamento já está em execução." });
                return;
            }

            ValidarDiretorios(origem, destino);

            try
            {
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

                // Fase 1: Sincronização Inteligente (Diff)
                await SincronizarDiretoriosAsync(origem, destino, progresso, _cancellationTokenSource.Token);

                // Fase 2: Monitoramento
                IniciarMonitoramento(origem, destino, progresso);
                
                // Monitoramento periódico (backup)
                _tarefaMonitoramento = Task.Run(async () =>
                {
                    try { await MonitoramentoContinuoAsync(origem, destino, progresso, _cancellationTokenSource.Token); }
                    catch (OperationCanceledException) { }
                    catch (Exception ex) 
                    { 
                        ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = $"❌ Erro no monitoramento: {ex.Message}" });
                    }
                }, _cancellationTokenSource.Token);

                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Fase = FaseEspelhamento.MonitoramentoContinuo,
                    Percentual = 100,
                    Status = "Monitoramento ativo",
                    Mensagem = "✓ Espelhamento ativo. Monitorando alterações..."
                });
                
                // Manter ativo
                while (_executando && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = "Espelhamento cancelado." });
                throw;
            }
            catch (Exception ex)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = $"ERRO: {ex.Message}" });
                throw;
            }
        }

        #region Sincronização Inteligente (Diff)
        private async Task SincronizarDiretoriosAsync(
            string origem,
            string destino,
            IProgress<ProgressoEspelhamento>? progresso,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(destino)) Directory.CreateDirectory(destino);

            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Fase = FaseEspelhamento.Analise,
                Percentual = 0,
                Status = "Analisando...",
                Mensagem = "🔍 Analisando diferenças entre diretórios..."
            });

            var diff = await AnalisarDiferencasAsync(origem, destino, cancellationToken);
            
            int totalOperacoes = diff.ParaCopiar.Count + diff.ParaDeletar.Count;
            int processados = 0;

            if (totalOperacoes == 0)
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento
                {
                    Fase = FaseEspelhamento.Copia,
                    Percentual = 100,
                    Status = "Sincronizado",
                    Mensagem = "✓ Diretórios já estão perfeitamente sincronizados."
                });
                return;
            }

            ReportarProgresso(progresso, new ProgressoEspelhamento
            {
                Fase = FaseEspelhamento.Analise,
                Percentual = 100,
                Status = "Análise concluída",
                Mensagem = $"📝 Encontrado: {diff.ParaCopiar.Count} a copiar, {diff.ParaDeletar.Count} a remover."
            });

            // LIMPEZA
            if (diff.ParaDeletar.Any())
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento { Fase = FaseEspelhamento.Limpeza, Status = "Limpando...", Mensagem = "🧹 Removendo arquivos obsoletos..." });
                foreach (var arq in diff.ParaDeletar)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TentarDeletarArquivo(arq, progresso);
                    processados++;
                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Fase = FaseEspelhamento.Limpeza,
                        Percentual = (double)processados / totalOperacoes * 100,
                        TotalArquivos = totalOperacoes,
                        ArquivosProcessados = processados,
                        Status = $"Deletando: {Path.GetFileName(arq)}"
                    });
                }
            }

            // CÓPIA
            if (diff.ParaCopiar.Any())
            {
                ReportarProgresso(progresso, new ProgressoEspelhamento { Fase = FaseEspelhamento.Copia, Status = "Copiando...", Mensagem = "📋 Copiando arquivos..." });
                int copiados = 0;
                foreach (var origemFile in diff.ParaCopiar)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rel = Path.GetRelativePath(origem, origemFile);
                    var destFile = Path.Combine(destino, rel);

                    var dirDest = Path.GetDirectoryName(destFile);
                    if (!Directory.Exists(dirDest)) Directory.CreateDirectory(dirDest);

                    await TentarCopiarArquivoAsync(origemFile, destFile, null);
                    processados++;
                    copiados++;

                    ReportarProgresso(progresso, new ProgressoEspelhamento
                    {
                        Fase = FaseEspelhamento.Copia,
                        Percentual = (double)processados / totalOperacoes * 100,
                        TotalArquivos = totalOperacoes,
                        ArquivosProcessados = processados,
                        ArquivosCopiadados = copiados,
                        Status = $"Copiando: {Path.GetFileName(origemFile)}"
                    });
                }
            }
            
            await LimparPastasVaziasAsync(destino, cancellationToken);
        }

        private async Task<(List<string> ParaCopiar, List<string> ParaDeletar)> AnalisarDiferencasAsync(string origem, string destino, CancellationToken token)
        {
            return await Task.Run(async () =>
            {
                var paraCopiar = new List<string>();
                var paraDeletar = new List<string>();

                var arquivosOrigem = Directory.GetFiles(origem, "*.*", SearchOption.AllDirectories);
                var arquivosDestino = Directory.GetFiles(destino, "*.*", SearchOption.AllDirectories);
                
                var hashOrigem = new HashSet<string>(arquivosOrigem.Select(x => Path.GetRelativePath(origem, x)));

                // Detectar Deletions
                foreach (var destFile in arquivosDestino)
                {
                    token.ThrowIfCancellationRequested();
                    if (!hashOrigem.Contains(Path.GetRelativePath(destino, destFile)))
                        paraDeletar.Add(destFile);
                }

                // Detectar Changes/Creations
                foreach (var origFile in arquivosOrigem)
                {
                    token.ThrowIfCancellationRequested();
                    var destFile = Path.Combine(destino, Path.GetRelativePath(origem, origFile));
                    if (await ArquivoPrecisaSerAtualizadoAsync(origFile, destFile))
                        paraCopiar.Add(origFile);
                }

                return (paraCopiar, paraDeletar);
            }, token);
        }

        private async Task LimparPastasVaziasAsync(string dir, CancellationToken token)
        {
            await Task.Run(() => {
                try {
                foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length)) {
                    if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);
                }} catch{}
            }, token);
        }
        #endregion

        #region Helpers Monitoramento
        private void IniciarMonitoramento(string origem, string destino, IProgress<ProgressoEspelhamento>? progresso)
        {
            _watcher = new FileSystemWatcher(origem)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            _watcher.Created += (s, e) => ProcessarEvento("CRIADO", e, origem, destino, progresso);
            _watcher.Changed += (s, e) => ProcessarEvento("MODIFICADO", e, origem, destino, progresso);
            _watcher.Deleted += (s, e) => ProcessarEvento("DELETADO", e, origem, destino, progresso);
            _watcher.Renamed += (s, e) => ProcessarRenomeacao(e, origem, destino, progresso);
            _watcher.Error += (s, e) => ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = $"❌ ERRO Watcher: {e.GetException()?.Message}" });

            _watcher.EnableRaisingEvents = true;
        }

        private void ProcessarEvento(string tipo, FileSystemEventArgs e, string origem, string destino, IProgress<ProgressoEspelhamento>? progresso)
        {
            if (!DeveProcessarEvento(e.FullPath)) return;

            _ = Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    await Task.Delay(DEBOUNCE_DELAY_MS);
                    var rel = Path.GetRelativePath(origem, e.FullPath);
                    var dest = Path.Combine(destino, rel);

                    switch (tipo)
                    {
                        case "CRIADO":
                        case "MODIFICADO":
                            if (File.Exists(e.FullPath) && await ArquivoPrecisaSerAtualizadoAsync(e.FullPath, dest))
                            {
                                var dDir = Path.GetDirectoryName(dest);
                                if (!Directory.Exists(dDir)) Directory.CreateDirectory(dDir);
                                await TentarCopiarArquivoAsync(e.FullPath, dest, progresso);
                            }
                            break;
                        case "DELETADO":
                            if (File.Exists(dest)) TentarDeletarArquivo(dest, progresso);
                            break;
                    }
                    ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = $"🔄 {tipo}: {e.Name}" });
                }
                finally { _semaphore.Release(); }
            });
        }

        private void ProcessarRenomeacao(RenamedEventArgs e, string origem, string destino, IProgress<ProgressoEspelhamento>? progresso)
        {
             _ = Task.Run(async () => {
                await _semaphore.WaitAsync();
                try {
                    var relOld = Path.GetRelativePath(origem, e.OldFullPath);
                    var relNew = Path.GetRelativePath(origem, e.FullPath);
                    var destOld = Path.Combine(destino, relOld);
                    var destNew = Path.Combine(destino, relNew);

                    if (File.Exists(destOld)) {
                        var dDir = Path.GetDirectoryName(destNew);
                        if (!Directory.Exists(dDir)) Directory.CreateDirectory(dDir);
                        File.Move(destOld, destNew, true);
                        ReportarProgresso(progresso, new ProgressoEspelhamento { Mensagem = $"📝 RENOMEADO: {e.OldName} -> {e.Name}" });
                    }
                } finally { _semaphore.Release(); }
            });
        }

        private bool DeveProcessarEvento(string caminho)
        {
            lock (_ultimosEventos) {
                var agora = DateTime.Now;
                if (_ultimosEventos.TryGetValue(caminho, out var ult) && (agora - ult).TotalMilliseconds < 1000) return false;
                _ultimosEventos[caminho] = agora;
                // Cleanup
                foreach(var k in _ultimosEventos.Where(kv => (agora - kv.Value).TotalSeconds > 10).Select(k => k.Key).ToList()) _ultimosEventos.Remove(k);
                return true;
            }
        }
        
        private async Task MonitoramentoContinuoAsync(string origem, string destino, IProgress<ProgressoEspelhamento>? progresso, CancellationToken token)
        {
             while (!token.IsCancellationRequested && _executando) {
                 await Task.Delay(VERIFICACAO_PERIODICA_MS, token);
                 // Opcional: Re-verificar diffs periodicamente
             }
        }
        #endregion

        #region Helpers Gerais
        public async Task PararAsync(TimeSpan? timeout = null)
        {
            if (!_executando) return;
            lock (_lock) { _executando = false; _cancellationTokenSource?.Cancel(); }
            try { if (_tarefaMonitoramento != null) await Task.WhenAny(_tarefaMonitoramento, Task.Delay(timeout ?? TimeSpan.FromSeconds(5))); } catch {}
            await LimparRecursosAsync();
        }

        public async ValueTask DisposeAsync() { await PararAsync(); Dispose(true); GC.SuppressFinalize(this); }
        public void Dispose() { PararAsync().GetAwaiter().GetResult(); Dispose(true); GC.SuppressFinalize(this); }
        protected virtual void Dispose(bool disposing) 
        { 
            if (_disposed) return; 
            if (disposing) { _semaphore?.Dispose(); _cancellationTokenSource?.Dispose(); _watcher?.Dispose(); }
            _disposed = true; 
        }

        private async Task LimparEstadoAnteriorAsync() { if (_executando) await PararAsync(); _ultimosEventos.Clear(); _hashsArquivos.Clear(); }
        private async Task LimparRecursosAsync() { 
            try { 
                _watcher?.Dispose(); 
                _watcher = null;
                _cancellationTokenSource?.Dispose();
            } catch {}
            _ultimosEventos.Clear();
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PastaEspelhador)); }
        private void ValidarDiretorios(string o, string d) { if(string.IsNullOrWhiteSpace(o) || string.IsNullOrWhiteSpace(d)) throw new ArgumentException("Diretórios inválidos"); if(!Directory.Exists(o)) throw new DirectoryNotFoundException("Origem não existe"); }
        private void ReportarProgresso(IProgress<ProgressoEspelhamento>? p, ProgressoEspelhamento i) => p?.Report(i);

        private async Task<bool> ArquivoPrecisaSerAtualizadoAsync(string origem, string destino)
        {
            if (!File.Exists(destino)) return true;
            var infoO = new FileInfo(origem);
            var infoD = new FileInfo(destino);
            if (infoO.Length != infoD.Length) return true;
            if (Math.Abs((infoO.LastWriteTimeUtc - infoD.LastWriteTimeUtc).TotalSeconds) > 2) return true; // Check rápido de data
            return false;
        }

        private async Task TentarCopiarArquivoAsync(string origem, string destino, IProgress<ProgressoEspelhamento>? p)
        {
             for(int i=0; i<=MAX_RETRIES; i++) {
                 try {
                     using(var s = new FileStream(origem, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                     using(var d = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.Read))
                         await s.CopyToAsync(d);
                     File.SetLastWriteTimeUtc(destino, File.GetLastWriteTimeUtc(origem));
                     return;
                 } catch (Exception ex) {
                     if(i==MAX_RETRIES) p?.Report(new ProgressoEspelhamento { Mensagem = $"❌ Falha ao copiar {Path.GetFileName(origem)}: {ex.Message}" });
                     await Task.Delay(RETRY_DELAY_MS);
                 }
             }
        }
        private void TentarDeletarArquivo(string c, IProgress<ProgressoEspelhamento>? p) { try { if(File.Exists(c)) File.Delete(c); } catch(Exception ex) { p?.Report(new ProgressoEspelhamento { Mensagem = $"❌ Falha ao deletar {Path.GetFileName(c)}: {ex.Message}" }); } }
        private void TentarDeletarDiretorio(string c, IProgress<ProgressoEspelhamento>? p) { try { if(Directory.Exists(c)) Directory.Delete(c, true); } catch(Exception ex) { p?.Report(new ProgressoEspelhamento { Mensagem = $"❌ Falha ao deletar pasta: {ex.Message}" }); } }
        private async Task<string> CalcularHashArquivoAsync(string c) {
            using var md5 = MD5.Create(); using var s = File.OpenRead(c); return BitConverter.ToString(await md5.ComputeHashAsync(s));
        }
        #endregion
    }
}
