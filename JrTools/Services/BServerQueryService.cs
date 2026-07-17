using JrTools.Models;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JrTools.Services
{
    // Substitui o uso direto de BServerConnectionService nas páginas.
    // Spawna o próprio JrTools.exe em modo headless (--bserver) para carregar as DLLs Benner
    // num processo filho — assim o processo principal nunca trava os arquivos da pasta de binários.
    public static class BServerQueryService
    {
        public static async Task<ConnectionResult> ConsultarAsync(string servidor, string diretorioBinarios)
        {
            var exePath = Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(exePath))
                return Erro("Não foi possível determinar o caminho do executável.");

            var psi = new ProcessStartInfo
            {
                FileName        = exePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow  = true,
                WindowStyle     = ProcessWindowStyle.Hidden
            };
            psi.ArgumentList.Add("--bserver");
            psi.ArgumentList.Add(servidor);
            psi.ArgumentList.Add(diretorioBinarios);

            try
            {
                using var proc = Process.Start(psi)!;
                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (string.IsNullOrWhiteSpace(output))
                    return Erro("Processo auxiliar não retornou dados.");

                var resp = JsonSerializer.Deserialize<BServerResponse>(output.Trim());
                return resp?.Success == true
                    ? new ConnectionResult { IsSuccess = true, AvailableSystems = resp.Systems ?? [] }
                    : Erro(resp?.Error ?? "Resposta inválida do processo auxiliar.");
            }
            catch (Exception ex)
            {
                return Erro($"Falha ao iniciar processo auxiliar: {ex.Message}");
            }
        }

        private static ConnectionResult Erro(string msg) =>
            new ConnectionResult { IsSuccess = false, ErrorMessage = msg };

        private sealed class BServerResponse
        {
            [JsonPropertyName("success")] public bool      Success { get; set; }
            [JsonPropertyName("systems")] public string[]? Systems { get; set; }
            [JsonPropertyName("error")]   public string?   Error   { get; set; }
        }
    }
}
