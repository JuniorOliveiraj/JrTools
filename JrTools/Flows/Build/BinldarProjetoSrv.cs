using JrTools.Enums;
using JrTools.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Flows.Build
{
    public class BinldarProjetoSrv
    {
        public async Task BuildarProjetoAsync(string caminhoSln, AcaoBuild acao, IProgress<string>? progresso = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    string msbuildExe = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe";

                    if (!File.Exists(msbuildExe))
                        throw new FileNotFoundException("MSBuild.exe não encontrado.", msbuildExe);

                    string target = acao switch
                    {
                        AcaoBuild.Build => "Build",
                        AcaoBuild.Limpar => "Clean",
                        AcaoBuild.Rebuild => "Rebuild",
                        _ => "Build"
                    };

                    string args = $"\"{caminhoSln}\" /t:{target} /p:Configuration=Release";

                    var psi = new ProcessStartInfo
                    {
                        FileName = msbuildExe,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var processo = new Process { StartInfo = psi, EnableRaisingEvents = true })
                    {
                        var tcs = new TaskCompletionSource<bool>();

                        processo.OutputDataReceived += (s, e) => { if (e.Data != null) progresso?.Report(e.Data); };
                        processo.ErrorDataReceived += (s, e) => { if (e.Data != null) progresso?.Report("[ERRO] " + e.Data); };
                        processo.Exited += (s, e) => tcs.SetResult(true);

                        progresso?.Report($"[INFO] Iniciando {acao.ToString().ToLower()} da solução: {caminhoSln}");
                        processo.Start();
                        processo.BeginOutputReadLine();
                        processo.BeginErrorReadLine();

                        await tcs.Task; // espera terminar

                        if (processo.ExitCode != 0)
                            throw new FluxoException($"MSBuild terminou com erro. Código de saída: {processo.ExitCode}");

                        progresso?.Report($"[INFO] {acao.ToString()} concluído com sucesso!");
                    }
                }
                catch (Exception ex)
                {
                    progresso?.Report($"[ERRO] Falha ao executar a ação {acao.ToString().ToLower()} no projeto: {ex.Message}");
                    throw new FluxoException($"[ERRO] Falha ao executar a ação {acao.ToString().ToLower()} no projeto: {ex.Message}");
                }
            });
        }
    }
}
