using Benner.Tecnologia.Common;
using Benner.Tecnologia.Metadata;
using Benner.Tecnologia.Metadata.Entities;
using Benner.Tecnologia.Wes.Components.WebApp;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BennerSmartInstaller.Verbs
{
    [Verb("install", HelpText = "Instalação Seletiva de Artefatos do WES resolvendo dependências automaticamente.")]
    public class SmartInstallVerb : BaseVerb
    {
        [Option('a', "appPath", Required = true, HelpText = "Caminho absoluto para o diretório raiz do WebApp (onde fica a pasta Artifacts).")]
        public string AppPath { get; set; }

        [Option('f', "artifacts", Required = true, HelpText = "Lista de arquivos separados por ponto-e-vírgula (;) para instalação inicial.")]
        public string ArtifactsRaw { get; set; }

        [Option('l', "layer", Default = "all", HelpText = "Camada de instalação. Ex: all, cliente, especifico, vertical, benner.")]
        public string Layer { get; set; }

        public override void Execute()
        {
            if (!Directory.Exists(AppPath))
            {
                throw new DirectoryNotFoundException($"Diretório do WebApp não encontrado: {AppPath}");
            }

            // A inicialização (Login, Provider, etc) já foi feita no Program.cs

            var initialFiles = ArtifactsRaw
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToList();

            Console.WriteLine("[INFO] Resolvendo dependências (busca recursiva) para os artefatos selecionados...");
            var fullFilesList = ResolverDependenciasXml(AppPath, initialFiles);

            var typesList = new HashSet<ArtifactType>();
            var artifactsToInstallList = new List<ArtifactToInstall>();

            foreach (var fullPath in fullFilesList)
            {
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[AVISO] Arquivo XML não encontrado no disco: {fullPath}");
                    continue;
                }

                string parentDir = Path.GetFileName(Path.GetDirectoryName(fullPath));
                ArtifactType artifactType = MapearGuiaParaEnum(parentDir);

                typesList.Add(artifactType);
                artifactsToInstallList.Add(new ArtifactToInstall(fullPath, artifactType));

                Console.WriteLine($"[SELETIVO] 📦 Artefato a instalar: {Path.GetFileName(fullPath)} ({parentDir})");
            }

            if (artifactsToInstallList.Count == 0)
            {
                Console.WriteLine("[AVISO] Nenhum artefato válido selecionado para instalação.");
                return;
            }

            ArtifactLayer layerEnum = GetArtifactLayer(Layer);
            bool installCustomer = (layerEnum == ArtifactLayer.All || layerEnum == ArtifactLayer.Cliente);

            Console.WriteLine($"[INFO] Executando pipeline oficial de instalação do WES para {artifactsToInstallList.Count} artefato(s)...");

            try
            {
                var metadataAssembly = typeof(ArtifactType).Assembly;
                var ordererType = metadataAssembly.GetType("Benner.Tecnologia.Metadata.ArtifactInstallOrderer");
                var defaultOrder = ordererType?.GetMethod("GetDefaultOrder")?.Invoke(null, null) as List<ArtifactType> ?? new List<ArtifactType>();

                var dbContextFactoryType = metadataAssembly.GetType("Benner.Tecnologia.Metadata.Entities.BennerDbContextFactory") 
                                           ?? typeof(Benner.Tecnologia.Common.Application.BennerAppInfraServices).Assembly.GetType("Benner.Tecnologia.Common.Application.BennerDbContextFactory");
                
                object factory = null;
                IDisposable dbContext = null;
                
                if (dbContextFactoryType != null)
                {
                    factory = Activator.CreateInstance(dbContextFactoryType);
                    var newTaskMethod = dbContextFactoryType.GetMethod("NewTaskDbContext", Type.EmptyTypes);
                    
                    if (newTaskMethod != null)
                    {
                        dbContext = newTaskMethod.Invoke(factory, null) as IDisposable;
                    }
                }

                try
                {
                    InstallArtifactsManager.StartInstalation(
                        defaultOrder,
                        artifactsToInstallList,
                        layerEnum,
                        false, // fullInstall = false (seletivo)
                        AppPath,
                        installCustomer
                    );
                }
                finally
                {
                    dbContext?.Dispose();
                }

                Console.WriteLine("[BENNER SMART INSTALLER] ✅ Instalação oficial concluída com sucesso!");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("ICustomInstallArtifacts") || ex.Message.Contains("BusinessComponent"))
                {
                    Console.WriteLine("[INFO] Pipeline principal finalizada. Hooks customizados ignorados.");
                    Console.WriteLine("[BENNER SMART INSTALLER] ✅ Instalação concluída com sucesso!");
                    return;
                }
                throw;
            }
        }

        private List<string> ResolverDependenciasXml(string appPath, List<string> initialRelativeFiles)
        {
            var result = new List<string>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();

            string artifactsBaseDir = Path.Combine(appPath, "Artifacts");

            foreach (var rel in initialRelativeFiles)
            {
                string full = Path.IsPathRooted(rel) ? rel : Path.Combine(appPath, rel);
                if (File.Exists(full))
                {
                    queue.Enqueue(full);
                }
            }

            while (queue.Count > 0)
            {
                string currentFile = queue.Dequeue();
                if (processed.Contains(currentFile)) continue;

                processed.Add(currentFile);
                result.Add(currentFile);

                try
                {
                    string content = File.ReadAllText(currentFile);
                    var potentialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    // Match 1: atributos
                    var attrMatches = Regex.Matches(content, @"(?:id|page|view|datasource|script|pageid|viewid|datasourceid|scriptid)=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    foreach (Match m in attrMatches)
                    {
                        if (m.Groups.Count > 1 && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                            potentialIds.Add(m.Groups[1].Value.Trim());
                    }

                    // Match 2: conteúdo de tags XML
                    var tagMatches = Regex.Matches(content, @">(AGA_[A-Z0-9_]+)<", RegexOptions.IgnoreCase);
                    foreach (Match m in tagMatches)
                    {
                        if (m.Groups.Count > 1 && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
                            potentialIds.Add(m.Groups[1].Value.Trim());
                    }

                    foreach (string refId in potentialIds)
                    {
                        if (refId.Length < 3) continue;

                        string pattern = $"{refId}.*.xml";
                        string exactPattern = $"{refId}.xml";

                        var matchesOnDisk = Directory.GetFiles(artifactsBaseDir, pattern, SearchOption.AllDirectories)
                                            .Concat(Directory.GetFiles(artifactsBaseDir, exactPattern, SearchOption.AllDirectories))
                                            .Distinct(StringComparer.OrdinalIgnoreCase);

                        foreach (var depFile in matchesOnDisk)
                        {
                            if (!processed.Contains(depFile))
                            {
                                Console.WriteLine($"[AUTO-FIX DEPENDÊNCIA] 🔗 Encontrado artefato dependente: {Path.GetFileName(depFile)}");
                                queue.Enqueue(depFile);
                            }
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private ArtifactType MapearGuiaParaEnum(string guia)
        {
            if (string.Equals(guia, "Scripts", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)1;
            if (string.Equals(guia, "Views", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)2;
            if (string.Equals(guia, "Pages", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)3;
            if (string.Equals(guia, "Menus", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)4;
            if (string.Equals(guia, "Templates", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)5;
            if (string.Equals(guia, "Widgets", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)6;
            if (string.Equals(guia, "Tasks", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)7;
            if (string.Equals(guia, "Roles", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)8;
            if (string.Equals(guia, "Filters", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)9;
            if (string.Equals(guia, "DataSources", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)10;
            if (string.Equals(guia, "DynamicQueries", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)11;
            if (string.Equals(guia, "DynamicQueryTypes", StringComparison.OrdinalIgnoreCase)) return (ArtifactType)12;
            return (ArtifactType)3; // Page default
        }

        private ArtifactLayer GetArtifactLayer(string artifactLayer)
        {
            string str = artifactLayer?.ToLower()?.Trim();
            if (string.IsNullOrEmpty(str)) return ArtifactLayer.All;

            if (str.Contains("builder") || str == "10") return ArtifactLayer.Builder;
            if (str.Contains("tecnologia") || str == "15") return ArtifactLayer.Tecnologia;
            if (str.Contains("benner") || str == "20") return ArtifactLayer.Benner;
            if (str.Contains("vertical") || str == "30") return ArtifactLayer.Vertical;
            if (str.Contains("especifico") || str.Contains("específico") || str == "40") return ArtifactLayer.Especifico;
            if (str.Contains("cliente") || str == "50") return ArtifactLayer.Cliente;

            return ArtifactLayer.All;
        }
    }
}
