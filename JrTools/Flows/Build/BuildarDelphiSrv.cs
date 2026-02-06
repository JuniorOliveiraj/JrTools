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
    public class BuildarDelphiSrv
    {
        public async Task BuildarAsync(string caminhoDoProjeto, string msbuildExe, string rsvarsBat, AcaoBuild acao, IProgress<string>? progresso = null)
        {
            await Task.Run(async () =>
            {
                try
                {
                    if (!File.Exists(rsvarsBat))
                        throw new FileNotFoundException("Arquivo rsvars.bat não encontrado.", rsvarsBat);

                    if (!File.Exists(msbuildExe))
                        throw new FileNotFoundException("MSBuild.exe não encontrado.", msbuildExe);

                    if (!File.Exists(caminhoDoProjeto))
                        throw new FileNotFoundException("Arquivo de projeto não encontrado.", caminhoDoProjeto);

                    string target = acao switch
                    {
                        AcaoBuild.Build => "Build",
                        AcaoBuild.Limpar => "Clean",
                        AcaoBuild.Rebuild => "Rebuild",
                        _ => "Build"
                    };

                    // Mantém a mesma configuração utilizada no script PowerShell
                    // DCC_LibraryPath e LU são importantes para o compilador Delphi localizar libs corretamente.
                    string libraryPath = @"C:\Program Files (x86)\Embarcadero\Studio\17.0\lib\win32\release;$(BDSUSERDIR)\Imports;$(BDS)\Imports;$(BDSCOMMONDIR)\Dcp\$(Platform);$(BDS)\include";

                    string args =
                        $"\"{caminhoDoProjeto}\" " +
                        $"/t:{target} " +
                        "/p:Configuration=Release " +
                        "/nologo " +
                        "/verbosity:normal " +
                        "/p:DCC_Hints=false " +
                        "/p:DCC_Warnings=false " +
                        $"/p:DCC_LibraryPath=\"{libraryPath}\" " +
                        "/p:LU=\"\"";

                    string cmdScript = $"""
                        @echo off
                        call "{rsvarsBat}"
                        "{msbuildExe}" {args}
                        """;

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

                        progresso?.Report($"[INFO] Iniciando {acao.ToString().ToLower()} Delphi: {caminhoDoProjeto}");
                        processo.Start();
                        processo.BeginOutputReadLine();
                        processo.BeginErrorReadLine();

                        await tcs.Task;

                        if (processo.ExitCode != 0)
                            throw new FluxoException($"MSBuild Delphi terminou com erro. Código de saída: {processo.ExitCode}");

                        progresso?.Report($"[INFO] {acao.ToString()} Delphi concluído com sucesso!");
                    }

                    File.Delete(tempCmd);
                }
                catch (Exception ex)
                {
                    progresso?.Report($"[ERRO] Falha ao executar a ação {acao.ToString().ToLower()} no projeto Delphi: {ex.Message}");
                    throw new FluxoException($"[ERRO] Falha ao executar a ação {acao.ToString().ToLower()} no projeto Delphi: {ex.Message}");
                }
            });
        }
    }
}
