using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public static class SmartInstallerDaemon
    {
        private static Process? _daemonProcess;
        private static string? _currentWebAppPath;
        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private static TaskCompletionSource<bool>? _readyTcs;
        private static TaskCompletionSource<int>? _commandTcs;
        private static Action<string>? _currentLineCallback;
        private static readonly object _stateLock = new object();

        public static async Task<bool> EnsureRunningAsync(string webAppPath, IProgress<string>? progresso = null)
        {
            if (_daemonProcess != null && !_daemonProcess.HasExited && string.Equals(_currentWebAppPath, webAppPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            await _lock.WaitAsync();
            try
            {
                if (_daemonProcess != null && !_daemonProcess.HasExited && string.Equals(_currentWebAppPath, webAppPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                await StopDaemonInternalAsync();

                string installerExe = ResolverInstallerExe(webAppPath);
                if (!File.Exists(installerExe))
                {
                    progresso?.Report($"[DAEMON ERRO] Executável não encontrado: {installerExe}");
                    return false;
                }

                progresso?.Report($"[SMART DAEMON] Inicializando Worker Persistente para: {webAppPath}...");

                _readyTcs = new TaskCompletionSource<bool>();

                var psi = new ProcessStartInfo
                {
                    FileName = installerExe,
                    Arguments = $"server -a \"{webAppPath}\"",
                    WorkingDirectory = Path.GetDirectoryName(installerExe) ?? AppDomain.CurrentDomain.BaseDirectory,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

                proc.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(e.Data)) return;
                    string line = e.Data;

                    if (line.Contains("[SERVER_READY]"))
                    {
                        _readyTcs?.TrySetResult(true);
                    }

                    lock (_stateLock)
                    {
                        _currentLineCallback?.Invoke(line);

                        if (line.Contains("[CMD_END:SUCCESS]"))
                        {
                            _commandTcs?.TrySetResult(0);
                        }
                        else if (line.Contains("[CMD_END:ERROR"))
                        {
                            _commandTcs?.TrySetResult(1);
                        }
                    }
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        lock (_stateLock)
                        {
                            _currentLineCallback?.Invoke($"[DAEMON STDERR]: {e.Data}");
                        }
                    }
                };

                proc.Exited += (s, e) =>
                {
                    lock (_stateLock)
                    {
                        _readyTcs?.TrySetResult(false);
                        _commandTcs?.TrySetResult(proc.ExitCode);
                    }
                };

                if (!proc.Start())
                {
                    progresso?.Report("[DAEMON ERRO] Falha ao iniciar processo worker.");
                    return false;
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                _daemonProcess = proc;
                _currentWebAppPath = webAppPath;

                // Aguarda o [SERVER_READY] com timeout de 45 segundos
                var readyTimeoutTask = Task.Delay(TimeSpan.FromSeconds(45));
                var completedTask = await Task.WhenAny(_readyTcs.Task, readyTimeoutTask);

                if (completedTask == _readyTcs.Task && await _readyTcs.Task)
                {
                    progresso?.Report("[SMART DAEMON] Worker Persistente conectado e pronto!");
                    return true;
                }
                else
                {
                    progresso?.Report("[SMART DAEMON AVISO] Timeout aguardando inicialização do worker. Tentando continuar...");
                    return false;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task<int> ExecuteCommandAsync(string webAppPath, string command, IProgress<string>? progresso = null, Action<string>? onLine = null)
        {
            bool ready = await EnsureRunningAsync(webAppPath, progresso);
            if (!ready || _daemonProcess == null || _daemonProcess.HasExited)
            {
                progresso?.Report("[SMART DAEMON] Worker indisponível. Executando comando em modo isolado...");
                return -1; // Sinaliza para fallback
            }

            await _lock.WaitAsync();
            try
            {
                if (_daemonProcess == null || _daemonProcess.HasExited)
                {
                    return -1;
                }

                var cmdTcs = new TaskCompletionSource<int>();
                lock (_stateLock)
                {
                    _commandTcs = cmdTcs;
                    _currentLineCallback = line =>
                    {
                        progresso?.Report(line);
                        onLine?.Invoke(line);
                    };
                }

                await _daemonProcess.StandardInput.WriteLineAsync(command);
                await _daemonProcess.StandardInput.FlushAsync();

                return await cmdTcs.Task;
            }
            finally
            {
                lock (_stateLock)
                {
                    _currentLineCallback = null;
                    _commandTcs = null;
                }
                _lock.Release();
            }
        }

        public static async Task StopDaemonAsync()
        {
            await _lock.WaitAsync();
            try
            {
                await StopDaemonInternalAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task StopDaemonInternalAsync()
        {
            if (_daemonProcess != null)
            {
                try
                {
                    if (!_daemonProcess.HasExited)
                    {
                        await _daemonProcess.StandardInput.WriteLineAsync("EXIT");
                        await _daemonProcess.StandardInput.FlushAsync();
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await _daemonProcess.WaitForExitAsync(cts.Token);
                    }
                }
                catch { }

                try
                {
                    if (!_daemonProcess.HasExited)
                    {
                        _daemonProcess.Kill();
                    }
                }
                catch { }

                _daemonProcess.Dispose();
                _daemonProcess = null;
                _currentWebAppPath = null;
            }
        }

        public static string ResolverInstallerExe(string webAppPath)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidateLocal = Path.Combine(baseDir, "BennerSmartInstaller.exe");
            string candidateDebug = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\BennerSmartInstaller\bin\Debug\BennerSmartInstaller.exe"));
            string candidateRelease = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\BennerSmartInstaller\bin\Release\BennerSmartInstaller.exe"));
            string binInstaller = Path.Combine(webAppPath, "Bin", "BennerSmartInstaller.exe");

            string? sourceInstaller = null;
            if (File.Exists(candidateLocal)) sourceInstaller = candidateLocal;
            if (File.Exists(candidateDebug) && (sourceInstaller == null || File.GetLastWriteTimeUtc(candidateDebug) > File.GetLastWriteTimeUtc(sourceInstaller)))
            {
                sourceInstaller = candidateDebug;
            }
            if (File.Exists(candidateRelease) && (sourceInstaller == null || File.GetLastWriteTimeUtc(candidateRelease) > File.GetLastWriteTimeUtc(sourceInstaller)))
            {
                sourceInstaller = candidateRelease;
            }

            if (File.Exists(sourceInstaller))
            {
                try
                {
                    if (!File.Exists(binInstaller) || File.GetLastWriteTimeUtc(sourceInstaller) > File.GetLastWriteTimeUtc(binInstaller))
                    {
                        File.Copy(sourceInstaller, binInstaller, true);
                    }
                }
                catch { }
            }

            return File.Exists(binInstaller) ? binInstaller : (sourceInstaller ?? candidateLocal);
        }
    }
}
