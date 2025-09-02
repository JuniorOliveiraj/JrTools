using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;

namespace JrTools.Services
{
    public static class ConfigHelper
    {
        private const string FileName = "config.json";

        public static async Task<ConfiguracoesdataObject> LerConfiguracoesAsync()
        {
            StorageFile file = null;

            try
            {
                file = await ApplicationData.Current.LocalFolder.GetFileAsync("config.json");
            }
            catch (FileNotFoundException)
            {
                // Copia do Assets se não existir no LocalFolder
                var assetsFolder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
                var assetFile = await assetsFolder.GetFileAsync("config.json");

                var json = await FileIO.ReadTextAsync(assetFile);

                file = await ApplicationData.Current.LocalFolder.CreateFileAsync("config.json", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json);
            }

            var fileContent = await FileIO.ReadTextAsync(file);
            return JsonSerializer.Deserialize<ConfiguracoesdataObject>(fileContent);
        }



        public static async Task SalvarConfiguracoesAsync(ConfiguracoesdataObject config)
        {
            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await FileIO.WriteTextAsync(file, json);
        }

       
    }
}
