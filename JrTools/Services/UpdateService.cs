using JrTools.Dto;
using JrTools.Services.Db;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JrTools.Services
{
    /// <summary>
    /// Checa releases publicadas em github.com/JuniorOliveiraj/JrTools (repo público, sem
    /// necessidade de token), baixa o zip mais novo e troca os arquivos da instalação atual.
    /// </summary>
    public class UpdateService
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/JuniorOliveiraj/JrTools/releases?per_page=1";
        private static readonly TimeSpan IntervaloMinimoEntreChecagens = TimeSpan.FromMinutes(1);

        private static readonly HttpClient _http = CriarHttpClient();

        private readonly IProcessLauncher _processLauncher;
        private readonly Action _sairDoApp;

        public UpdateService() : this(null, null) { }

        /// <summary>
        /// Construtor interno para injeção de dependência em testes (mesmo padrão usado em
        /// <see cref="BinarioDelphiExplorerViewModel"/>).
        /// </summary>
        internal UpdateService(IProcessLauncher? processLauncher, Action? sairDoApp)
        {
            _processLauncher = processLauncher ?? new ProcessLauncherImpl();
            _sairDoApp = sairDoApp ?? (() => Microsoft.UI.Xaml.Application.Current.Exit());
        }

        private static HttpClient CriarHttpClient()
        {
            var http = new HttpClient();
            // A API do GitHub exige um User-Agent, senão responde 403.
            http.DefaultRequestHeaders.UserAgent.ParseAdd("JrTools-AutoUpdate");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return http;
        }

        /// <summary>
        /// Tag da build atualmente instalada, lida de version.txt ao lado do executável.
        /// Null quando o arquivo não existe (ex.: build local/debug) — nesse caso a checagem
        /// de atualização é pulada por completo.
        /// </summary>
        public static string? VersaoAtual() => VersaoAtual(AppContext.BaseDirectory);

        internal static string? VersaoAtual(string baseDirectory)
        {
            var path = Path.Combine(baseDirectory, "version.txt");
            if (!File.Exists(path)) return null;
            try
            {
                var texto = File.ReadAllText(path).Trim();
                return string.IsNullOrWhiteSpace(texto) ? null : texto;
            }
            catch { return null; }
        }

        internal static int ExtrairRunNumber(string tag)
        {
            var match = Regex.Match(tag, @"^build-(\d+)-");
            return match.Success ? int.Parse(match.Groups[1].Value) : -1;
        }

        /// <summary>
        /// Decide, a partir do JSON de <c>GET /releases?per_page=1</c>, se há uma versão mais
        /// nova que <paramref name="runAtual"/> com um asset .zip. Não depende de rede — toda a
        /// lógica de comparação fica isolada aqui para poder ser testada com JSONs de exemplo.
        /// </summary>
        internal static AtualizacaoDisponivelDto? AnalisarRelease(string json, int runAtual)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    return null;

                var release = doc.RootElement[0];
                var tag = release.GetProperty("tag_name").GetString();
                if (string.IsNullOrWhiteSpace(tag)) return null;

                var runNovo = ExtrairRunNumber(tag);
                if (runNovo < 0 || runNovo <= runAtual) return null;

                string? downloadUrl = null;
                if (release.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var nome = asset.GetProperty("name").GetString();
                        if (nome != null && nome.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(downloadUrl)) return null;

                var htmlUrl = release.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() ?? string.Empty : string.Empty;

                return new AtualizacaoDisponivelDto
                {
                    Tag = tag,
                    RunNumber = runNovo,
                    DownloadUrl = downloadUrl,
                    HtmlUrl = htmlUrl
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Consulta a última release do GitHub e retorna os dados da atualização se houver
        /// uma versão mais nova que a instalada. Só deve ser chamado uma vez, na abertura do
        /// app — não é feito nenhum tipo de polling periódico aqui.
        /// </summary>
        public async Task<AtualizacaoDisponivelDto?> VerificarAsync()
        {
            var versaoAtual = VersaoAtual();
            if (versaoAtual == null) return null;

            var runAtual = ExtrairRunNumber(versaoAtual);
            if (runAtual < 0) return null;

            var cfg = await AtualizacaoHelper.LerAsync();
            if (DateTime.UtcNow - cfg.UltimaVerificacaoUtc < IntervaloMinimoEntreChecagens)
                return null;

            cfg.UltimaVerificacaoUtc = DateTime.UtcNow;
            await AtualizacaoHelper.SalvarAsync(cfg);

            try
            {
                var json = await _http.GetStringAsync(ReleasesApiUrl);
                return AnalisarRelease(json, runAtual);
            }
            catch
            {
                // Sem rede, GitHub fora do ar, rate limit, etc. — falha silenciosa, o app
                // continua funcionando normalmente sem atualização.
                return null;
            }
        }

        /// <summary>
        /// Baixa o zip da release e extrai para uma pasta de staging em %TEMP%. Retorna o
        /// caminho da pasta extraída, pronta para o updater aplicar por cima da instalação.
        /// </summary>
        public async Task<string> BaixarEExtrairAsync(AtualizacaoDisponivelDto atualizacao, IProgress<string>? progresso = null)
        {
            var pastaBase = Path.Combine(Path.GetTempPath(), "JrTools_Update");
            Directory.CreateDirectory(pastaBase);

            var zipPath = Path.Combine(pastaBase, $"{atualizacao.Tag}.zip");
            var stagingDir = Path.Combine(pastaBase, atualizacao.Tag);

            progresso?.Report($"[ATUALIZAÇÃO] Baixando {atualizacao.Tag}...");

            using (var response = await _http.GetAsync(atualizacao.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long tamanhoTotal = response.Content.Headers.ContentLength ?? -1;
                long totalBaixado = 0;

                using var origem = await response.Content.ReadAsStreamAsync();
                using var destino = new FileStream(zipPath, FileMode.Create, FileAccess.Write);

                byte[] buffer = new byte[81920];
                int bytesLidos;
                while ((bytesLidos = await origem.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destino.WriteAsync(buffer, 0, bytesLidos);
                    totalBaixado += bytesLidos;

                    if (tamanhoTotal > 0)
                    {
                        int pct = (int)((totalBaixado * 100) / tamanhoTotal);
                        progresso?.Report($"[ATUALIZAÇÃO] Baixando... {pct}%");
                    }
                }
            }

            progresso?.Report("[ATUALIZAÇÃO] Validando arquivo baixado...");
            try
            {
                using var teste = ZipFile.OpenRead(zipPath);
                _ = teste.Entries.Count;
            }
            catch (Exception ex)
            {
                File.Delete(zipPath);
                throw new InvalidOperationException("O arquivo baixado está corrompido. Tente novamente.", ex);
            }

            if (Directory.Exists(stagingDir))
                Directory.Delete(stagingDir, recursive: true);

            progresso?.Report("[ATUALIZAÇÃO] Extraindo...");
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, stagingDir, overwriteFiles: true));

            return stagingDir;
        }

        /// <summary>
        /// Monta o conteúdo do script PowerShell que espera o processo <paramref name="pid"/>
        /// terminar, espelha <paramref name="stagingDir"/> por cima de <paramref name="installDir"/>
        /// via robocopy e relança <paramref name="exePath"/>. Função pura (sem I/O) para poder
        /// ser testada diretamente.
        /// </summary>
        internal static string GerarScript(int pid, string stagingDir, string installDir, string exePath, string zipPath) => $@"
try {{ Wait-Process -Id {pid} -ErrorAction SilentlyContinue }} catch {{}}
Start-Sleep -Seconds 1
robocopy ""{stagingDir}"" ""{installDir}"" /MIR /NFL /NDL /NJH /NJS /R:5 /W:2 | Out-Null
Start-Process -FilePath ""{exePath}""
Start-Sleep -Seconds 2
Remove-Item -LiteralPath ""{stagingDir}"" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath ""{zipPath}"" -Force -ErrorAction SilentlyContinue
";

        /// <summary>
        /// Gera e dispara o script que espera o app fechar, copia os arquivos da pasta de
        /// staging por cima da instalação atual e reabre o app — depois fecha o app atual.
        /// </summary>
        public void PrepararEReiniciar(string stagingDir)
        {
            var installDir = AppContext.BaseDirectory.TrimEnd('\\');
            var exePath = Path.Combine(installDir, "JrTools.exe");
            var zipPath = stagingDir + ".zip";
            var scriptPath = Path.Combine(Path.GetTempPath(), "JrTools_Update", "updater.ps1");
            var pid = Environment.ProcessId;

            var script = GerarScript(pid, stagingDir, installDir, exePath, zipPath);

            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            File.WriteAllText(scriptPath, script);

            _processLauncher.Launch("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"");

            _sairDoApp();
        }

        /// <summary>
        /// Implementação real de <see cref="IProcessLauncher"/> usada em produção.
        /// </summary>
        private sealed class ProcessLauncherImpl : IProcessLauncher
        {
            public void Launch(string fileName, string arguments)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }
    }
}
