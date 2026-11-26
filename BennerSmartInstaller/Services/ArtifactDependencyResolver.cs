using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BennerSmartInstaller.Services
{
    public class ArtifactDependencyResolver
    {
        private readonly string _webAppPath;
        private readonly string _binDir;
        private readonly HashSet<string> _existingPages;
        private readonly HashSet<string> _existingViews;
        private readonly HashSet<string> _existingTasks;

        public ArtifactDependencyResolver(string webAppPath, string binDir)
        {
            _webAppPath = webAppPath;
            _binDir = binDir;

            _existingPages = LoadExistingArtifactIdentifiers(3); // 3 = Page
            _existingViews = LoadExistingArtifactIdentifiers(4); // 4 = View
            _existingTasks = LoadExistingArtifactIdentifiers(7); // 7 = Task
        }

        public List<string> Resolve(IEnumerable<string> initialFiles)
        {
            var resolvedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();

            foreach (var f in initialFiles)
            {
                string fullPath = NormalizePath(f);
                if (File.Exists(fullPath))
                {
                    queue.Enqueue(fullPath);
                }
            }

            int addedCount = 0;

            while (queue.Count > 0)
            {
                string currentFile = queue.Dequeue();
                if (!resolvedFiles.Add(currentFile))
                    continue;

                string fileName = Path.GetFileName(currentFile);
                string content;
                try
                {
                    content = File.ReadAllText(currentFile);
                }
                catch
                {
                    continue;
                }

                // 1. Se o arquivo for uma Tarefa (Task): buscar dependências de Páginas e Sub-tarefas
                if (currentFile.IndexOf(@"\Tasks\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Páginas referenciadas na Tarefa
                    var pageMatches = Regex.Matches(content, @"<Pages>[\s\S]*?</Pages>");
                    if (pageMatches.Count > 0)
                    {
                        var pages = Regex.Matches(pageMatches[0].Value, @"<string>([^<]+)</string>");
                        foreach (Match m in pages)
                        {
                            string pageId = m.Groups[1].Value.Trim();
                            if (!_existingPages.Contains(pageId))
                            {
                                foreach (string pageFile in FindArtifactFiles("Pages", pageId))
                                {
                                    if (resolvedFiles.Contains(pageFile)) continue;
                                    Console.WriteLine($"[AUTO-DEPENDÊNCIA] Tarefa '{fileName}' requer Página '{Path.GetFileName(pageFile)}' (adicionada à fila)");
                                    queue.Enqueue(pageFile);
                                    addedCount++;
                                }
                            }
                        }
                    }

                    // Sub-tarefas incluídas na Tarefa
                    var taskMatches = Regex.Matches(content, @"<Tasks>[\s\S]*?</Tasks>");
                    if (taskMatches.Count > 0)
                    {
                        var subTasks = Regex.Matches(taskMatches[0].Value, @"<string>([^<]+)</string>");
                        foreach (Match m in subTasks)
                        {
                            string taskId = m.Groups[1].Value.Trim();
                            if (!_existingTasks.Contains(taskId))
                            {
                                foreach (string subTaskFile in FindArtifactFiles("Tasks", taskId))
                                {
                                    if (resolvedFiles.Contains(subTaskFile)) continue;
                                    Console.WriteLine($"[AUTO-DEPENDÊNCIA] Tarefa '{fileName}' requer Sub-Tarefa '{Path.GetFileName(subTaskFile)}' (adicionada à fila)");
                                    queue.Enqueue(subTaskFile);
                                    addedCount++;
                                }
                            }
                        }
                    }
                }
                // 2. Se o arquivo for uma Página (Page): buscar dependências de Visões (Views)
                else if (currentFile.IndexOf(@"\Pages\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var viewMatches = Regex.Matches(content, @"<EntityViewName>([^<]+)</EntityViewName>");
                    foreach (Match m in viewMatches)
                    {
                        string viewId = m.Groups[1].Value.Trim();
                        if (!_existingViews.Contains(viewId))
                        {
                            foreach (string viewFile in FindArtifactFiles("Views", viewId))
                            {
                                if (resolvedFiles.Contains(viewFile)) continue;
                                Console.WriteLine($"[AUTO-DEPENDÊNCIA] Página '{fileName}' requer Visão '{Path.GetFileName(viewFile)}' (adicionada à fila)");
                                queue.Enqueue(viewFile);
                                addedCount++;
                            }
                        }
                    }
                }
            }

            if (addedCount > 0)
            {
                Console.WriteLine($"\n[RESOLUÇÃO DE DEPENDÊNCIAS] {addedCount} artefato(s) dependente(s) resolvido(s) e incluído(s) na fila!");
            }

            // Ordena os arquivos pela ordem natural hierárquica (Views -> Pages -> Tasks -> ...)
            return resolvedFiles.OrderBy(f => GetArtifactInstallOrder(f)).ToList();
        }

        private string NormalizePath(string filePath)
        {
            filePath = filePath.Trim();
            if (File.Exists(filePath)) return Path.GetFullPath(filePath);

            string combined = Path.Combine(_webAppPath, filePath);
            if (File.Exists(combined)) return Path.GetFullPath(combined);

            return filePath;
        }

        /// <summary>
        /// Todos os arquivos locais do artefato (uma por camada), exceto camadas 50 (cliente) e 60 (usuário),
        /// que a plataforma não instala. O padrão "{id}.*.xml" também casa ids que apenas começam com "{id}.",
        /// então o nome base é conferido.
        /// </summary>
        private IEnumerable<string> FindArtifactFiles(string folder, string artifactId)
        {
            string dir = Path.Combine(_webAppPath, "Artifacts", folder);
            if (!Directory.Exists(dir)) return new string[0];

            try
            {
                return Directory.GetFiles(dir, $"{artifactId}.*.xml", SearchOption.TopDirectoryOnly)
                    .Where(f =>
                    {
                        var parts = Path.GetFileName(f).Split('.');
                        int layer;
                        if (parts.Length < 3 || !int.TryParse(parts[parts.Length - 2], out layer)) return false;
                        if (layer == 50 || layer == 60) return false;
                        return string.Equals(string.Join(".", parts.Take(parts.Length - 2)), artifactId, StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new string[0];
            }
        }

        private int GetArtifactInstallOrder(string path)
        {
            if (path.IndexOf(@"\DataSources\", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            if (path.IndexOf(@"\Views\", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (path.IndexOf(@"\Scripts\", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
            if (path.IndexOf(@"\Filters\", StringComparison.OrdinalIgnoreCase) >= 0) return 4;
            if (path.IndexOf(@"\Widgets\", StringComparison.OrdinalIgnoreCase) >= 0) return 5;
            if (path.IndexOf(@"\Templates\", StringComparison.OrdinalIgnoreCase) >= 0) return 6;
            if (path.IndexOf(@"\Pages\", StringComparison.OrdinalIgnoreCase) >= 0) return 7;
            if (path.IndexOf(@"\Menus\", StringComparison.OrdinalIgnoreCase) >= 0) return 8;
            if (path.IndexOf(@"\DynamicQueryTypes\", StringComparison.OrdinalIgnoreCase) >= 0) return 9;
            if (path.IndexOf(@"\DynamicQueries\", StringComparison.OrdinalIgnoreCase) >= 0) return 10;
            if (path.IndexOf(@"\Tasks\", StringComparison.OrdinalIgnoreCase) >= 0) return 11;
            if (path.IndexOf(@"\Roles\", StringComparison.OrdinalIgnoreCase) >= 0) return 12;
            return 99;
        }

        private HashSet<string> LoadExistingArtifactIdentifiers(int tipo)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Antes procurava Benner.Tecnologia.Common.dll (o assembly real é Benner.Tecnologia.Common2 e não
                // fica em WebApp\Bin), então o conjunto vinha sempre vazio e TODA dependência local era
                // adicionada à fila, mesmo já existindo no banco. IndexDbReader usa os assemblies já carregados.
                foreach (string rawId in new IndexDbReader(_binDir).Read(tipo).Keys)
                {
                    set.Add(rawId);
                    // Remove extensões de camada (ex: .20.xml)
                    string[] parts = rawId.Split('.');
                    if (parts.Length > 2)
                    {
                        set.Add(string.Join(".", parts.Take(parts.Length - 2)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO DEPENDENCY LOADER] Falha ao consultar W_INDICEARTEFATOS (TIPO={tipo}): {ex.Message}");
            }

            return set;
        }
    }
}

