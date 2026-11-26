using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;
using BennerSmartInstaller.Bootstrapping;

namespace BennerSmartInstaller.Services
{
    public class ArtifactComparatorService
    {
        private readonly string _webAppPath;
        private readonly string _binDir;

        public ArtifactComparatorService(string webAppPath, string binDir)
        {
            _webAppPath = webAppPath;
            _binDir = binDir;
        }

        public void Execute()
        {
            string metadataDll = Path.Combine(_binDir, "Benner.Tecnologia.Metadata.dll");
            string webAppComponentsDll = Path.Combine(_binDir, "Benner.Tecnologia.Wes.Components.WebApp.dll");

            if (!File.Exists(metadataDll) || !File.Exists(webAppComponentsDll))
            {
                throw new FileNotFoundException($"DLLs de Metadata/Components ausentes em: {_binDir}");
            }

            var metadataAsm = Assembly.LoadFrom(metadataDll);
            var webAppComponentsAsm = Assembly.LoadFrom(webAppComponentsDll);

            Type artifactTypeEnum = metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactType")
                ?? webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "ArtifactType");

            Type factoryType = metadataAsm.GetTypes().FirstOrDefault(t => t.Name == "WebArtifactSyncronizerFactory")
                ?? webAppComponentsAsm.GetTypes().FirstOrDefault(t => t.Name == "WebArtifactSyncronizerFactory");

            if (factoryType == null || artifactTypeEnum == null)
            {
                throw new InvalidOperationException("WebArtifactSyncronizerFactory ou ArtifactType não foi localizado.");
            }

            var createSyncMethod = factoryType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Create" && m.GetParameters().Length == 1);

            if (createSyncMethod == null)
            {
                throw new InvalidOperationException("WebArtifactSyncronizerFactory.Create não foi localizado.");
            }

            var profiler = new StepProfiler("COMPARE");
            // SMARTINSTALL_NOCACHE=1 força a comparação original da plataforma (sem cache de hash).
            var hashCache = Environment.GetEnvironmentVariable("SMARTINSTALL_NOCACHE") == "1"
                ? null
                : new LocalHashCache(_webAppPath);
            if (hashCache != null) Console.WriteLine($"[CACHE] {hashCache.Count} hash(es) em cache carregado(s).");

            _indexReader = null;
            if (Environment.GetEnvironmentVariable("SMARTINSTALL_PLATFORM_INDEX") != "1")
            {
                try { _indexReader = new IndexDbReader(_binDir); }
                catch (Exception ex) { Console.WriteLine($"[AVISO COMPARE] Leitura direta do índice indisponível ({ex.Message}); usando o indexer da plataforma."); }
            }
            int totalCount = 0;
            int pendingCount = 0;

            var summary = new Dictionary<string, (int total, int pending, int fileOnly, int different, int dbOnly)>();

            foreach (var typeVal in Enum.GetValues(artifactTypeEnum))
            {
                if (typeVal.ToString().Equals("Unknown", StringComparison.OrdinalIgnoreCase)) continue;

                string typeName = typeVal.ToString();
                int typeTotal = 0;
                int typePending = 0;
                int typeFileOnly = 0;
                int typeDifferent = 0;
                int typeDbOnly = 0;

                try
                {
                    object syncronizerObj = createSyncMethod.Invoke(null, new object[] { typeVal });
                    if (syncronizerObj == null) continue;

                    var appPathProp = syncronizerObj.GetType().GetProperty("ApplicationPath");
                    appPathProp?.SetValue(syncronizerObj, _webAppPath);

                    List<KeyValuePair<string, string>> rows = null;

                    // Caminho rápido: índice do banco (plataforma) + MD5 local com cache persistente.
                    if (hashCache != null)
                    {
                        try
                        {
                            rows = FastCompare(syncronizerObj, typeName, profiler, hashCache);
                        }
                        catch (Exception fastEx)
                        {
                            var realFast = fastEx;
                            while (realFast is TargetInvocationException && realFast.InnerException != null) realFast = realFast.InnerException;
                            Console.WriteLine($"[AVISO COMPARE] Comparação rápida indisponível para {typeName} ({realFast.Message}); usando comparação da plataforma.");
                        }
                    }

                    if (rows == null)
                    {
                        var swType = System.Diagnostics.Stopwatch.StartNew();
                        rows = LegacyCompare(syncronizerObj);
                        if (rows == null) continue;
                        // CompareArtifacts = índice no banco + MD5 de todos os arquivos locais do tipo
                        profiler.Add($"CompareArtifacts[{typeName}] (plataforma)", swType.Elapsed);
                    }

                    foreach (var row in rows)
                    {
                        typeTotal++;
                        string statusStr = row.Value;

                        if (!statusStr.Equals("Equal", StringComparison.OrdinalIgnoreCase))
                        {
                            typePending++;
                            if (statusStr.Equals("FileOnly", StringComparison.OrdinalIgnoreCase)) typeFileOnly++;
                            else if (statusStr.Equals("Diferent", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Different", StringComparison.OrdinalIgnoreCase)) typeDifferent++;
                            else if (statusStr.Equals("DatabaseOnly", StringComparison.OrdinalIgnoreCase)) typeDbOnly++;

                            Console.WriteLine($"[COMPARE_ITEM] Name={row.Key}; Status={statusStr}");
                        }
                    }

                    summary[typeName] = (typeTotal, typePending, typeFileOnly, typeDifferent, typeDbOnly);
                    totalCount += typeTotal;
                    pendingCount += typePending;
                }
                catch (Exception ex)
                {
                    var realEx = ex;
                    while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                    Console.WriteLine($"[AVISO COMPARE] Falha ao comparar {typeVal}: {realEx.Message}");
                }
            }

            Console.WriteLine("\n================== RESUMO DA COMPARAÇÃO ==================");
            Console.WriteLine($"{"Tipo",-20} | {"Total",-8} | {"Pendentes",-10} | {"Novos",-6} | {"Modificados",-12} | {"Excluídos",-10}");
            Console.WriteLine(new string('-', 75));
            foreach (var kvp in summary)
            {
                Console.WriteLine($"{kvp.Key,-20} | {kvp.Value.total,-8} | {kvp.Value.pending,-10} | {kvp.Value.fileOnly,-6} | {kvp.Value.different,-12} | {kvp.Value.dbOnly,-10}");
            }
            Console.WriteLine(new string('-', 75));
            Console.WriteLine($"[SMART COMPARE] Total Geral: {pendingCount} pendente(s) de {totalCount} analisados.");

            if (hashCache != null)
            {
                Console.WriteLine($"[CACHE] hits={hashCache.Hits}, recalculados={hashCache.Misses}.");
                hashCache.Save(true);
            }

            profiler.PrintSummary();

            // Libera memória de coleções e working set
            summary.Clear();
            MemoryOptimizer.TrimMemory();
        }

        private IndexDbReader _indexReader;

        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Mesma regra de WebArtifactSyncronizerBase.CompareArtifacts(false), mas com MD5 em cache e sem
        /// modificar dicionário em paralelo: ignora camada 60 e 50 (cliente); DatabaseOnly exclui camada 50.
        /// </summary>
        private List<KeyValuePair<string, string>> FastCompare(object sync, string typeName, StepProfiler profiler, LocalHashCache cache)
        {
            Type t = sync.GetType();
            string dir = t.GetProperty("ArtifactPath", AnyInstance)?.GetValue(sync, null) as string;
            if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("ArtifactPath indisponível");

            Dictionary<string, string> db = null;

            // Padrão: SELECT direto em W_INDICEARTEFATOS (~100 ms por tipo). O indexer da plataforma
            // (CallDatabaseIndexerTask) também reindexa artefatos do banco ainda não indexados e custa
            // ~22 s só para Views; use SMARTINSTALL_PLATFORM_INDEX=1 para forçá-lo.
            if (_indexReader != null)
            {
                int tipo = Convert.ToInt32(t.GetProperty("ArtifactIndexType", AnyInstance).GetValue(sync, null));
                db = profiler.Measure($"Índice no banco[{typeName}] (SELECT direto)", () => _indexReader.Read(tipo));
            }

            if (db == null)
            {
                var indexer = t.GetMethods(AnyInstance).FirstOrDefault(m => m.Name == "CallDatabaseIndexerTask" && m.GetParameters().Length == 1);
                if (indexer == null) throw new InvalidOperationException("CallDatabaseIndexerTask não localizado");
                db = profiler.Measure($"Índice no banco[{typeName}] (indexer da plataforma)",
                    () => indexer.Invoke(sync, new object[] { false }) as Dictionary<string, string>);
                if (db == null) throw new InvalidOperationException("índice do banco retornou nulo");
                db = new Dictionary<string, string>(db);
            }

            string[] files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.*.xml", SearchOption.TopDirectoryOnly)
                : new string[0];
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            int hits0 = cache.Hits, miss0 = cache.Misses;
            var hashes = new ConcurrentDictionary<string, string>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, file =>
            {
                string name = Path.GetFileName(file);
                int layer = GetLayer(name);
                if (layer == 50 || layer == 60) return;
                hashes[name] = cache.GetHash(file);
            });
            profiler.Add($"MD5 local[{typeName}] ({files.Length} arq., cache hits={cache.Hits - hits0}, recalculados={cache.Misses - miss0})", sw.Elapsed);

            var rows = new List<KeyValuePair<string, string>>(files.Length);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string hash;
                if (!hashes.TryGetValue(name, out hash)) continue;

                string status;
                string dbHash;
                if (!db.TryGetValue(name, out dbHash)) status = "FileOnly";
                else status = dbHash.Split(',').Contains(hash) ? "Equal" : "Diferent";

                if (status == "Diferent" && StepProfiler.DeepMode)
                    Console.WriteLine($"[PROFILE] DIFF {name}: arquivo={hash} banco={dbHash}");

                db.Remove(name);
                rows.Add(new KeyValuePair<string, string>(name, status));
            }

            foreach (string key in db.Keys)
            {
                if (GetLayer(key) != 50)
                    rows.Add(new KeyValuePair<string, string>(key, "DatabaseOnly"));
            }
            return rows;
        }

        private static int GetLayer(string artifactFileName)
        {
            var parts = artifactFileName.Split('.');
            int layer;
            return parts.Length >= 2 && int.TryParse(parts[parts.Length - 2], out layer) ? layer : -1;
        }

        /// <summary>Comparação original da plataforma (CompareArtifacts), convertida para (nome, status).</summary>
        private static List<KeyValuePair<string, string>> LegacyCompare(object sync)
        {
            var compareMethod = sync.GetType().GetMethods(AnyInstance)
                .FirstOrDefault(m => m.Name.Equals("CompareArtifacts", StringComparison.OrdinalIgnoreCase));
            if (compareMethod == null) return null;

            object gridRowsObj = null;
            if (compareMethod.GetParameters().Length == 1) gridRowsObj = compareMethod.Invoke(sync, new object[] { false });
            else if (compareMethod.GetParameters().Length == 0) gridRowsObj = compareMethod.Invoke(sync, null);

            var rows = new List<KeyValuePair<string, string>>();
            if (gridRowsObj is IEnumerable gridRows)
            {
                foreach (var row in gridRows)
                {
                    if (row == null) continue;
                    Type rowType = row.GetType();
                    string status = rowType.GetProperty("Status")?.GetValue(row)?.ToString() ?? "Equal";
                    object nameObj = rowType.GetProperty("ArtifactName")?.GetValue(row)
                        ?? rowType.GetProperty("FileName")?.GetValue(row)
                        ?? rowType.GetProperty("Name")?.GetValue(row);
                    rows.Add(new KeyValuePair<string, string>(nameObj?.ToString() ?? string.Empty, status));
                }
            }
            return rows;
        }
    }
}
