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

    }
}
