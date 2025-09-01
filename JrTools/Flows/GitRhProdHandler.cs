using JrTools.Dto;
using JrTools.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    public class GitRhProdHandler
    {
        private readonly GitService _gitService;
        private readonly string _branch;
        public GitRhProdHandler(GitService gitService, string branch)
        {
            _gitService = gitService;
            _branch = branch;

        }

        public async Task<string> ExecutarPullAsync()
        {
            var result = await _gitService.RunCommandAsync(new ProcessStartInfoGitDataobject
            {
                FileName = "git",
                Arguments = "pull",
                WorkingDirectory = @"F:\REACT\MEU SITE\canaa",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            return $"[GIT PULL]\n{result}";
        }

        public async Task<string> ExecutarCheckOutAsync()
        {
            var result = await _gitService.RunCommandAsync(new ProcessStartInfoGitDataobject
            {
                FileName = "git",
                Arguments = $"checkout {_branch}",
                WorkingDirectory = @"F:\REACT\MEU SITE\canaa",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            return $"[CHECKOUT {_branch}]\n{result}";
        }
    }
}
