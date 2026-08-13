using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace BennerSmartInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  Benner Smart Installer - Instalação Seletiva");
            Console.WriteLine("==================================================");

            string webAppPath = null;
            string filesArg = null;
            string commandMode = "install";

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("compare", StringComparison.OrdinalIgnoreCase) || args[i].Equals("-c", StringComparison.OrdinalIgnoreCase) || args[i].Equals("--compare", StringComparison.OrdinalIgnoreCase))
                {
                    commandMode = "compare";
                }
                else if (args[i].Equals("install", StringComparison.OrdinalIgnoreCase))
                {
                    commandMode = "install";
                }
                else if (args[i] == "-a" && i + 1 < args.Length)
                {
                    webAppPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "-f" && i + 1 < args.Length)
                {
                    filesArg = args[i + 1];
                    i++;
                }
            }

            if (string.IsNullOrWhiteSpace(webAppPath))
            {
                Console.WriteLine("Uso: BennerSmartInstaller.exe install -a \"<caminho_webApp>\" -f \"<arquivo1;arquivo2>\"");
                Console.WriteLine("Ou:  BennerSmartInstaller.exe compare -a \"<caminho_webApp>\"");
                Environment.Exit(1);
                return;
            }

            if (commandMode == "install" && string.IsNullOrWhiteSpace(filesArg))
            {
                Console.WriteLine("Uso em modo instalação: BennerSmartInstaller.exe install -a \"<caminho_webApp>\" -f \"<arquivo1;arquivo2>\"");
                Environment.Exit(1);
                return;
            }

            string binDir = Path.Combine(webAppPath, "Bin");
            if (!Directory.Exists(binDir))
            {
                Console.WriteLine($"[ERRO] Pasta Bin do WebApp não encontrada em: {binDir}");
                Environment.Exit(1);
                return;
            }

            // 1. Carrega Web.Config IMEDIATAMENTE antes de qualquer inicialização do Benner
            AppDomain.CurrentDomain.SetData("APPBASE", webAppPath);
            CarregarWebConfig(webAppPath);

            // 2. Resolve dependências dos assemblies do Benner a partir da pasta Bin do WebApp
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string asmName = new AssemblyName(resolveArgs.Name).Name + ".dll";
                string asmPath = Path.Combine(binDir, asmName);
                if (File.Exists(asmPath))
                {
                    return Assembly.LoadFrom(asmPath);
                }
                return null;
            };

            try
            {
                if (commandMode == "compare")
                {
                    ExecutarComparacao(webAppPath, binDir);
                    Console.WriteLine("\n[SUCESSO] Comparação de artefatos concluída com êxito!");
                }
                else
                {
                    ExecutarInstalacao(webAppPath, binDir, filesArg);
                    Console.WriteLine("\n[SUCESSO] Instalação seletiva concluída com êxito!");
                }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                var realEx = ex;
                while (realEx is TargetInvocationException && realEx.InnerException != null)
                {
                    realEx = realEx.InnerException;
                }
                Console.WriteLine($"\n[ERRO FATAL] Falha no instalador de artefatos: {realEx.Message}");
                Console.WriteLine($"[STACK TRACE]\n{realEx.StackTrace}");
                Environment.Exit(1);
            }
        }

        private static void ExecutarInstalacao(string webAppPath, string binDir, string filesArg)
        {
            string metadataDll = Path.Combine(binDir, "Benner.Tecnologia.Metadata.dll");
            string webAppComponentsDll = Path.Combine(binDir, "Benner.Tecnologia.Wes.Components.WebApp.dll");

            if (!File.Exists(metadataDll) || !File.Exists(webAppComponentsDll))
            {
                throw new FileNotFoundException($"DLLs de Metadata/Components ausentes em: {binDir}");
            }

            var metadataAsm = Assembly.LoadFrom(metadataDll);
            var webAppComponentsAsm = Assembly.LoadFrom(webAppComponentsDll);

            // Inicializa IoC Container e AppServer do WES
            InicializarAppServer(binDir, metadataAsm, webAppComponentsAsm);

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

            // Lista Genérica List<ArtifactToInstall>
            var listType = typeof(List<>).MakeGenericType(artifactToInstallType);
            var artifactsList = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");

            string[] relativeFiles = filesArg.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int count = 0;

            foreach (var relFile in relativeFiles)
            {
                string fullPath = relFile.Trim();
                if (!File.Exists(fullPath))
                {
                    fullPath = Path.Combine(webAppPath, relFile.Trim());
                }

                if (File.Exists(fullPath))
                {
                    try
                    {
                        object artifactObj = CriarArtifactToInstall(artifactToInstallType, artifactTypeEnum, fullPath);

                        if (artifactObj != null)
                        {
                            addMethod.Invoke(artifactsList, new[] { artifactObj });
                            Console.WriteLine($"[SMART INSTALL] Selecionado: {Path.GetFileName(fullPath)}");
                            count++;
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

            if (count == 0)
            {
                Console.WriteLine("[AVISO] Nenhum artefato válido encontrado para instalar.");
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

            Console.WriteLine($"[SMART INSTALL] Disparando StartInstalation para {count} artefato(s)...");

            var parameters = startInstalationMethod.GetParameters();
            object[] paramValues = new object[parameters.Length];

            if (parameters.Length == 6)
            {
                paramValues[0] = defaultOrder;
                paramValues[1] = artifactsList;
                paramValues[2] = layerAllObj;
                paramValues[3] = false; // fullInstall = false (instalação seletiva)
                paramValues[4] = webAppPath;
                paramValues[5] = true;  // installCustomerArtifacts = true
            }
            else if (parameters.Length == 5)
            {
                paramValues[0] = defaultOrder;
                paramValues[1] = artifactsList;
                paramValues[2] = false; // fullInstall = false
                paramValues[3] = webAppPath;
                paramValues[4] = true;
            }

            startInstalationMethod.Invoke(null, paramValues);
        }

        private static void ExecutarComparacao(string webAppPath, string binDir)
        {
            string metadataDll = Path.Combine(binDir, "Benner.Tecnologia.Metadata.dll");
            string webAppComponentsDll = Path.Combine(binDir, "Benner.Tecnologia.Wes.Components.WebApp.dll");

            if (!File.Exists(metadataDll) || !File.Exists(webAppComponentsDll))
            {
                throw new FileNotFoundException($"DLLs de Metadata/Components ausentes em: {binDir}");
            }

            var metadataAsm = Assembly.LoadFrom(metadataDll);
            var webAppComponentsAsm = Assembly.LoadFrom(webAppComponentsDll);

            InicializarAppServer(binDir, metadataAsm, webAppComponentsAsm);

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

            foreach (var typeVal in Enum.GetValues(artifactTypeEnum))
            {
                if (typeVal.ToString().Equals("Unknown", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    object syncronizerObj = createSyncMethod.Invoke(null, new object[] { typeVal });
                    if (syncronizerObj == null) continue;

                    var appPathProp = syncronizerObj.GetType().GetProperty("ApplicationPath");
                    appPathProp?.SetValue(syncronizerObj, webAppPath);

                    var initDirMethod = syncronizerObj.GetType().GetMethod("InitializeArtifactsDirectory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    initDirMethod?.Invoke(syncronizerObj, null);

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
                            totalCount++;

                            Type rowType = row.GetType();
                            string artifactName = rowType.GetProperty("ArtifactName")?.GetValue(row)?.ToString() ?? "";
                            object artifactTypeObj = rowType.GetProperty("ArtifactType")?.GetValue(row);
                            object layerObj = rowType.GetProperty("Camada")?.GetValue(row);
                            object statusObj = rowType.GetProperty("Status")?.GetValue(row);

                            string typeStr = artifactTypeObj?.ToString() ?? typeVal.ToString();
                            string layerStr = layerObj != null ? Convert.ToInt32(layerObj).ToString() : "20";
                            string statusStr = statusObj?.ToString() ?? "Equal";

                            Console.WriteLine($"[COMPARE_ITEM] Name={artifactName};Type={typeStr};Layer={layerStr};Status={statusStr}");

                            if (!statusStr.Equals("Equal", StringComparison.OrdinalIgnoreCase))
                            {
                                pendingCount++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var realEx = ex;
                    while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                    Console.WriteLine($"[AVISO COMPARE] Falha ao comparar {typeVal}: {realEx.Message}");
                }
            }

            Console.WriteLine($"[SMART COMPARE] Comparação concluída: {pendingCount} artefato(s) pendente(s) de {totalCount} analisados.");
        }

        private static void CarregarWebConfig(string webAppPath)
        {
            try
            {
                string webConfigPath = Path.Combine(webAppPath, "web.config");
                string exeConfigPath = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;

                if (File.Exists(webConfigPath))
                {
                    var webConfigDoc = XDocument.Load(webConfigPath);
                    var webAppSettings = webConfigDoc.Root?.Element("appSettings");

                    if (webAppSettings != null)
                    {
                        XDocument exeConfigDoc;
                        if (File.Exists(exeConfigPath))
                        {
                            exeConfigDoc = XDocument.Load(exeConfigPath);
                        }
                        else
                        {
                            exeConfigDoc = new XDocument(new XElement("configuration"));
                        }

                        if (exeConfigDoc.Root == null)
                        {
                            exeConfigDoc.Add(new XElement("configuration"));
                        }

                        var exeAppSettings = exeConfigDoc.Root.Element("appSettings");
                        if (exeAppSettings == null)
                        {
                            exeAppSettings = new XElement("appSettings");
                            exeConfigDoc.Root.Add(exeAppSettings);
                        }

                        exeAppSettings.RemoveAll();
                        exeAppSettings.Add(webAppSettings.Nodes().ToArray());
                        exeConfigDoc.Save(exeConfigPath);

                        ConfigurationManager.RefreshSection("appSettings");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO CONFIG] Falha ao sincronizar appSettings do web.config: {ex.Message}");
            }
        }

        private static void InicializarAppServer(string binDir, Assembly metadataAsm, Assembly webAppComponentsAsm)
        {
            try
            {
                foreach (var dllPath in Directory.GetFiles(binDir, "Benner.*.dll"))
                {
                    try { Assembly.LoadFrom(dllPath); } catch { }
                }
            }
            catch { }

            string wesExePath = Path.Combine(binDir, "wes.exe");
            Assembly wesAsm = null;
            if (File.Exists(wesExePath))
            {
                try { wesAsm = Assembly.LoadFrom(wesExePath); } catch { }
            }

            // 1. Configura a camada em Benner.Tecnologia.Business.Factory
            try
            {
                string businessDll = Path.Combine(binDir, "Benner.Tecnologia.Business.dll");
                if (File.Exists(businessDll))
                {
                    var businessAsm = Assembly.LoadFrom(businessDll);
                    var factoryType = businessAsm.GetType("Benner.Tecnologia.Business.Factory");
                    if (factoryType != null)
                    {
                        var mSetPres = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "SetPresentationLayer" && m.GetParameters().Length == 0);
                        mSetPres?.Invoke(null, null);

                        var mSetAppServer = factoryType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "SetAppServerLayer" && m.GetParameters().Length == 0);
                        mSetAppServer?.Invoke(null, null);
                    }
                }
            }
            catch { }

            // 2. Executa WES.CLI.Program.Initialize() que configura o IoC container nativo do WES
            if (wesAsm != null)
            {
                try
                {
                    var programType = wesAsm.GetType("WES.CLI.Program");
                    var initProgMethod = programType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 0);
                    if (initProgMethod != null)
                    {
                        initProgMethod.Invoke(null, null);
                        Console.WriteLine("[BENNER INIT] WES.CLI.Program.Initialize() executado com sucesso!");
                    }
                }
                catch (Exception ex)
                {
                    var realEx = ex;
                    while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                    Console.WriteLine($"[AVISO WES PROGRAM INIT] {realEx.Message}");
                }
            }

            // 3. Garante binding de IBusinessComponentProxyFactory e ICustomInstallArtifacts no Ninject Kernel
            try
            {
                Type depContainerType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "DependencyContainer");
                if (depContainerType != null)
                {
                    var internalKernelProp = depContainerType.GetProperty("InternalKernel", BindingFlags.Public | BindingFlags.Static);
                    object internalKernel = internalKernelProp?.GetValue(null);

                    if (internalKernel != null)
                    {
                        RegisterBindingIfMissing(internalKernel, "IBusinessComponentProxyFactory", "BusinessComponentProxyFactory");
                        RegisterBindingIfMissing(internalKernel, "ICustomInstallArtifacts", null);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO BINDING PROXY] {ex.Message}");
            }

            // 4. InitAppServer
            try
            {
                if (wesAsm != null)
                {
                    var helperType = wesAsm.GetType("WES.CLI.AppServerHelper");
                    var initMethod = helperType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "InitAppServer" && m.GetParameters().Length == 0);
                    if (initMethod != null)
                    {
                        initMethod.Invoke(null, null);
                    }
                }
            }
            catch { }

            // 5. LegacyAppServer
            try
            {
                string appServerDll = Path.Combine(binDir, "Benner.Tecnologia.Bas.AppServer.BusinessLogic.dll");
                if (File.Exists(appServerDll))
                {
                    var appServerAsm = Assembly.LoadFrom(appServerDll);
                    Type legacyAppServerType = null;
                    try
                    {
                        legacyAppServerType = appServerAsm.GetTypes().FirstOrDefault(t => t != null && t.Name == "LegacyAppServer");
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        legacyAppServerType = ex.Types.FirstOrDefault(t => t != null && t.Name == "LegacyAppServer");
                    }

                    if (legacyAppServerType != null)
                    {
                        var unblockMethod = legacyAppServerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "UnblockPool" && m.GetParameters().Length == 0);
                        unblockMethod?.Invoke(null, null);

                        var startMethod = legacyAppServerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "Start" && m.GetParameters().Length == 0);
                        if (startMethod != null)
                        {
                            startMethod.Invoke(null, null);
                            Console.WriteLine("[LEGACY INIT] LegacyAppServer.Start() [0 params] executado com sucesso!");
                        }
                        else
                        {
                            var startMethod3 = legacyAppServerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                .FirstOrDefault(m => m.Name == "Start" && m.GetParameters().Length == 3);
                            if (startMethod3 != null)
                            {
                                Type infraType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).FirstOrDefault(t => t != null && t.Name == "BennerAppInfraServices");
                                Type dbConfigType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } }).FirstOrDefault(t => t != null && t.Name == "BennerAppDbConfiguration");

                                object appConfig = infraType?.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                                object dbConfig = dbConfigType?.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

                                startMethod3.Invoke(null, new object[] { appConfig, dbConfig, "WES" });
                                Console.WriteLine("[LEGACY INIT] LegacyAppServer.Start(appConfig, dbConfig, \"WES\") executado com sucesso!");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var realEx = ex;
                while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                Console.WriteLine($"[AVISO LEGACY APPSERVER] {realEx.Message}");
            }
        }

        private static void RegisterBindingIfMissing(object internalKernel, string interfaceName, string defaultImplName)
        {
            Type targetInterface = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == interfaceName);
            if (targetInterface == null) return;

            Type targetImpl = null;
            if (!string.IsNullOrEmpty(defaultImplName))
            {
                targetImpl = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == defaultImplName || (targetInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract));
            }
            else
            {
                targetImpl = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(t => targetInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            }

            if (targetImpl == null)
            {
                targetImpl = CriarTipoDummyParaInterface(targetInterface);
            }

            if (targetImpl != null)
            {
                var bindMethod = internalKernel.GetType().GetMethod("Bind", new[] { typeof(Type[]) })
                    ?? internalKernel.GetType().GetMethods().FirstOrDefault(m => m.Name == "Bind" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Type[]));

                if (bindMethod != null)
                {
                    var bindingBuilder = bindMethod.Invoke(internalKernel, new object[] { new Type[] { targetInterface } });
                    var toMethod = bindingBuilder?.GetType().GetMethod("To", new[] { typeof(Type) });
                    toMethod?.Invoke(bindingBuilder, new object[] { targetImpl });
                    Console.WriteLine($"[BENNER IOC] Binding dinâmico para {interfaceName} -> {targetImpl.Name} configurado com sucesso.");
                }
            }
        }

        private static Type CriarTipoDummyParaInterface(Type interfaceType)
        {
            try
            {
                var assemblyName = new AssemblyName("BennerDynamicDummyAssembly_" + interfaceType.Name);
                var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("BennerDynamicDummyModule");
                var typeBuilder = moduleBuilder.DefineType("Dummy" + interfaceType.Name, TypeAttributes.Public | TypeAttributes.Class);

                typeBuilder.AddInterfaceImplementation(interfaceType);

                foreach (var method in interfaceType.GetMethods())
                {
                    var methodBuilder = typeBuilder.DefineMethod(
                        method.Name,
                        MethodAttributes.Public | MethodAttributes.Virtual,
                        method.ReturnType,
                        method.GetParameters().Select(p => p.ParameterType).ToArray());

                    var il = methodBuilder.GetILGenerator();
                    if (method.ReturnType != typeof(void))
                    {
                        if (method.ReturnType.IsValueType)
                        {
                            il.Emit(OpCodes.Ldc_I4_0);
                        }
                        else
                        {
                            il.Emit(OpCodes.Ldnull);
                        }
                    }
                    il.Emit(OpCodes.Ret);

                    typeBuilder.DefineMethodOverride(methodBuilder, method);
                }

                return typeBuilder.CreateType();
            }
            catch
            {
                return null;
            }
        }

        private static object MapearArtifactType(string folderName, Type artifactTypeEnum)
        {
            string nameUpper = folderName.ToUpperInvariant();
            string enumTargetName = "Unknown";

            if (nameUpper.Contains("MENU")) enumTargetName = "Menu";
            else if (nameUpper.Contains("PAGE")) enumTargetName = "Page";
            else if (nameUpper.Contains("VIEW")) enumTargetName = "View";
            else if (nameUpper.Contains("TEMPLATE")) enumTargetName = "Template";
            else if (nameUpper.Contains("WIDGET")) enumTargetName = "Widget";
            else if (nameUpper.Contains("TASK")) enumTargetName = "Task";
            else if (nameUpper.Contains("ROLE")) enumTargetName = "Role";
            else if (nameUpper.Contains("SCRIPT")) enumTargetName = "Script";
            else if (nameUpper.Contains("FILTER")) enumTargetName = "Filter";
            else if (nameUpper.Contains("DATASOURCE")) enumTargetName = "DataSource";
            else if (nameUpper.Contains("DYNAMICQUERYTYPE")) enumTargetName = "DynamicQueryType";
            else if (nameUpper.Contains("DYNAMICQUERY")) enumTargetName = "DynamicQuery";

            try
            {
                return Enum.Parse(artifactTypeEnum, enumTargetName, true);
            }
            catch
            {
                return Enum.ToObject(artifactTypeEnum, 0);
            }
        }

        private static object CriarArtifactToInstall(Type artifactToInstallType, Type artifactTypeEnum, string fullPath)
        {
            // 1. Tenta métodos estáticos chamados "Create", "CreateFromFile", "FromFile"
            var staticMethods = artifactToInstallType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var m in staticMethods.Where(m => m.Name.Equals("Create", StringComparison.OrdinalIgnoreCase) ||
                                                       m.Name.Equals("CreateFromFile", StringComparison.OrdinalIgnoreCase) ||
                                                       m.Name.Equals("FromFile", StringComparison.OrdinalIgnoreCase)))
            {
                var pars = m.GetParameters();
                try
                {
                    if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                    {
                        return m.Invoke(null, new object[] { fullPath });
                    }
                    if (pars.Length == 1 && pars[0].ParameterType == typeof(FileInfo))
                    {
                        return m.Invoke(null, new object[] { new FileInfo(fullPath) });
                    }
                    if (pars.Length == 2)
                    {
                        string folderName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? "";
                        object typeVal = MapearArtifactType(folderName, artifactTypeEnum);
                        if (pars[0].ParameterType == typeof(string) && (pars[1].ParameterType.IsEnum || pars[1].ParameterType == typeof(object)))
                        {
                            return m.Invoke(null, new object[] { fullPath, typeVal });
                        }
                        if ((pars[0].ParameterType.IsEnum || pars[0].ParameterType == typeof(object)) && pars[1].ParameterType == typeof(string))
                        {
                            return m.Invoke(null, new object[] { typeVal, fullPath });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AVISO REFLECTION] Método estático '{m.Name}' falhou: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // 2. Tenta construtores da classe ArtifactToInstall
            var ctors = artifactToInstallType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var ctor in ctors)
            {
                var pars = ctor.GetParameters();
                try
                {
                    if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                    {
                        return ctor.Invoke(new object[] { fullPath });
                    }
                    if (pars.Length == 1 && pars[0].ParameterType == typeof(FileInfo))
                    {
                        return ctor.Invoke(new object[] { new FileInfo(fullPath) });
                    }
                    if (pars.Length == 2 && artifactTypeEnum != null)
                    {
                        string folderName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? "";
                        object typeVal = MapearArtifactType(folderName, artifactTypeEnum);
                        if (pars[0].ParameterType == typeof(string) && (pars[1].ParameterType.IsEnum || pars[1].ParameterType == typeof(object)))
                        {
                            return ctor.Invoke(new object[] { fullPath, typeVal });
                        }
                        if ((pars[0].ParameterType.IsEnum || pars[0].ParameterType == typeof(object)) && pars[1].ParameterType == typeof(string))
                        {
                            return ctor.Invoke(new object[] { typeVal, fullPath });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AVISO REFLECTION] Construtor falhou: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // 3. Fallback: qualquer método estático que retorne o próprio artifactToInstallType
            foreach (var m in staticMethods.Where(m => m.ReturnType == artifactToInstallType))
            {
                var pars = m.GetParameters();
                if (pars.Length >= 1 && pars[0].ParameterType == typeof(string))
                {
                    try
                    {
                        var args = new object[pars.Length];
                        args[0] = fullPath;
                        for (int p = 1; p < pars.Length; p++)
                        {
                            args[p] = pars[p].HasDefaultValue ? pars[p].DefaultValue : null;
                        }
                        return m.Invoke(null, args);
                    }
                    catch { }
                }
            }

            string staticInfo = string.Join("; ", staticMethods.Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})"));
            string ctorInfo = string.Join("; ", ctors.Select(c => $".ctor({string.Join(",", c.GetParameters().Select(p => p.ParameterType.Name))})"));
            throw new InvalidOperationException($"Não foi possível instanciar ArtifactToInstall para '{fullPath}'. Métodos estáticos: [{staticInfo}]. Construtores: [{ctorInfo}].");
        }
    }
}
