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

        public static List<SolucaoInformacoesDto> EncontrarArquivosRecursivo(
            string diretorioBase,
            string extensao,
            bool usarCache = true)
        {
            string cacheFile = Path.Combine(Path.GetTempPath(), $"cache_{Path.GetFileName(diretorioBase)}_{extensao}.json");

            // 1. Tenta carregar do cache
            if (usarCache && File.Exists(cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(cacheFile);
                    var cachedList = System.Text.Json.JsonSerializer.Deserialize<List<SolucaoInformacoesDto>>(json);
                    if (cachedList != null && cachedList.Any())
                        return cachedList;
                }
                catch { /* ignora se falhar */ }
            }

            // 2. Varrer diretório recursivamente
            var arquivos = new List<SolucaoInformacoesDto>();
            if (Directory.Exists(diretorioBase))
            {
                var todosArquivos = Directory.GetFiles(diretorioBase, $"*{extensao}", SearchOption.AllDirectories);
                foreach (var arq in todosArquivos)
                {
                    arquivos.Add(new SolucaoInformacoesDto
                    {
                        Nome = Path.GetFileName(arq),
                        Caminho = arq
                    });
                }
            }

            // 3. Salvar no cache
            if (usarCache)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(arquivos);
                    File.WriteAllText(cacheFile, json);
                }
                catch { /* ignora se falhar */ }
            }

            return arquivos;
        }

        public static List<SolucaoInformacoesDto> EncontrarProjetosDelphi(string caminhoDelphi, bool usarCache = true)
        {
            if (!Directory.Exists(caminhoDelphi))
                return new List<SolucaoInformacoesDto>();

            // Cache usando hash do caminho
            string hash = caminhoDelphi.GetHashCode().ToString();
            string cacheFile = Path.Combine(Path.GetTempPath(), $"cache_delphi_groupproj_{hash}.json");

            if (usarCache && File.Exists(cacheFile))
            {
                try
                {
                    var json = File.ReadAllText(cacheFile);
                    var cachedList = System.Text.Json.JsonSerializer.Deserialize<List<SolucaoInformacoesDto>>(json);
                    if (cachedList != null && cachedList.Any())
                        return cachedList;
                }
                catch { }
            }

            var arquivos = new List<SolucaoInformacoesDto>();

            // Procura diretamente no caminho base
            arquivos.AddRange(Directory.GetFiles(caminhoDelphi, "*.groupproj", SearchOption.TopDirectoryOnly)
                .Select(arq => new SolucaoInformacoesDto
                {
                    Nome = Path.GetFileName(arq),
                    Caminho = arq
                }));

            // Agora procura em subpastas de primeiro nível
            foreach (var subpasta in Directory.GetDirectories(caminhoDelphi))
            {
                try
                {
                    arquivos.AddRange(Directory.GetFiles(subpasta, "*.groupproj", SearchOption.TopDirectoryOnly)
                        .Select(arq => new SolucaoInformacoesDto
                        {
                            Nome = Path.GetFileName(arq),
                            Caminho = arq
                        }));
                }
                catch
                {
                    // Se não conseguir acessar a pasta, ignora
                }
            }

            // Salva cache
            if (usarCache)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(arquivos);
                    File.WriteAllText(cacheFile, json);
                }
                catch { }
            }

            return arquivos;
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