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
        public async Task BuildarProjetoAsync(string caminhoSln, IProgress<string>? progresso = null, string? args = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    string msbuildExe = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\MSBuild.exe";

                    if (!File.Exists(msbuildExe))
                        throw new FileNotFoundException("MSBuild.exe não encontrado.", msbuildExe);

                    if (args == null || string.IsNullOrEmpty(args))
                        args = $"\"{caminhoSln}\" /t:Build /p:Configuration=Release";

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

                        progresso?.Report($"[INFO] Iniciando build da solução: {caminhoSln}");
                        processo.Start();
                        processo.BeginOutputReadLine();
                        processo.BeginErrorReadLine();

                        await tcs.Task; // espera terminar

                        if (processo.ExitCode != 0)
                            throw new FluxoException($"MSBuild terminou com erro. Código de saída: {processo.ExitCode}");

                        progresso?.Report("[INFO] Build concluído com sucesso!");
                    }
                }
                catch (Exception ex)
                {
                    progresso?.Report($"[ERRO] Falha ao buildar projeto: {ex.Message}");
                    throw new FluxoException($"[ERRO] Falha ao buildar projeto: {ex.Message}");
                    throw;
                }
            });
        }


    }
}
