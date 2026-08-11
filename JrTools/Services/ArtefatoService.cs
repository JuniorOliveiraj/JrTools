using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JrTools.Services
{
    public class ArtefatoService
    {
        public List<ArtefatoDto> CarregarArtefatos(string webAppPath)
        {
            var resultado = new List<ArtefatoDto>();

            if (string.IsNullOrWhiteSpace(webAppPath))
                return resultado;

            string artifactsDir = Path.Combine(webAppPath, "Artifacts");
            if (!Directory.Exists(artifactsDir))
                return resultado;

            var subdirs = Directory.GetDirectories(artifactsDir);
            foreach (var subDir in subdirs)
            {
                string guiaNome = Path.GetFileName(subDir); // ex: Menus, Pages, Views
                var arquivos = Directory.GetFiles(subDir, "*.xml", SearchOption.TopDirectoryOnly);

                foreach (var file in arquivos)
                {
                    string fileName = Path.GetFileName(file); // ex: AGA_ADM.20.xml
                    
                    // Extrai o identificador e a camada
                    // Ex: AGA_ADM.20.xml => Identificador: AGA_ADM, Camada: 20
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName); // AGA_ADM.20
                    int lastDot = nameWithoutExt.LastIndexOf('.');
                    
                    string id = nameWithoutExt;
                    string camada = "20"; // Padrão

                    if (lastDot > 0)
                    {
                        id = nameWithoutExt.Substring(0, lastDot);
                        camada = nameWithoutExt.Substring(lastDot + 1);
                    }

                    resultado.Add(new ArtefatoDto
                    {
                        Identificador = id,
                        NomeArquivo = fileName,
                        Guia = guiaNome,
                        Camada = camada,
                        CaminhoCompleto = file
                    });
                }
            }

            return resultado.OrderBy(a => a.Guia).ThenBy(a => a.Identificador).ToList();
        }

        public List<ArtefatoDto> ResolverDependencias(ArtefatoDto principal, List<ArtefatoDto> todosArtefatos)
        {
            var ordenados = new List<ArtefatoDto>();
            var visitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            ResolverRecursivo(principal, todosArtefatos, ordenados, visitados);

            return ordenados;
        }

        private void ResolverRecursivo(ArtefatoDto atual, List<ArtefatoDto> todosArtefatos, List<ArtefatoDto> ordenados, HashSet<string> visitados)
        {
            if (atual == null || visitados.Contains(atual.CaminhoCompleto))
                return;

            visitados.Add(atual.CaminhoCompleto);

            // Tenta ler o XML para buscar dependências filhas (Páginas, Visões, etc.)
            try
            {
                if (File.Exists(atual.CaminhoCompleto))
                {
                    string conteudoXml = File.ReadAllText(atual.CaminhoCompleto);
                    
                    // Busca por identificadores de Páginas ou Visões citados dentro do XML (ex: AGA_E_..., AGA_V_..., AGA_W_...)
                    var matches = Regex.Matches(conteudoXml, @"\b(AGA_[A-Za-z0-9_]+)\b", RegexOptions.IgnoreCase);
                    
                    foreach (Match match in matches)
                    {
                        string idReferenciado = match.Value.Trim();
                        if (idReferenciado.Equals(atual.Identificador, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Procura se o idReferenciado existe na lista total de artefatos
                        var depArtefato = todosArtefatos.FirstOrDefault(a => 
                            a.Identificador.Equals(idReferenciado, StringComparison.OrdinalIgnoreCase));

                        if (depArtefato != null && !visitados.Contains(depArtefato.CaminhoCompleto))
                        {
                            ResolverRecursivo(depArtefato, todosArtefatos, ordenados, visitados);
                        }
                    }
                }
            }
            catch { }

            ordenados.Add(atual);
        }
    }
}
