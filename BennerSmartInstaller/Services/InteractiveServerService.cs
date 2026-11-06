using System;
using System.Reflection;
using BennerSmartInstaller.Bootstrapping;

namespace BennerSmartInstaller.Services
{
    public class InteractiveServerService
    {
        private readonly string _webAppPath;
        private readonly string _binDir;

        public InteractiveServerService(string webAppPath, string binDir)
        {
            _webAppPath = webAppPath;
            _binDir = binDir;
        }

        public void Run()
        {
            MemoryOptimizer.TrimMemory();
            Console.WriteLine("[SERVER_READY] Benner Smart Installer Server inicializado com sucesso e pronto para comandos.");

            string line;
            while ((line = Console.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.Equals("PING", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[PONG]");
                    Console.WriteLine("[CMD_END:SUCCESS]");
                }
                else if (line.Equals("STATUS", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string metadataDll = System.IO.Path.Combine(_binDir, "Benner.Tecnologia.Metadata.dll");
                        if (System.IO.File.Exists(metadataDll))
                        {
                            var metadataAsm = Assembly.LoadFrom(metadataDll);
                            Type artifactVersionType = System.Linq.Enumerable.FirstOrDefault(metadataAsm.GetTypes(), t => t.Name == "ArtifactVersion");
                            var getCountMethod = artifactVersionType?.GetMethod("GetCount", BindingFlags.Public | BindingFlags.Static);
                            if (getCountMethod != null)
                            {
                                int pendingCount = Convert.ToInt32(getCountMethod.Invoke(null, null));
                                Console.WriteLine($"[STATUS_INFO] Faltam atualizar {pendingCount} artefatos");
                            }
                        }
                        Console.WriteLine("[CMD_END:SUCCESS]");
                    }
                    catch (Exception ex)
                    {
                        var realEx = ex;
                        while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                        Console.WriteLine($"[AVISO SERVER STATUS] {realEx.Message}");
                        Console.WriteLine($"[CMD_END:ERROR:{realEx.Message}]");
                    }
                }
                else if (line.Equals("COMPARE", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var comparator = new ArtifactComparatorService(_webAppPath, _binDir);
                        comparator.Execute();
                        Console.WriteLine("[CMD_END:SUCCESS]");
                    }
                    catch (Exception ex)
                    {
                        var realEx = ex;
                        while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                        Console.WriteLine($"[AVISO SERVER COMPARE] {realEx.Message}");
                        Console.WriteLine($"[CMD_END:ERROR:{realEx.Message}]");
                    }
                }
                else if (line.StartsWith("INSTALL ", StringComparison.OrdinalIgnoreCase))
                {
                    string filesArg = line.Substring("INSTALL ".Length).Trim();
                    try
                    {
                        var installer = new ArtifactInstallerService(_webAppPath, _binDir);
                        installer.Execute(filesArg);
                        Console.WriteLine("[CMD_END:SUCCESS]");
                    }
                    catch (Exception ex)
                    {
                        var realEx = ex;
                        while (realEx is TargetInvocationException && realEx.InnerException != null) realEx = realEx.InnerException;
                        Console.WriteLine($"[AVISO SERVER INSTALL] {realEx.Message}");
                        Console.WriteLine($"[CMD_END:ERROR:{realEx.Message}]");
                    }
                }
                else if (line.Equals("EXIT", StringComparison.OrdinalIgnoreCase) ||
                         line.Equals("QUIT", StringComparison.OrdinalIgnoreCase) ||
                         line.Equals("STOP", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[SERVER_STOPPED]");
                    break;
                }
                else
                {
                    Console.WriteLine($"[AVISO SERVER] Comando desconhecido: {line}");
                    Console.WriteLine("[CMD_END:ERROR:Comando desconhecido]");
                }

                // Mantém a pegada de memória mínima enquanto aguarda novas instruções
                MemoryOptimizer.TrimMemory();
            }
        }
    }
}
