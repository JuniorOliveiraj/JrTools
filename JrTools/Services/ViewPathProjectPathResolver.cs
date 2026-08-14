using System;
using System.IO;
using System.Linq;

namespace JrTools.Services
{
    /// <summary>
    /// Resolve a selected project folder to the artifact root that contains Pages, Views, and related WES artifacts.
    /// </summary>
    public static class ViewPathProjectPathResolver
    {
        public static string ResolveArtifactsRoot(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return projectPath;

            if (Directory.Exists(Path.Combine(projectPath, "Pages")))
                return projectPath;

            var knownArtifactsPath = Path.Combine(projectPath, "WES", "WebApp", "Artifacts");
            if (Directory.Exists(Path.Combine(knownArtifactsPath, "Pages")))
                return knownArtifactsPath;

            var directArtifactsPath = Path.Combine(projectPath, "Artifacts");
            if (Directory.Exists(Path.Combine(directArtifactsPath, "Pages")))
                return directArtifactsPath;

            var discoveredArtifactsPath = FindArtifactsDirectoryWithPages(projectPath);
            return discoveredArtifactsPath ?? projectPath;
        }

        private static string? FindArtifactsDirectoryWithPages(string projectPath)
        {
            if (!Directory.Exists(projectPath))
                return null;

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                };

                return Directory
                    .EnumerateDirectories(projectPath, "Artifacts", options)
                    .FirstOrDefault(path => Directory.Exists(Path.Combine(path, "Pages")));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
