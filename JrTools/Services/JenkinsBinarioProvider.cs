using JrTools.Dto;
using JrTools.Utils;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class JenkinsBinarioProvider : IBinarioSourceProvider
    {
        private readonly string _baseUrl;
        private readonly string _jobPath;
        private readonly HttpClient _http;

        public JenkinsBinarioProvider(string baseUrl, string jobPath, string usuario, string apiToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("URL base do Jenkins não configurada.", nameof(baseUrl));
            if (string.IsNullOrWhiteSpace(jobPath))
                throw new ArgumentException("Caminho do job Jenkins não configurado.", nameof(jobPath));
            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(apiToken))
                throw new ArgumentException("Usuário/API Token do Jenkins não configurados.");

            _baseUrl = baseUrl.TrimEnd('/');
            _jobPath = jobPath.Trim('/');

            _http = new HttpClient();
            var credenciais = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{usuario}:{apiToken}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credenciais);
        }

        private string MontarJobUrl(string branchLimpo) =>
            $"{_baseUrl}/{_jobPath}/job/{Uri.EscapeDataString(branchLimpo)}";

        public async Task<BinarioInfoDataObject?> ObterBinarioAsync(string branchLimpo, IProgress<string>? progresso = null)
        {
            var jobUrl = MontarJobUrl(branchLimpo);
            var apiUrl = $"{jobUrl}/lastSuccessfulBuild/api/json?tree=number,artifacts[relativePath]";

            progresso?.Report($"[INFO] Consultando Jenkins: {jobUrl}...");

            string json;
            try
            {
                json = await _http.GetStringAsync(apiUrl);
            }
            catch (HttpRequestException ex)
            {
                throw new FluxoException($"Falha ao consultar o Jenkins em '{jobUrl}': {ex.Message}");
            }

            string? relativePath = null;
            using (var doc = JsonDocument.Parse(json))
            {
                foreach (var artefato in doc.RootElement.GetProperty("artifacts").EnumerateArray())
                {
                    var rel = artefato.GetProperty("relativePath").GetString();
                    if (rel != null && rel.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = rel;
                        break;
                    }
                }
            }

            if (relativePath == null)
            {
                progresso?.Report("[ERRO] Nenhum artefato .zip encontrado no último build com sucesso.");
                return null;
            }

            var branchDash = branchLimpo.Replace("/", "-");
            var nomeOriginal = "Bin_" + branchDash;
            var destino = Path.Combine(BinarioService.PastaTemporaria, nomeOriginal + ".zip");

            if (!Directory.Exists(BinarioService.PastaTemporaria))
                Directory.CreateDirectory(BinarioService.PastaTemporaria);

            if (!File.Exists(destino))
            {
                var downloadUrl = $"{jobUrl}/lastSuccessfulBuild/artifact/{relativePath}";
                progresso?.Report($"[INFO] Baixando {downloadUrl}...");
                await BaixarArquivoAsync(downloadUrl, destino, progresso);
                progresso?.Report("[INFO] Download concluído.");
            }
            else
            {
                progresso?.Report("[INFO] Zip já existe na pasta temporária, reutilizando.");
            }

            return new BinarioInfoDataObject
            {
                NomeOriginal = nomeOriginal,
                Caminho = destino,
                Branch = branchDash
            };
        }

        private async Task BaixarArquivoAsync(string url, string destino, IProgress<string>? progresso)
        {
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long tamanhoTotal = response.Content.Headers.ContentLength ?? -1;
                long totalBaixado = 0;

                using var origem = await response.Content.ReadAsStreamAsync();
                using var destinoStream = new FileStream(destino, FileMode.Create, FileAccess.Write);

                byte[] buffer = new byte[81920];
                int bytesLidos;
                while ((bytesLidos = await origem.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destinoStream.WriteAsync(buffer, 0, bytesLidos);
                    totalBaixado += bytesLidos;

                    if (tamanhoTotal > 0)
                    {
                        int pct = (int)((totalBaixado * 100) / tamanhoTotal);
                        int barras = (pct * 30) / 100;
                        progresso?.Report($"[{new string('#', barras)}{new string('-', 30 - barras)}] {pct}%");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw new FluxoException($"Falha ao baixar artefato do Jenkins: {ex.Message}");
            }
        }

        /// <summary>Testa usuário/token e a URL do job (sem depender de branch), para a tela de Configurações.</summary>
        public async Task<bool> TestarConexaoAsync()
        {
            var url = $"{_baseUrl}/{_jobPath}/api/json";
            var response = await _http.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
    }
}
