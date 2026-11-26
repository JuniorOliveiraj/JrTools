using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BennerSmartInstaller.Artifacts;
using BennerSmartInstaller.Bootstrapping;

namespace BennerSmartInstaller.Services
{
    public class ArtifactInstallerService
    {
        private readonly string _webAppPath;
        private readonly string _binDir;

        public ArtifactInstallerService(string webAppPath, string binDir)
        {
            _webAppPath = webAppPath;
            _binDir = binDir;
        }

        public void Execute(string filesArg, bool dryRun = false)
        {
            var sw = Stopwatch.StartNew();
            var profiler = new StepProfiler("INSTALL");

            string metadataDll = Path.Combine(_binDir, "Benner.Tecnologia.Metadata.dll");
            string webAppComponentsDll = Path.Combine(_binDir, "Benner.Tecnologia.Wes.Components.WebApp.dll");

            if (!File.Exists(metadataDll) || !File.Exists(webAppComponentsDll))
            {
                throw new FileNotFoundException($"DLLs de Metadata/Components ausentes em: {_binDir}");
            }

            var metadataAsm = Assembly.LoadFrom(metadataDll);
            var webAppComponentsAsm = Assembly.LoadFrom(webAppComponentsDll);

            // 1. ArtifactInstallOrderer
            Type ordererType = metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactInstallOrderer")
                ?? webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactInstallOrderer");

            if (ordererType == null)
            {
                throw new InvalidOperationException("ArtifactInstallOrderer não foi localizado.");
            }

            var getDefaultOrderMethod = ordererType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetDefaultOrder" && m.GetParameters().Length == 0);

            if (getDefaultOrderMethod == null)
            {
                throw new InvalidOperationException("ArtifactInstallOrderer.GetDefaultOrder não foi localizado.");
            }
            object defaultOrder = getDefaultOrderMethod.Invoke(null, null);

            // 2. ArtifactToInstall & ArtifactType
            Type artifactTypeEnum = metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactType")
                ?? webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactType");

            Type artifactToInstallType = webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactToInstall")
                ?? metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactToInstall");

            if (artifactToInstallType == null)
            {
                throw new InvalidOperationException("Classe ArtifactToInstall não foi localizada.");
            }

            var listType = typeof(List<>).MakeGenericType(artifactToInstallType);
            var fullArtifactsList = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");

            string[] rawFiles;
            if (File.Exists(filesArg) && (filesArg.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || filesArg.EndsWith(".lst", StringComparison.OrdinalIgnoreCase)))
            {
                string fileContent = File.ReadAllText(filesArg);
                rawFiles = fileContent.Split(new[] { ';', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                rawFiles = filesArg.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            }

            // Resolução automática e inteligente de dependências antes da instalação
            var resolver = profiler.Measure("DependencyResolver: carga W_INDICEARTEFATOS (Page/View/Task)",
                () => new ArtifactDependencyResolver(_webAppPath, _binDir));
            var relativeFiles = profiler.Measure("DependencyResolver: Resolve (leitura XML + regex)",
                () => resolver.Resolve(rawFiles));

            var loadedArtifacts = new List<(string fileName, string fullPath, string typeName, object artifactObj)>();
            var swLoad = Stopwatch.StartNew();

            foreach (var relFile in relativeFiles)
            {
                string fullPath = relFile.Trim();
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine(_webAppPath, relFile.Trim());
                }

                if (File.Exists(fullPath))
                {
                    try
                    {
                        object artifactObj = ArtifactModelFactory.CreateArtifactToInstall(artifactToInstallType, artifactTypeEnum, fullPath);

                        if (artifactObj != null)
                        {
                            string folderName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? "";
                            object typeVal = ArtifactModelFactory.MapArtifactType(folderName, artifactTypeEnum);
                            string typeName = typeVal?.ToString() ?? "Desconhecido";

                            loadedArtifacts.Add((Path.GetFileName(fullPath), fullPath, typeName, artifactObj));
                            addMethod.Invoke(fullArtifactsList, new[] { artifactObj });
                        }
                    }
                    catch (Exception ex)
                    {
                        var realEx = ex;
                        while (realEx is TargetInvocationException && realEx.InnerException != null)
                        {
                            realEx = realEx.InnerException;
                        }
                        Console.WriteLine($"[AVISO] Erro ao carregar artefato '{Path.GetFileName(fullPath)}': {realEx.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[AVISO] Arquivo de artefato não encontrado: {relFile}");
                }
            }

            profiler.Add($"Carga dos artefatos (ModelFactory) x{loadedArtifacts.Count}", swLoad.Elapsed);

            int count = loadedArtifacts.Count;
            if (count == 0)
            {
                Console.WriteLine("[AVISO] Nenhum artefato válido encontrado para instalar.");
                return;
            }

            // Log estruturado do que está sendo instalado
            Console.WriteLine($"\n[SMART INSTALL] Preparando instalação de {count} artefato(s):");
            var groups = loadedArtifacts.GroupBy(a => a.typeName).OrderBy(g => g.Key);
            foreach (var g in groups)
            {
                Console.WriteLine($"  • [{g.Key}] {g.Count()} arquivo(s)");
            }

            Console.WriteLine("\n[SMART INSTALL] Lista de artefatos na fila:");
            for (int i = 0; i < loadedArtifacts.Count; i++)
            {
                var item = loadedArtifacts[i];
                int faltam = count - (i + 1);
                Console.WriteLine($"  {i + 1}/{count}. [{item.typeName}] {item.fileName} (Faltam {faltam} artefatos)");
            }

            var fastItems = loadedArtifacts
                .Select(a => new KeyValuePair<string, object>(a.fullPath, a.artifactObj.GetType().GetProperty("Type").GetValue(a.artifactObj, null)))
                .ToList();
            bool tryFast = Environment.GetEnvironmentVariable("SMARTINSTALL_PLATFORM_INSTALL") != "1";

            if (dryRun)
            {
                if (tryFast)
                {
                    try
                    {
                        var planner = new FastInstaller(_webAppPath, _binDir, metadataAsm, webAppComponentsAsm, profiler);
                        var plan = planner.Run(fastItems, true, true);
                        Console.WriteLine($"\n[DRY-RUN] Importaria {plan.Imported} artefato(s) + {plan.Restored} reindexação(ões) de camada superior; {plan.AlreadyUpToDate} já atualizado(s). Nada foi gravado.");
                        profiler.PrintSummary();
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AVISO SMART INSTALL] Plano rápido indisponível: {Unwrap(ex).Message}");
                    }
                }
                Console.WriteLine($"\n[DRY-RUN] {count} artefato(s) seriam enviados à plataforma. Nada foi gravado.");
                profiler.PrintSummary();
                return;
            }

            // 3. InstallArtifactsManager
            Type managerType = webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "InstallArtifactsManager")
                ?? metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "InstallArtifactsManager");

            if (managerType == null)
            {
                throw new InvalidOperationException("InstallArtifactsManager não localizado.");
            }

            Type layerType = metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactLayer") ?? typeof(int);
            object layerAllObj = Enum.ToObject(layerType, 999);

            var startInstalationMethod = managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name.Equals("StartInstalation", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == 6)
                ?? managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name.Equals("StartInstalation", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == 5);

            if (startInstalationMethod == null)
            {
                throw new InvalidOperationException("Método InstallArtifactsManager.StartInstalation não localizado.");
            }

            var parameters = startInstalationMethod.GetParameters();

            var pendingArtifacts = new List<(string fileName, string fullPath, string typeName, object artifactObj)>(loadedArtifacts);
            var failedArtifacts = new List<(string fileName, string reason)>();
            bool finished = false;
            int attempt = 0;
            FastInstallResult fastResult = null;

            if (tryFast)
            {
                FastInstaller fast = null;
                try
                {
                    fast = new FastInstaller(_webAppPath, _binDir, metadataAsm, webAppComponentsAsm, profiler);
                }
                catch (Exception ex)
                {
                    // Falha de binding por reflexão acontece antes de qualquer gravação: pode cair no caminho da plataforma.
                    Console.WriteLine($"[AVISO SMART INSTALL] Instalação rápida indisponível ({Unwrap(ex).Message}); usando o caminho da plataforma.");
                }

                if (fast != null)
                {
                    Console.WriteLine("\n[SMART INSTALL] [Fase 1/3] Diff por hash contra o índice do banco (instalação incremental)...");
                    Console.WriteLine("[SMART INSTALL] [Fase 2/3] Importando somente o que mudou e gerando páginas se necessário...");
                    try
                    {
                        fastResult = fast.Run(fastItems, true, false);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Falha na instalação incremental: " + Unwrap(ex).Message, Unwrap(ex));
                    }
                    foreach (var f in fastResult.Failed) failedArtifacts.Add((f.Key, f.Value));
                    pendingArtifacts.Clear();
                    finished = true;
                }
            }

            while (!finished && pendingArtifacts.Count > 0)
            {
                attempt++;
                var currentBatchList = Activator.CreateInstance(listType);
                foreach (var a in pendingArtifacts)
                {
                    addMethod.Invoke(currentBatchList, new[] { a.artifactObj });
                }

                object[] currentParams = new object[parameters.Length];
                if (parameters.Length == 6)
                {
                    currentParams[0] = defaultOrder;
                    currentParams[1] = currentBatchList;
                    currentParams[2] = layerAllObj;
                    currentParams[3] = false; // fullInstall = false
                    currentParams[4] = _webAppPath;
                    currentParams[5] = true;  // installCustomerArtifacts = true
                }
                else if (parameters.Length == 5)
                {
                    currentParams[0] = defaultOrder;
                    currentParams[1] = currentBatchList;
                    currentParams[2] = false;
                    currentParams[3] = _webAppPath;
                    currentParams[4] = true;
                }

                try
                {
                    Console.WriteLine($"\n[SMART INSTALL] [Fase 1/3] Disparando sincronização atômica para {pendingArtifacts.Count} artefato(s)...");
                    Console.WriteLine("[SMART INSTALL] [Fase 2/3] Processando sincronização no banco de dados e gerando páginas...");
                    if (StepProfiler.DeepMode && parameters.Length == 6)
                    {
                        InvokeInstallationDeep(profiler, attempt, managerType, metadataAsm, webAppComponentsAsm, currentParams);
                    }
                    else
                    {
                        profiler.Measure($"StartInstalation completo (tentativa {attempt}, {pendingArtifacts.Count} artefatos)",
                            () => startInstalationMethod.Invoke(null, currentParams));
                    }
                    finished = true;
                }
                catch (Exception batchEx)
                {
                    var realBatchEx = batchEx;
                    while (realBatchEx is TargetInvocationException && realBatchEx.InnerException != null)
                    {
                        realBatchEx = realBatchEx.InnerException;
                    }

                    string errorMsg = realBatchEx.Message;
                    Console.WriteLine($"\n[AVISO SMART INSTALL] Falha na sincronização do lote: {errorMsg}");

                    // Identifica o(s) artefato(s) que falharam pelo nome/identificador
                    var itemsToRemove = pendingArtifacts.Where(item =>
                    {
                        string baseName = Path.GetFileNameWithoutExtension(item.fileName);
                        if (baseName.Contains("."))
                            baseName = baseName.Substring(0, baseName.LastIndexOf('.'));

                        // A plataforma cita o artefato entre aspas simples ("Erro ao importar Tarefa 'CONSUMIRIA'").
                        // Casar por substring solta descartava artefatos sadios (ex.: 'RELATORIOS' dentro de
                        // 'MODULO_DO_DOSSIE_RELATORIOS').
                        return errorMsg.IndexOf("'" + baseName + "'", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               errorMsg.IndexOf(item.fileName, StringComparison.OrdinalIgnoreCase) >= 0;
                    }).ToList();

                    // Se a mensagem não identificou pelo nome, remove o primeiro item pendente para evitar loop infinito
                    if (itemsToRemove.Count == 0 && pendingArtifacts.Count > 0)
                    {
                        itemsToRemove.Add(pendingArtifacts[0]);
                    }

                    foreach (var badItem in itemsToRemove)
                    {
                        Console.WriteLine($"[DESCARTE] Removendo artefato com falha da fila: [{badItem.typeName}] {badItem.fileName}");
                        failedArtifacts.Add((badItem.fileName, errorMsg));
                        pendingArtifacts.Remove(badItem);
                    }

                    if (pendingArtifacts.Count > 0)
                    {
                        Console.WriteLine($"[RETRY] Reiniciando instalação em lote com os {pendingArtifacts.Count} artefato(s) restante(s)...");
                    }
                }
            }

            sw.Stop();
            int installedCount = loadedArtifacts.Count - failedArtifacts.Count;
            Console.WriteLine($"\n[SMART INSTALL] [Fase 3/3] Processamento finalizado em {sw.Elapsed.TotalSeconds:F2}s!");
            if (fastResult != null)
            {
                Console.WriteLine($"  • Importados: {fastResult.Imported}  |  Reindexados (camada superior): {fastResult.Restored}  |  Já atualizados (ignorados): {fastResult.AlreadyUpToDate}");
                installedCount = fastResult.Imported + fastResult.Restored + fastResult.AlreadyUpToDate;
            }
            Console.WriteLine($"  • Instalados com êxito: {installedCount}");
            if (failedArtifacts.Count > 0)
            {
                Console.WriteLine($"  • Falhas descartadas: {failedArtifacts.Count}");
                foreach (var f in failedArtifacts)
                {
                    Console.WriteLine($"     - {f.fileName}");
                }
            }

            profiler.PrintSummary();

            // Otimiza o consumo de memória imediatamente
            MemoryOptimizer.TrimMemory();

            // Falha de artefato não pode terminar como "SUCESSO"/exit 0 (a UI e o daemon usam o código de saída).
            if (failedArtifacts.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{failedArtifacts.Count} de {loadedArtifacts.Count} artefato(s) falharam: " +
                    string.Join("; ", failedArtifacts.Select(f => f.fileName)));
            }
        }

        private static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }

        /// <summary>
        /// Somente diagnóstico (SMARTINSTALL_PROFILE=1): reproduz InstallArtifactsManager.StartInstalation
        /// chamando por reflexão as etapas privadas na mesma ordem, para medir cada uma separadamente.
        /// Não é usado no fluxo normal.
        /// </summary>
        private static void InvokeInstallationDeep(StepProfiler profiler, int attempt, Type managerType,
            Assembly metadataAsm, Assembly webAppComponentsAsm, object[] p)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            object types = p[0], artifacts = p[1], layer = p[2], fullInstall = p[3], appPath = p[4], customer = p[5];
            string tag = $"(tentativa {attempt})";

            MethodInfo Find(string name, int args) =>
                managerType.GetMethods(flags).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args);

            var install = Find("InstallArtifacts", 5);
            var delete = Find("DeleteArtifacts", 1);
            var generate = Find("GeneratePages", 1);
            if (install == null || delete == null || generate == null)
            {
                Console.WriteLine("[PROFILE] Etapas internas não localizadas; usando StartInstalation completo.");
                managerType.GetMethods(flags)
                    .First(m => m.Name.Equals("StartInstalation", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == 6)
                    .Invoke(null, p);
                return;
            }

            profiler.Measure($"[plataforma] InstallArtifacts: indexer + ArtifactVersion + importação {tag}",
                () => install.Invoke(null, new[] { types, artifacts, layer, fullInstall, customer }));
            profiler.Measure($"[plataforma] DeleteArtifacts {tag}", () => delete.Invoke(null, new[] { types }));

            bool hasPage = ((IEnumerable)types).Cast<object>().Any(t => t.ToString() == "Page");
            var ignoreProp = managerType.GetProperty("IgnorePageGeneration", flags);
            bool ignorePages = ignoreProp != null && (bool)ignoreProp.GetValue(null, null);
            if (hasPage && !ignorePages)
                profiler.Measure($"[plataforma] GeneratePages {tag}", () => generate.Invoke(null, new[] { appPath }));
            else
                Console.WriteLine($"[PROFILE] GeneratePages não executado {tag} (hasPage={hasPage}, ignore={ignorePages})");

            var cacheHelper = metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "CacheArtifactHelper");
            var clearCache = cacheHelper?.GetMethods(flags).FirstOrDefault(m => m.Name == "ClearCache" && m.GetParameters().Length == 1);
            if (clearCache != null)
                profiler.Measure($"[plataforma] ClearCache por tipo {tag}", () =>
                {
                    foreach (var t in (IEnumerable)types) clearCache.Invoke(null, new[] { t });
                });

            var custom = webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "CustomInstallArtifacts");
            var after = custom?.GetMethods(flags).FirstOrDefault(m => m.Name == "AfterExecuteInstallArtifacts" && m.GetParameters().Length == 0);
            if (after != null)
                profiler.Measure($"[plataforma] AfterExecuteInstallArtifacts {tag}", () => after.Invoke(null, null));
        }
    }
}
