using Benner.Tecnologia.Common;
using Benner.Tecnologia.Wes.Components.WebApp;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BennerSmartInstaller
{
    /// <summary>
    /// Replica exatamente a lógica de WES.CLI.CLIConfiguration.
    /// Carrega as appSettings do web.config do WES para o contexto do executável.
    /// </summary>
    public static class CLIConfiguration
    {
        public static string WebConfigFilePath => BennerConfiguration.WebConfigFilePath;

        public static bool HasWebConfigFile => File.Exists(WebConfigFilePath);

        public static void LoadWebConfig()
        {
            bool hasModelo = File.Exists(BennerConfiguration.WebConfigModeloFilePath);
            if (!HasWebConfigFile && !hasModelo)
                return;
            if (!HasWebConfigFile & hasModelo)
                File.Copy(BennerConfiguration.WebConfigModeloFilePath, BennerConfiguration.WebConfigFilePath);

            string configurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
            XDocument webConfigDoc = XDocument.Load(WebConfigFilePath);
            XDocument exeConfigDoc = XDocument.Load(configurationFile);
            XElement webAppSettings = webConfigDoc.Root.Element("appSettings");

            if (webAppSettings == null)
            {
                File.Delete(WebConfigFilePath);
                WesFilesNormalizer.Normalize();
                LoadWebConfig();
            }
            else
            {
                XElement exeAppSettings = exeConfigDoc.Root.Element("appSettings");
                if (exeAppSettings == null)
                {
                    exeAppSettings = new XElement("appSettings");
                    exeConfigDoc.Root.Add(exeAppSettings);
                }
                
                exeAppSettings.RemoveAll();
                exeAppSettings.Add(webAppSettings.Nodes().ToArray());
                exeConfigDoc.Save(configurationFile);
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        public static XDocument ReadWebConfig()
        {
            return XDocument.Load(WebConfigFilePath, LoadOptions.None);
        }

        public static void SaveWebConfig(XDocument webConfig)
        {
            webConfig.Save(WebConfigFilePath, SaveOptions.None);
        }
    }
}
