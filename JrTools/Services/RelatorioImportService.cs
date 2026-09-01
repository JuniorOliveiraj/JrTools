using JrTools.Dto;
using JrTools.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class RelatorioImportService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        // Campos que o Benner regrava a cada exportação/salvamento do .rpt mesmo sem
        // nenhuma mudança real no relatório — HANDLE é o id interno do registro (varia
        // entre bases/ambientes), ALTERADOPOR/ULTIMAALTERACAO são metadados de quem/quando
        // mexeu por último. Sem ignorá-los, todo relatório aparecia como "Diferente" o
        // tempo todo, mesmo quando o conteúdo em si não mudou.
        private static readonly string[] CamposVolateis = { "HANDLE", "ALTERADOPOR", "ULTIMAALTERACAO" };

        public string CalcularHash(string caminho)
        {
            // Lê como Latin1 (mapeamento 1:1 de byte pra char) só para filtrar linhas por
            // prefixo ASCII com segurança — não depende de acertar a codificação real do
            // arquivo (esses .rpt legados costumam vir em ANSI/Windows-1252) nem risca
            // corromper acentuação, já que as linhas que sobram não são reescritas, só
            // concatenadas de volta.
            var linhas = File.ReadAllLines(caminho, Encoding.Latin1);

            var relevantes = linhas.Where(linha =>
            {
                var semIndentacao = linha.TrimStart('\t', ' ');
                foreach (var campo in CamposVolateis)
                {
                    if (semIndentacao.StartsWith(campo + "|", StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                return true;
            });

            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(string.Join('\n', relevantes)));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public async Task<Dictionary<string, LogDeImportacao>> LerLogAsync(string sistema)
        {
            var caminho = ObterCaminhoLog(sistema);

            if (!File.Exists(caminho))
                return new Dictionary<string, LogDeImportacao>();

            try
            {
                var json = await File.ReadAllTextAsync(caminho, Encoding.UTF8);
                var resultado = JsonSerializer.Deserialize<Dictionary<string, LogDeImportacao>>(json, _jsonOptions);
                return resultado ?? new Dictionary<string, LogDeImportacao>();
            }
            catch
            {
                return new Dictionary<string, LogDeImportacao>();
            }
        }

        public async Task SalvarLogAsync(string sistema, Dictionary<string, LogDeImportacao> log)
        {
            var caminho = ObterCaminhoLog(sistema);
            var diretorio = Path.GetDirectoryName(caminho);

            if (!string.IsNullOrEmpty(diretorio) && !Directory.Exists(diretorio))
                Directory.CreateDirectory(diretorio);

            var json = JsonSerializer.Serialize(log, _jsonOptions);
            await File.WriteAllTextAsync(caminho, json, Encoding.UTF8);
        }

        public async Task<List<RelatorioItem>> CarregarRelatoriosAsync(string diretorio, string sistema)
        {
            if (!Directory.Exists(diretorio))
                return [];

            var log = await LerLogAsync(sistema);

            return await Task.Run(() =>
            {
                var resultado = new List<RelatorioItem>();
                var arquivos = Directory.GetFiles(diretorio, "*.rpt", SearchOption.AllDirectories);

                foreach (var arquivo in arquivos)
                {
                    var nome = Path.GetFileName(arquivo);
                    string hash;
                    try { hash = CalcularHash(arquivo); }
                    catch { continue; }

                    StatusRelatorio status;
                    if (!log.TryGetValue(nome, out var entrada))
                        status = StatusRelatorio.Novo;
                    else if (entrada.Hash != hash)
                        status = StatusRelatorio.Diferente;
                    else
                        status = StatusRelatorio.Atualizado;

                    resultado.Add(new RelatorioItem
                    {
                        Nome = nome,
                        Caminho = arquivo,
                        Hash = hash,
                        Status = status,
                        Selecionado = false
                    });
                }

                return resultado;
            });
        }

        public List<string> ValidarConfiguracoes(ConfiguracaoRh config)
        {
            var erros = new List<string>();

            if (string.IsNullOrWhiteSpace(config?.CaminhoCSReportImport))
            {
                erros.Add("Caminho do CSReportImport.exe não informado.");
            }
            else
            {
                if (!File.Exists(config.CaminhoCSReportImport))
                    erros.Add($"Arquivo não encontrado: {config.CaminhoCSReportImport}");

                if (!config.CaminhoCSReportImport.EndsWith("CSReportImport.exe", StringComparison.OrdinalIgnoreCase))
                    erros.Add("O caminho informado não aponta para CSReportImport.exe.");
            }

            if (string.IsNullOrWhiteSpace(config?.CaminhoRelatorios))
                erros.Add("Caminho dos relatórios não informado.");
            else if (!Directory.Exists(config.CaminhoRelatorios))
                erros.Add($"Diretório de relatórios não encontrado: {config.CaminhoRelatorios}");

            if (string.IsNullOrWhiteSpace(config?.Servidor))
                erros.Add("Servidor não informado.");

            if (string.IsNullOrWhiteSpace(config?.Sistema))
                erros.Add("Sistema não informado.");

            if (string.IsNullOrWhiteSpace(config?.Usuario))
                erros.Add("Usuário não informado.");

            if (string.IsNullOrWhiteSpace(config?.Senha))
                erros.Add("Senha não informada.");

            return erros;
        }

        public async Task<ResultadoImportacao> ImportarRelatorioAsync(RelatorioItem relatorio, ConfiguracaoRh config)
        {
            var args = $"\"server={config.Servidor}\" \"system={config.Sistema}\" \"file={relatorio.Caminho}\" \"user={config.Usuario}\" \"pass={config.Senha}\"";

            var psi = new ProcessStartInfo(config.CaminhoCSReportImport, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var processo = Process.Start(psi);
            var saida = await processo.StandardOutput.ReadToEndAsync();
            await processo.WaitForExitAsync();

            return new ResultadoImportacao
            {
                Saida = saida,
                ExitCode = processo.ExitCode
            };
        }

        private static string ObterCaminhoLog(string sistema)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "JrTools", $"RelacaoRelatorios_{sistema}.json");
        }
    }
}
