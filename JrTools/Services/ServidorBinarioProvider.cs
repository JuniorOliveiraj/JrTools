using JrTools.Dto;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class ServidorBinarioProvider : IBinarioSourceProvider
    {
        private readonly string _binPath;

        public ServidorBinarioProvider(string caminhoServidor)
        {
            if (string.IsNullOrWhiteSpace(caminhoServidor))
                throw new ArgumentException("Caminho do servidor de binários não configurado.", nameof(caminhoServidor));

            _binPath = caminhoServidor;
        }

        public async Task<BinarioInfoDataObject?> ObterBinarioAsync(string branchLimpo, IProgress<string>? progresso = null)
        {
            var versao = branchLimpo.Replace("/", "-");

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
    }
}
