using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public static class Folders
    {
        public static List<PastaInformacoesDto> ListarPastas(string caminho, List<string>? ignorar = null)
        {
            if (!Directory.Exists(caminho))
                return new List<PastaInformacoesDto>();

            var todasPastas = Directory.GetDirectories(caminho)
                                       .Where(dir => !EhOculta(dir)) // remove ocultas
                                       .Select(dir => new PastaInformacoesDto
                                       {
                                           Nome = Path.GetFileName(dir) ?? "",
                                           Caminho = dir
                                       })
                                       .Where(pasta => !string.IsNullOrEmpty(pasta.Nome));

            if (ignorar != null && ignorar.Any())
                todasPastas = todasPastas.Where(pasta => !ignorar.Contains(pasta.Nome));

            return todasPastas.ToList();
        }

        static bool EhOculta(string caminho)
        {
            var atributos = File.GetAttributes(caminho);
            return (atributos & FileAttributes.Hidden) == FileAttributes.Hidden;
        }




        public static List<SolucaoInformacoesDto> ListarArquivosSln(string caminhoBase)
        {
            if (!Directory.Exists(caminhoBase))
                return new List<SolucaoInformacoesDto>();

            string caminhoDotnet = Path.Combine(caminhoBase, "dotnet");
            if (!Directory.Exists(caminhoDotnet))
                return new List<SolucaoInformacoesDto>();

             var candidatos = new List<string>
            {
                Path.Combine(caminhoDotnet, "Solutions"),
                Path.Combine(caminhoDotnet, "Solution"),
                caminhoDotnet 
            };

             string caminhoFinal = candidatos.FirstOrDefault(Directory.Exists);
            if (caminhoFinal == null)
                return new List<SolucaoInformacoesDto>();

            return Directory.GetFiles(caminhoFinal, "*.sln", SearchOption.TopDirectoryOnly)
                            .Select(arq => new SolucaoInformacoesDto
                            {
                                Nome = Path.GetFileName(arq),
                                Caminho = arq
                            })
                            .ToList();
        }
    }
}