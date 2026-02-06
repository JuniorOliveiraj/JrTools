using JrTools.Dto;
using System; 
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JrTools.Services
{
    public static class MsBuildLocator
    {
        public static List<MsBuildInfo> FindMsBuildVersions()
        {
            var versions = new List<MsBuildInfo>();
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (string.IsNullOrEmpty(programFilesX86) || !Directory.Exists(programFilesX86))
            {
                return versions; 
            }

            string vsBasePath = Path.Combine(programFilesX86, "Microsoft Visual Studio");
            if (!Directory.Exists(vsBasePath)) 
            {
                return versions;
            }

            string[] yearDirectories = Directory.GetDirectories(vsBasePath);

            foreach (var yearDir in yearDirectories)
            {
                string[] editionDirectories = Directory.GetDirectories(yearDir);
                foreach (var editionDir in editionDirectories)
                {
                    string msBuildPath = Path.Combine(editionDir, "MSBuild", "Current", "Bin", "MSBuild.exe");
                    if (File.Exists(msBuildPath))
                    {
                        string version = new DirectoryInfo(yearDir).Name + " (" + new DirectoryInfo(editionDir).Name + ")";
                        versions.Add(new MsBuildInfo { Version = version, Path = msBuildPath });
                        continue; 
                    }

                    string msBuildLegacyPath = Path.Combine(editionDir, "MSBuild", "15.0", "Bin", "MSBuild.exe");
                    if (File.Exists(msBuildLegacyPath))
                    {
                        string version = new DirectoryInfo(yearDir).Name + " (" + new DirectoryInfo(editionDir).Name + ")";
                        versions.Add(new MsBuildInfo { Version = version, Path = msBuildLegacyPath });
                    }
                }
            }

            return versions.OrderByDescending(v => v.Version).ToList();
        }
    }
}
