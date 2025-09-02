using JrTools.Dto;
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
                    // Exemplo: Bin_producao_08.05 ou Bin_dev-08.05.13

                    var match = Regex.Match(nome, @"^Bin_(.+)$");

                    if (match.Success)
                    {
                        var branchExtraida = match.Groups[1].Value;

                        // Comparação exata com a versão informada
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

                return null; // não encontrou
            });
        }


        /// <summary>
        /// Copia o zip do binário para a pasta temporária e extrai para a pasta de destino, sem travar a UI.
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



                progresso?.Report("[INFO] Limpando pasta de destino...");
                LimparPastaDestino(pastaDestino);
                LimparPastaDestino(pastaDestinoWes);
                progresso?.Report("[INFO] Pasta de destino limpa.");

                string caminhoTemporario = Path.Combine(_pastaTemporaria, binarioInfo.NomeOriginal + ".zip");

                progresso?.Report($"[INFO] Copiando {binarioInfo.Caminho} para {caminhoTemporario}...");
                using (var origem = new FileStream(binarioInfo.Caminho, FileMode.Open, FileAccess.Read))
                using (var destino = new FileStream(caminhoTemporario, FileMode.Create, FileAccess.Write))
                {
                    await origem.CopyToAsync(destino);
                }
                progresso?.Report("[INFO] Arquivo copiado com sucesso.");





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


        /// <summary>
        /// Limpa todos os arquivos e pastas dentro de um diretório.
        /// </summary>
        private void LimparPastaDestino(string caminho)
        {
            if (!Directory.Exists(caminho))
                return;

            // Remove todos os arquivos
            foreach (var arquivo in Directory.GetFiles(caminho))
            {
                File.SetAttributes(arquivo, FileAttributes.Normal); // garante que seja deletável
                File.Delete(arquivo);
            }

            // Remove todas as subpastas
            foreach (var dir in Directory.GetDirectories(caminho))
            {
                Directory.Delete(dir, true);
            }
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
