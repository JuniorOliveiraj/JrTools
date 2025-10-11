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
    public class BuildarDelphiSrv
    {
        public async Task BuildarProjetoDelphiAsync(
            string caminhoGroupProj,
            IProgress<string>? progresso = null,
            string? args = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    // Caminhos padrão — ajuste conforme seu ambiente
                    string rsvarsBat = @"C:\Program Files (x86)\Embarcadero\Studio\17.0\bin\rsvars.bat";
                    string msbuildExe = @"C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\MSBuild.exe";

                    if (!File.Exists(rsvarsBat))
                        throw new FileNotFoundException("Arquivo rsvars.bat não encontrado.", rsvarsBat);

                    if (!File.Exists(msbuildExe))
                        throw new FileNotFoundException("MSBuild.exe não encontrado.", msbuildExe);

                    if (!File.Exists(caminhoGroupProj))
                        throw new FileNotFoundException("Arquivo .groupproj não encontrado.", caminhoGroupProj);

                    if (string.IsNullOrEmpty(args))
                        args = $"\"{caminhoGroupProj}\" /nologo /verbosity:normal /p:DCC_Hints=false /p:DCC_Warnings=false";

                    // Monta o comando em lote
                    string cmdScript = $@"
                    @echo off
                        call ""{rsvarsBat}""
                        ""{msbuildExe}"" {args}
                    ";

                    // Cria um arquivo temporário .cmd para executar o build
                    string tempCmd = Path.Combine(Path.GetTempPath(), $"build_delphi_{Guid.NewGuid():N}.cmd");
                    await File.WriteAllTextAsync(tempCmd, cmdScript, Encoding.Default);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{tempCmd}\"",
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
                        processo.Exited += (s, e) => tcs.TrySetResult(true);

                        progresso?.Report($"[INFO] Iniciando build Delphi: {caminhoGroupProj}");
                        processo.Start();
                        processo.BeginOutputReadLine();
                        processo.BeginErrorReadLine();

                        await tcs.Task;

                        if (processo.ExitCode != 0)
                            throw new FluxoException($"MSBuild Delphi terminou com erro. Código de saída: {processo.ExitCode}");

                        progresso?.Report("[INFO] Build Delphi concluído com sucesso!");
                    }

                    File.Delete(tempCmd);
                }
                catch (Exception ex)
                {
                    progresso?.Report($"[ERRO] Falha ao buildar Delphi: {ex.Message}");
                    throw new FluxoException($"[ERRO] Falha ao buildar Delphi: {ex.Message}");
                }
            });
        }
    }
}

