using System;
using System.Collections;
using System.Collections.Generic;
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

                    var compareMethod = syncronizerObj.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name.Equals("CompareArtifacts", StringComparison.OrdinalIgnoreCase));

                    if (compareMethod == null) continue;

                    object gridRowsObj = null;
                    if (compareMethod.GetParameters().Length == 1)
                    {
                        gridRowsObj = compareMethod.Invoke(syncronizerObj, new object[] { false });
                    }
                    else if (compareMethod.GetParameters().Length == 0)
                    {
                        gridRowsObj = compareMethod.Invoke(syncronizerObj, null);
                    }

                    if (gridRowsObj is IEnumerable gridRows)
                    {
                        foreach (var row in gridRows)
                        {
                            if (row == null) continue;
                            typeTotal++;

                            Type rowType = row.GetType();
                            object statusObj = rowType.GetProperty("Status")?.GetValue(row);
                            string statusStr = statusObj?.ToString() ?? "Equal";

                            if (!statusStr.Equals("Equal", StringComparison.OrdinalIgnoreCase))
                            {
                                typePending++;
                                if (statusStr.Equals("FileOnly", StringComparison.OrdinalIgnoreCase)) typeFileOnly++;
                                else if (statusStr.Equals("Diferent", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Different", StringComparison.OrdinalIgnoreCase)) typeDifferent++;
                                else if (statusStr.Equals("DatabaseOnly", StringComparison.OrdinalIgnoreCase)) typeDbOnly++;
                            }
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

            // Libera memória de coleções e working set
            summary.Clear();
            MemoryOptimizer.TrimMemory();
        }
    }
}
