using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BennerSmartInstaller.Artifacts
{
    public static class ArtifactModelFactory
    {
        public static object MapArtifactType(string folderName, Type artifactTypeEnum)
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

        public static object CreateArtifactToInstall(Type artifactToInstallType, Type artifactTypeEnum, string fullPath)
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
                        object typeVal = MapArtifactType(folderName, artifactTypeEnum);
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
                        object typeVal = MapArtifactType(folderName, artifactTypeEnum);
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
