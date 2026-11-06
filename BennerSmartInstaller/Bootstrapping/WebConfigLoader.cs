using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BennerSmartInstaller.Bootstrapping
{
    public static class WebConfigLoader
    {
        public static void Load(string webAppPath)
        {
            AppDomain.CurrentDomain.SetData("APPBASE", webAppPath);

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
    }
}
