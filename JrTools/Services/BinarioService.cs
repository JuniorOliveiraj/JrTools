using JrTools.Dto;
using JrTools.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class BinarioService
    {
        private readonly string _binPath = @"\\bnu-rhslave001\public";
        private readonly string _pastaTemporaria = Path.Combine(Path.GetTempPath(), "BinariosTemp");

        public async Task<BinarioInfoDataObject?> ObterBinarioAsync(string versao)
        {
            return await Task.Run(() =>
            {
                var arquivos = Directory.GetFiles(_binPath, "Bin_*.zip");

                foreach (var arquivo in arquivos)
                {
                    var nome = Path.GetFileNameWithoutExtension(arquivo);
                    var match = Regex.Match(nome, @"^Bin_(.+)$");

                    if (match.Success)
                    {
                        var branchExtraida = match.Groups[1].Value;

                        if (branchExtraida.Equals(versao, StringComparison.OrdinalIgnoreCase))
                        {
                            return new BinarioInfoDataObject
                            {
                                NomeOriginal = nome,
                                Caminho = arquivo,
                                Branch = branchExtraida
                            };
                        }
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// Copia o zip do binário para a pasta temporária e extrai para a pasta de destino, sem travar a UI.
        /// Cria barra de progresso na cópia.
        /// Limpa a pasta de destino antes da extração.
        /// </summary>
        public async Task ExtrairBinarioAsync(BinarioInfoDataObject binarioInfo, IProgress<string>? progresso = null)
        {
            try
            {
                progresso?.Report("[INFO] Garantindo pastas temporária e de destino...");

                if (!Directory.Exists(_pastaTemporaria))
                    Directory.CreateDirectory(_pastaTemporaria);

                if (!Directory.Exists(binarioInfo.destino))
                    Directory.CreateDirectory(binarioInfo.destino);

                string pastaAplicacaoWes = ObterPastaAplicacaoWes(binarioInfo.Branch);
                string pastaDestino = Path.Combine(binarioInfo.destino, "Delphi");
                string pastaDestinoWes = Path.Combine(binarioInfo.destino, pastaAplicacaoWes);

                Directory.CreateDirectory(pastaDestino);
                Directory.CreateDirectory(pastaDestinoWes);

                string caminhoTemporario = Path.Combine(_pastaTemporaria, binarioInfo.NomeOriginal + ".zip");

                // Verifica se o arquivo já existe na pasta temporária
                if (!File.Exists(caminhoTemporario))
                {
                    progresso?.Report($"[INFO] Copiando {binarioInfo.Caminho} para {caminhoTemporario}...");

                    long tamanhoTotal = new FileInfo(binarioInfo.Caminho).Length;
                    long totalCopiado = 0;
                    int blocoTamanho = 81920;

                    using (var origem = new FileStream(binarioInfo.Caminho, FileMode.Open, FileAccess.Read))
                    using (var destino = new FileStream(caminhoTemporario, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[blocoTamanho];
                        int bytesLidos;
                        while ((bytesLidos = await origem.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await destino.WriteAsync(buffer, 0, bytesLidos);
                            totalCopiado += bytesLidos;

                            int percentual = (int)((totalCopiado * 100) / tamanhoTotal);
                            int totalBarras = 30;
                            int barrasPreenchidas = (percentual * totalBarras) / 100;
                            string barra = new string('#', barrasPreenchidas) + new string('-', totalBarras - barrasPreenchidas);

                            progresso?.Report($"[{barra}] {percentual}%");
                        }
                    }

                    progresso?.Report("[INFO] Arquivo copiado com sucesso.");
                }
                else
                {
                    progresso?.Report("[INFO] Arquivo já existe na pasta temporária. Usando arquivo existente.");
                }

                progresso?.Report($"[INFO] Limpando pasta de destino... {pastaDestino}");
                await LimparPastaDestinoAsync(pastaDestino, progresso);
                progresso?.Report($"[INFO] Limpando pasta de destino... {pastaDestinoWes}");
                await LimparPastaDestinoAsync(pastaDestinoWes, progresso);
                progresso?.Report("[INFO] Pasta de destino limpa.");

                progresso?.Report("[INFO] Pasta de destino Concluída.");
                progresso?.Report($"[INFO] Extraindo {pastaDestino}...");
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(caminhoTemporario, pastaDestino, true);
                });

                progresso?.Report($"[INFO] Extraindo {pastaDestinoWes}...");
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(caminhoTemporario, pastaDestinoWes, true);
                });

                progresso?.Report($"[INFO] Extração concluída: {binarioInfo.destino}");
            }
            catch (Exception ex)
            {
                progresso?.Report($"[ERRO] Falha ao extrair binário: {ex.Message}");
                throw;
            }
        }


        private async Task LimparPastaDestinoAsync(string caminho, IProgress<string>? progresso = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(caminho))
                    {
                        progresso?.Report($"[INFO] Limpando pasta de destino: {caminho}");
                        Directory.Delete(caminho, true);
                    }
                    Directory.CreateDirectory(caminho);
                    progresso?.Report("[INFO] Limpeza da pasta concluída.");
                }
                catch (Exception ex)
                {
                    var fluxEx = new FluxoException($"Não foi possível limpar a pasta {caminho}: {ex.Message}", ex);
                    progresso?.Report($"[ERRO] {fluxEx.Message}");
                    throw fluxEx;
                }
            });
        }



        private string ObterPastaAplicacaoWes(string branch)
        {
            var match = Regex.Match(branch, @"(\d{2}\.\d{2})");

            if (!match.Success)
                throw new InvalidOperationException($"Não foi possível identificar a versão para WES no branch '{branch}'.");

            string versaoCurta = match.Groups[1].Value;
            return $"RH_LOCAL_DESENV_{versaoCurta}";
        }
    }
}
