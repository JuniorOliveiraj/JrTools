using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BennerSmartInstaller.DynamicProxy;

namespace BennerSmartInstaller.Bootstrapping
{
    public static class BennerEnvironmentInitializer
    {
        public static void Initialize(string binDir)
        {
            // 1. Carrega apenas as DLLs essenciais de tecnologia sob demanda (economiza ~100MB de RAM)
            string[] coreDlls = new[]
            {
                "Benner.Tecnologia.Common.dll",
                "Benner.Tecnologia.Business.dll",
                "Benner.Tecnologia.Metadata.dll",
                "Benner.Tecnologia.Wes.Components.WebApp.dll",
                "Benner.Tecnologia.Bas.AppServer.BusinessLogic.dll"
            };

            foreach (var dllName in coreDlls)
            {
                string dllPath = Path.Combine(binDir, dllName);
                if (File.Exists(dllPath))
                {
                    try { Assembly.LoadFrom(dllPath); } catch { }
                }
            }

            string wesExePath = Path.Combine(binDir, "wes.exe");
            Assembly wesAsm = null;
            if (File.Exists(wesExePath))
            {
                try { wesAsm = Assembly.LoadFrom(wesExePath); } catch { }
            }

            // 2. Configura a camada em Benner.Tecnologia.Business.Factory
            try
            {
                string businessDll = Path.Combine(binDir, "Benner.Tecnologia.Business.dll");
                if (File.Exists(businessDll))
                {
                    var businessAsm = Assembly.LoadFrom(businessDll);
                    var factoryType = businessAsm.GetType("Benner.Tecnologia.Business.Factory");
                    if (factoryType != null)
                    {
                        var mSetPres = factoryType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "SetPresentationLayer" && m.GetParameters().Length == 0);
                        mSetPres?.Invoke(null, null);

                        var mSetBusiness = factoryType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            .FirstOrDefault(m => m.Name == "SetBusinessLayer" && m.GetParameters().Length == 0);
                        mSetBusiness?.Invoke(null, null);
                    }
                }
            }
            catch { }

            // 3. Executa WES.CLI.Program.Initialize() que configura o IoC container nativo do WES
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

            // 4. Garante binding de IBusinessComponentProxyFactory e ICustomInstallArtifacts no Ninject Kernel
            try
            {
                Type depContainerType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => GetLoadableTypes(a)).FirstOrDefault(t => t != null && t.Name == "DependencyContainer");
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

            // 5. InitAppServer
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

            // 6. LegacyAppServer
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

            // Otimiza o working set após a inicialização
            MemoryOptimizer.TrimMemory();
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                return new Type[0];
            }
        }

        private static void RegisterBindingIfMissing(object internalKernel, string interfaceName, string defaultImplName)
        {
            Type targetInterface = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => GetLoadableTypes(a)).FirstOrDefault(t => t.Name == interfaceName);
            if (targetInterface == null) return;

            Type targetImpl = null;
            if (!string.IsNullOrEmpty(defaultImplName))
            {
                targetImpl = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => GetLoadableTypes(a)).FirstOrDefault(t => t.Name == defaultImplName || (targetInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract));
            }
            else
            {
                targetImpl = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => GetLoadableTypes(a)).FirstOrDefault(t => targetInterface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            }

            if (targetImpl == null)
            {
                targetImpl = DynamicTypeGenerator.CreateDummyTypeForInterface(targetInterface);
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
    }
}
