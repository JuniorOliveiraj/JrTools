using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Pages.Apps
{
    public sealed partial class Cria : Page
    {
        private Process? _runningProcess;

        public Cria()
        {
            InitializeComponent();
            Loaded += Cria_Loaded;
            Unloaded += Cria_Unloaded;

        }
        private void Cria_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_runningProcess != null && !_runningProcess.HasExited)
                {
                    // Tenta fechar com elegância
                    _runningProcess.Kill(entireProcessTree: true);
                    _runningProcess.Dispose();
                    _runningProcess = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao encerrar processo: {ex.Message}");
            }
        }

        private async void Cria_Loaded(object sender, RoutedEventArgs e)
        {
            // Se preferir executar por botão, comente essa linha:
            await RunCriaAppAsync();
        }

        private async Task RunCriaAppAsync()
        {
            string psCommand = @"
                function cria {
                    $app = 'D:\Benner\bin\delphi\CRIA\Cria.exe'
                    if (Test-Path $app) { & $app } else { Write-Error ""Aplicativo não encontrado: $app"" }
                }
                cria
            ";

            try
            {
                await AppendLogAsync("Iniciando processo CRIA...");

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                _runningProcess = process;

                process.OutputDataReceived += async (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await AppendLogAsync(e.Data.Replace("\r", "").Replace("\n", Environment.NewLine));
                };

                process.ErrorDataReceived += async (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        await AppendLogAsync($"[ERRO] {e.Data.Replace("\r", "").Replace("\n", Environment.NewLine)}");
                };

                process.Exited += async (s, e) =>
                {
                    try
                    {
                        var exit = process.ExitCode;
                        await AppendLogAsync($"\nProcesso finalizado. Código de saída: {exit}");
                    }
                    catch (Exception ex)
                    {
                        await AppendLogAsync($"[EXCEÇÃO no Exited] {ex.Message}");
                    }
                    finally
                    {
                        _runningProcess = null;
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());
            }
            catch (Exception ex)
            {
                await AppendLogAsync($"[EXCEÇÃO] {ex.Message}");
            }
        }

        private Task RunOnUiThreadAsync(Action action)
        {
            var tcs = new TaskCompletionSource<object?>();
            var dq = DispatcherQueue;

            if (dq == null)
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                return tcs.Task;
            }

            bool posted = dq.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!posted)
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }

            return tcs.Task;
        }

        private async Task AppendLogAsync(string message)
        {
            await RunOnUiThreadAsync(() =>
            {
                if (string.IsNullOrEmpty(LogsTextBox.Text) || LogsTextBox.Text == "Aguardando início...")
                    LogsTextBox.Text = message;
                else
                    LogsTextBox.Text += Environment.NewLine + message;

                LogsTextBox.SelectionStart = LogsTextBox.Text.Length;
                LogsTextBox.SelectionLength = 0;

                try
                {
                    LogsScrollViewer.ChangeView(null, LogsScrollViewer.ExtentHeight, null, true);
                }
                catch
                {
                    // ignorar erros de scroll
                }
            });
        }
    }
}
