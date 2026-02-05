using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class GitService
    {
        public async Task<string> RunCommandAsync(ProcessStartInfoGitDataobject startInfoData)
        {
            var output = new StringBuilder();

            try
            {
                if (!Directory.Exists(startInfoData.WorkingDirectory))
                {
                    return $"[ERRO] Diretório não encontrado: {startInfoData.WorkingDirectory}";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = startInfoData.FileName,
                    Arguments = startInfoData.Arguments,
                    RedirectStandardOutput = startInfoData.RedirectStandardOutput,
                    RedirectStandardError = startInfoData.RedirectStandardError,
                    UseShellExecute = startInfoData.UseShellExecute,
                    CreateNoWindow = startInfoData.CreateNoWindow,
                    WorkingDirectory = startInfoData.WorkingDirectory
                };

                using var process = new Process { StartInfo = startInfo };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine("ERROR: " + e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                return output.ToString();
            }
            catch (Exception ex)
            {
                return $"[EXCEÇÃO] {ex.Message}";
            }
        }

        public async Task<string> RunCommandWithProgressAsync(
            string arguments,
            string workingDirectory,
            IProgress<string> progress = null,
            string commandTitle = "GIT")
        {
            if (!Directory.Exists(workingDirectory))
            {
                var msg = $"[ERRO] Diretório não encontrado: {workingDirectory}";
                progress?.Report(msg);
                throw new DirectoryNotFoundException(msg);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var outputBuilder = new StringBuilder();
            var tcs = new TaskCompletionSource<bool>();

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    progress?.Report($"{commandTitle}: {e.Data}");
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    progress?.Report($"{commandTitle} [MSG/ERRO]: {e.Data}");
                }
            };

            process.Exited += (s, e) => tcs.TrySetResult(true);

            progress?.Report($"{commandTitle} iniciando em {workingDirectory}...");

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await tcs.Task;
                await process.WaitForExitAsync(); 
            }
            catch (Exception ex)
            {
                progress?.Report($"[EXCEPTION] {ex.Message}");
                throw;
            }

            if (process.ExitCode != 0)
            {
                throw new Exception($"{commandTitle} terminou com erro. Código de saída: {process.ExitCode}");
            }

            progress?.Report($"{commandTitle} concluído.");
            return outputBuilder.ToString();
        }

    }
}
