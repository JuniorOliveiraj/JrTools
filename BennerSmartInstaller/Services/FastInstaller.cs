using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BennerSmartInstaller.Services
{
    public class FastInstallResult
    {
        public int Imported;
        public int Restored;
        public int AlreadyUpToDate;
        public int Skipped;
        public List<KeyValuePair<string, string>> Failed = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// Instalação incremental: reproduz InstallArtifactsManager.StartInstalation com o "prepare" próprio:
    ///  - diff por MD5 (cache local) contra um SELECT direto em W_INDICEARTEFATOS, sem o indexer da plataforma
    ///    (que reindexa todos os tipos a cada chamada e levava ~37 s mesmo para 1 artefato);
    ///  - só ArtifactVersion.InsertMany + ArtifactImporterTask do que mudou (Novo/Modificado);
    ///  - 2ª passada: a plataforma, ao importar a camada N, regrava o índice das camadas acima com o hash da camada N;
    ///    os irmãos de camada superior que estavam "Igual" voltam a aparecer como "Modificado". Eles são reimportados;
    ///  - falhas lidas da tabela de staging (W_VERSAOARTEFATOS), sem retry do lote inteiro;
    ///  - GeneratePages/ClearCache só se algo daquele tipo foi realmente importado.
    /// Qualquer incompatibilidade de reflexão acontece no construtor (antes de gravar) e o chamador cai no caminho da plataforma.
    /// </summary>
    public class FastInstaller
    {
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private class Candidate
        {
            public string Name;
            public string Path;
            public int Layer;
            public string Hash;
            public string Status;
        }

        private class TypePlan
        {
            public object TypeVal;
            public string TypeName;
            public object ArtifactType;
            public object IndexType;
            public string Dir;
            public List<Candidate> ToImport = new List<Candidate>();
            public List<KeyValuePair<string, string>> Rejected = new List<KeyValuePair<string, string>>();
            public List<string> Restore = new List<string>();
            public int UpToDate;
            public int Skipped;
        }

        private readonly string _webAppPath;
        private readonly StepProfiler _profiler;
        private readonly IndexDbReader _reader;
        private readonly LocalHashCache _cache;

        private readonly Type _tType, _tTransfer, _tRequest, _tLayer;
        private readonly MethodInfo _insertMany, _deleteAll, _getManyBy, _create, _ensureOrder, _syncMethod;
        private readonly object _syncManager;
        private readonly MethodInfo _generatePages, _clearCache, _afterExecute;
        private readonly PropertyInfo _ignorePages;

        public FastInstaller(string webAppPath, string binDir, Assembly metadataAsm, Assembly componentsAsm, StepProfiler profiler)
        {
            _webAppPath = webAppPath;
            _profiler = profiler;

            // InstallArtifactsManager e CustomInstallArtifacts vivem em Wes.Components.WebApp; os demais em Metadata.
            var componentsFirst = new HashSet<string> { "InstallArtifactsManager", "CustomInstallArtifacts" };
            Func<string, Type> find = name =>
            {
                Assembly first = componentsFirst.Contains(name) ? componentsAsm : metadataAsm;
                Assembly second = componentsFirst.Contains(name) ? metadataAsm : componentsAsm;
                Type t = SafeTypes(first).FirstOrDefault(x => x.Name == name)
                         ?? SafeTypes(second).FirstOrDefault(x => x.Name == name);
                if (t == null) throw new InvalidOperationException($"Tipo {name} não localizado.");
                return t;
            };
            Func<Type, string, int, BindingFlags, MethodInfo> method = (type, name, args, flags) =>
            {
                var m = type.GetMethods(flags).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args);
                if (m == null) throw new InvalidOperationException($"Método {type.Name}.{name}({args}) não localizado.");
                return m;
            };

            _tType = find("ArtifactType");
            _tLayer = find("ArtifactLayer");
            _tTransfer = find("ArtifactDataTransfer");
            _tRequest = find("ArtifactImporterTaskRequest");

            var tVersion = find("ArtifactVersion");
            _insertMany = method(tVersion, "InsertMany", 2, AnyStatic);
            _deleteAll = method(tVersion, "DeleteAll", 0, AnyStatic);
            _getManyBy = method(tVersion, "GetManyBy", 1, AnyStatic);

            _create = method(find("WebArtifactSyncronizerFactory"), "Create", 1, AnyStatic);
            _ensureOrder = method(find("ArtifactInstallOrderer"), "EnsureTypesOrder", 1, AnyStatic);

            var tManager = find("WebArtifactsSyncronizerManager");
            _syncManager = tManager.GetProperty("Instance", AnyStatic).GetValue(null, null);
            _syncMethod = method(tManager, "SyncronizeBaseFromFiles", 3, AnyInstance);

            var tInstall = find("InstallArtifactsManager");
            _generatePages = method(tInstall, "GeneratePages", 1, AnyStatic);
            _ignorePages = tInstall.GetProperty("IgnorePageGeneration", AnyStatic);
            if (_ignorePages == null) throw new InvalidOperationException("InstallArtifactsManager.IgnorePageGeneration não localizado.");
            _clearCache = method(find("CacheArtifactHelper"), "ClearCache", 1, AnyStatic);
            _afterExecute = method(find("CustomInstallArtifacts"), "AfterExecuteInstallArtifacts", 0, AnyStatic);

            _reader = new IndexDbReader(binDir);
            _cache = new LocalHashCache(webAppPath);
        }

        private static IEnumerable<Type> SafeTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }

        // ------------------------------------------------------------------------------------------------

        /// <param name="items">(caminho completo do XML, valor do enum ArtifactType)</param>
        public FastInstallResult Run(List<KeyValuePair<string, object>> items, bool installCustomer, bool dryRun)
        {
            var result = new FastInstallResult();

            var typeList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_tType));
            foreach (var tv in items.Select(i => i.Value).Distinct()) typeList.Add(tv);
            var ordered = ((IEnumerable)_ensureOrder.Invoke(null, new object[] { typeList })).Cast<object>().ToList();

            var pass1 = new List<TypePlan>();
            foreach (object tv in ordered)
            {
                object typeVal = tv;
                var files = items.Where(i => Equals(i.Value, typeVal)).Select(i => i.Key).ToList();
                pass1.Add(_profiler.Measure($"[rápido] Diff por hash [{typeVal}] ({files.Count} arq.)",
                    () => BuildPlan(typeVal, files, installCustomer, true)));
            }

            foreach (var p in pass1)
            {
                result.AlreadyUpToDate += p.UpToDate;
                result.Skipped += p.Skipped;
                result.Failed.AddRange(p.Rejected);
                Console.WriteLine($"[PLANO] {p.TypeName}: {p.ToImport.Count} a importar, {p.UpToDate} já atualizado(s)" +
                                  (p.Skipped > 0 ? $", {p.Skipped} ignorado(s) (camada 50/60)" : "") +
                                  (p.Restore.Count > 0 ? $", {p.Restore.Count} reindexação(ões) de camada superior" : ""));
                if (dryRun)
                {
                    foreach (var c in p.ToImport.Take(300)) Console.WriteLine($"    + {c.Name} ({c.Status})");
                    foreach (var r in p.Restore.Take(300)) Console.WriteLine($"    ~ {Path.GetFileName(r)} (reindexar após camada inferior)");
                }
            }

            if (dryRun)
            {
                result.Imported = pass1.Sum(p => p.ToImport.Count);
                result.Restored = pass1.Sum(p => p.Restore.Count);
                return result;
            }

            if (pass1.All(p => p.ToImport.Count == 0))
            {
                Console.WriteLine("[SMART INSTALL] Tudo já está atualizado no banco; nada a importar.");
                _cache.Save(false);
                return result;
            }

            var changed = new Dictionary<string, object>();

            // Mesma limpeza do staging que a plataforma faz no início de InstallArtifacts.
            _profiler.Measure("[rápido] ArtifactVersion.DeleteAll", () => _deleteAll.Invoke(null, null));
            ExecutePass(pass1, "passada 1", false, result, changed);

            // 2ª passada: irmãos de camada superior que estavam "Igual" e tiveram o índice regravado pela plataforma.
            var pass2 = new List<TypePlan>();
            foreach (var p in pass1.Where(x => x.Restore.Count > 0))
            {
                var plan = BuildPlan(p.TypeVal, p.Restore, installCustomer, false);
                if (plan.ToImport.Count > 0) pass2.Add(plan);
            }
            if (pass2.Count > 0)
            {
                Console.WriteLine($"[SMART INSTALL] Passada 2: reimportando {pass2.Sum(p => p.ToImport.Count)} artefato(s) de camada superior cujo índice foi regravado.");
                ExecutePass(pass2, "passada 2", true, result, changed);
            }

            if (changed.Count > 0)
            {
                if (changed.ContainsKey("Page"))
                {
                    bool ignore = (bool)_ignorePages.GetValue(null, null);
                    if (!ignore) _profiler.Measure("[plataforma] GeneratePages", () => _generatePages.Invoke(null, new object[] { _webAppPath }));
                    else Console.WriteLine("[PROFILE] GeneratePages ignorado (WES em linha de comando/contêiner).");
                }
                _profiler.Measure("[plataforma] ClearCache dos tipos alterados", () =>
                {
                    foreach (var tv in changed.Values) _clearCache.Invoke(null, new[] { tv });
                });
                _profiler.Measure("[plataforma] AfterExecuteInstallArtifacts", () => _afterExecute.Invoke(null, null));
            }

            _cache.Save(false);
            return result;
        }

        // ------------------------------------------------------------------------------------------------

        private TypePlan BuildPlan(object typeVal, List<string> files, bool customer, bool withSiblings)
        {
            object sync = _create.Invoke(null, new[] { typeVal });
            Type st = sync.GetType();
            st.GetProperty("ApplicationPath")?.SetValue(sync, _webAppPath);

            var plan = new TypePlan
            {
                TypeVal = typeVal,
                TypeName = typeVal.ToString(),
                ArtifactType = st.GetProperty("ArtifactType", AnyInstance).GetValue(sync, null),
                IndexType = st.GetProperty("ArtifactIndexType", AnyInstance).GetValue(sync, null),
                Dir = st.GetProperty("ArtifactPath", AnyInstance).GetValue(sync, null) as string
            };

            var db = _reader.Read(Convert.ToInt32(plan.IndexType));
            var restore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(file);
                int layer = GetLayer(name);
                if (layer < 0)
                {
                    plan.Rejected.Add(new KeyValuePair<string, string>(name, "O arquivo não possui a camada no nome (*.20.xml)"));
                    continue;
                }
                // Regras da plataforma (ShouldSkipFile): camada 60 nunca; camada 50 só com installCustomerArtifacts.
                if (layer == 60 || (!customer && layer == 50)) { plan.Skipped++; continue; }

                string hash = _cache.GetHash(file);
                string dbHash;
                string status;
                if (!db.TryGetValue(name, out dbHash)) status = "Novo";
                else status = dbHash.Split(',').Contains(hash) ? "Igual" : "Modificado";

                if (status == "Igual") { plan.UpToDate++; continue; }
                plan.ToImport.Add(new Candidate { Name = name, Path = file, Layer = layer, Hash = hash, Status = status });
            }

            if (withSiblings && !string.IsNullOrEmpty(plan.Dir) && Directory.Exists(plan.Dir))
            {
                var importing = new HashSet<string>(plan.ToImport.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var cand in plan.ToImport)
                {
                    string id = GetId(cand.Name);
                    foreach (string sib in Directory.GetFiles(plan.Dir, id + ".*.xml", SearchOption.TopDirectoryOnly))
                    {
                        string sname = Path.GetFileName(sib);
                        int sl = GetLayer(sname);
                        if (sl <= cand.Layer || sl == 60 || (!customer && sl == 50)) continue;
                        if (!string.Equals(GetId(sname), id, StringComparison.OrdinalIgnoreCase)) continue;
                        if (importing.Contains(sname)) continue; // já será importado e regrava o próprio índice
                        string sdb;
                        if (db.TryGetValue(sname, out sdb) && sdb.Split(',').Contains(_cache.GetHash(sib)))
                            restore.Add(sib); // estava "Igual": o índice será regravado pela importação da camada inferior
                    }
                }
            }
            plan.Restore = restore.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            return plan;
        }

        private void ExecutePass(List<TypePlan> plans, string label, bool restorePass, FastInstallResult result, Dictionary<string, object> changed)
        {
            var requests = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_tRequest));
            object layerAll = Enum.ToObject(_tLayer, 999);
            var active = new List<TypePlan>();

            foreach (var p in plans.Where(x => x.ToImport.Count > 0))
            {
                var transfers = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(_tTransfer));
                foreach (var c in p.ToImport)
                {
                    object t = Activator.CreateInstance(_tTransfer);
                    _tTransfer.GetProperty("Name").SetValue(t, c.Name, null);
                    _tTransfer.GetProperty("Hash").SetValue(t, c.Hash, null);
                    _tTransfer.GetProperty("Layer").SetValue(t, c.Layer, null);
                    _tTransfer.GetProperty("Content").SetValue(t, File.ReadAllBytes(c.Path), null);
                    transfers.Add(t);
                }
                _profiler.Measure($"[plataforma] InsertMany [{p.TypeName}] x{p.ToImport.Count} ({label})",
                    () => _insertMany.Invoke(null, new object[] { transfers, p.ArtifactType }));

                object req = Activator.CreateInstance(_tRequest);
                _tRequest.GetProperty("ArtifactType").SetValue(req, p.IndexType, null);
                _tRequest.GetProperty("LayerToImport").SetValue(req, layerAll, null);
                requests.Add(req);
                active.Add(p);
            }
            if (active.Count == 0) return;

            Exception importEx = null;
            try
            {
                _profiler.Measure($"[plataforma] Importação ({label}, {active.Sum(a => a.ToImport.Count)} artefatos)",
                    () => _syncMethod.Invoke(_syncManager, new object[] { layerAll, requests, false }));
            }
            catch (TargetInvocationException tie)
            {
                importEx = tie;
                while (importEx is TargetInvocationException && importEx.InnerException != null) importEx = importEx.InnerException;
                Console.WriteLine($"[AVISO SMART INSTALL] Falhas na importação ({label}): {importEx.Message}");
            }

            // Sucesso = a linha de W_VERSAOARTEFATOS foi consumida; quem sobrou no staging não foi importado.
            int failedHere = 0;
            foreach (var p in active)
            {
                var staging = (Dictionary<string, Tuple<int, string>>)_getManyBy.Invoke(null, new[] { p.IndexType });
                foreach (var c in p.ToImport)
                {
                    Tuple<int, string> row;
                    if (staging.TryGetValue(c.Name, out row))
                    {
                        failedHere++;
                        string reason = !string.IsNullOrWhiteSpace(row.Item2) ? row.Item2 : (importEx != null ? importEx.Message : "não importado");
                        result.Failed.Add(new KeyValuePair<string, string>(c.Name, reason));
                        Console.WriteLine($"[FALHA] [{p.TypeName}] {c.Name}: {reason}");
                    }
                    else
                    {
                        if (restorePass) result.Restored++; else result.Imported++;
                        changed[p.TypeName] = p.TypeVal;
                    }
                }
            }
            if (importEx != null && failedHere == 0) throw importEx; // erro que não é de um artefato específico
        }

        private static int GetLayer(string artifactFileName)
        {
            var parts = artifactFileName.Split('.');
            int layer;
            return parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out layer) ? layer : -1;
        }

        private static string GetId(string artifactFileName)
        {
            var parts = artifactFileName.Split('.');
            return string.Join(".", parts.Take(parts.Length - 2));
        }
    }
}
