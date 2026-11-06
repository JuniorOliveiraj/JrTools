using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class WesService
    {
        private readonly string _wesExePath;

        public WesService(string wesExePath)
        {
            _wesExePath = wesExePath;
        }

        public Task<int> ConfigSetAsync(string servidor, string nomeSistema, string usuario, string senha, IProgress<string> progresso)
            => RunAsync($"config set -h {servidor} -s {nomeSistema} -u {usuario} -p {senha}", progresso, "[WES CONFIG SET]");

        public Task<int> CacheClearAsync(IProgress<string> progresso)
            => RunAsync("cache clear", progresso, "[WES CACHE CLEAR]");

        public Task<int> ArtifactsInstallAsync(IProgress<string> progresso)
            => RunAsync("artifacts install -s", progresso, "[WES ARTIFACTS INSTALL]");

        public Task<int> ArtifactsInstallLayerAsync(string layer, IProgress<string> progresso)
            => RunAsync($"artifacts install -l {layer} -s -v", progresso, $"[WES ARTIFACTS INSTALL: {layer.ToUpper()}]");

        public Task<int> PagesGenerateAsync(IProgress<string> progresso)
            => RunAsync("pages generate", progresso, "[WES PAGES GENERATE]");

        public async Task<int> ArtifactsInstallSelectiveAsync(string webAppPath, List<string> relativeFiles, IProgress<string> progresso)
        {
            string installerExe = SmartInstallerDaemon.ResolverInstallerExe(webAppPath);

            if (File.Exists(installerExe))
            {
                string? tempFile = null;
                string artifactsArg;
                if (relativeFiles.Count > 5 || string.Join(";", relativeFiles).Length > 1000)
                {
                    tempFile = Path.Combine(Path.GetTempPath(), $"smart_install_files_{Guid.NewGuid():N}.txt");
                    await File.WriteAllLinesAsync(tempFile, relativeFiles);
                    artifactsArg = tempFile;
                }
                else
                {
                    artifactsArg = string.Join(";", relativeFiles);
                }

                try
                {
                    // Tenta executar via Worker Persistente (0ms boot, mantém conexão viva)
                    int daemonExitCode = await SmartInstallerDaemon.ExecuteCommandAsync(webAppPath, $"INSTALL {artifactsArg}", progresso);
                    if (daemonExitCode >= 0)
                    {
                        return daemonExitCode;
                    }

                    // Fallback para processo isolado
                    progresso?.Report("[SMART INSTALL] Executando em processo isolado de fallback...");
                    string args = $"install -a \"{webAppPath}\" -f \"{artifactsArg}\"";
                    return await RunProcessAsync(installerExe, args, progresso, "[SMART INSTALL SELETIVO]");
                }
                finally
                {
                    if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                }
            }

            // Fallback para wes.exe nativo caso o utilitário externo não esteja presente
            progresso?.Report("[WES ARTIFACTS] BennerSmartInstaller.exe não localizado. Instalando artefatos da camada específico via wes.exe...");
            return await ArtifactsInstallLayerAsync("especifico", progresso);
        }

        public async Task<Dictionary<string, string>> ArtifactsCompareAsync(string webAppPath, IProgress<string>? progresso = null)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string installerExe = SmartInstallerDaemon.ResolverInstallerExe(webAppPath);

            if (!File.Exists(installerExe)) return result;

            void ParseCompareLine(string? line)
            {
                if (line != null && line.Contains("[COMPARE_ITEM]"))
                {
                    int idx = line.IndexOf("[COMPARE_ITEM]");
                    string itemPart = line.Substring(idx + "[COMPARE_ITEM]".Length).Trim();
                    var parts = itemPart.Split(';');
                    string? name = null;
                    string? status = null;
                    foreach (var part in parts)
                    {
                        var kv = part.Split('=');
                        if (kv.Length == 2)
                        {
                            if (kv[0].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase)) name = kv[1].Trim();
                            else if (kv[0].Trim().Equals("Status", StringComparison.OrdinalIgnoreCase)) status = kv[1].Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(status))
                    {
                        result[name] = status;
                    }
                }
            }

            // 1. Tenta executar via Worker Persistente
            int daemonExitCode = await SmartInstallerDaemon.ExecuteCommandAsync(webAppPath, "COMPARE", progresso, ParseCompareLine);
            if (daemonExitCode >= 0)
            {
                return result;
            }

            // 2. Fallback para processo isolado
            progresso?.Report("[SMART COMPARE] Executando comparação em processo isolado de fallback...");
            string args = $"compare -a \"{webAppPath}\"";
            var compareProgress = new Progress<string>(line =>
            {
                progresso?.Report(line);
                ParseCompareLine(line);
            });

            await RunProcessAsync(installerExe, args, compareProgress, "[COMPARE ARTIFACTS]");
            return result;
        }

        private static string ResolverInstallerExe(string webAppPath) => SmartInstallerDaemon.ResolverInstallerExe(webAppPath);

        private Task<int> RunProcessAsync(string exePath, string arguments, IProgress<string> progresso, string titulo)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = System.IO.Path.GetDirectoryName(exePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var tcs = new TaskCompletionSource<bool>();
            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report($"[ERRO]: {e.Data}"); };
            process.Exited += (s, e) => tcs.TrySetResult(true);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return Task.Run(async () =>
            {
                using (process)
                {
                    await tcs.Task;
                    await process.WaitForExitAsync();
                    return process.ExitCode;
                }
            });
        }

        private async Task<int> RunAsync(string arguments, IProgress<string> progresso, string titulo)
        {
            progresso?.Report($"{titulo} iniciando...");

            var psi = new ProcessStartInfo
            {
                FileName = _wesExePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(_wesExePath),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var tcs = new TaskCompletionSource<bool>();
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report($"{titulo}: {e.Data}"); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) progresso?.Report($"{titulo} [ERRO]: {e.Data}"); };
            process.Exited += (s, e) => tcs.TrySetResult(true);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await tcs.Task;
            await process.WaitForExitAsync();

            progresso?.Report(process.ExitCode == 0
                ? $"{titulo} concluído com sucesso."
                : $"{titulo} finalizou com código {process.ExitCode}.");

            return process.ExitCode;
        }
    }
}
