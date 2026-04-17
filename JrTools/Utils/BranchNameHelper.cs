using JrTools.Dto;
using System;
using System.Text.RegularExpressions;

namespace JrTools.Utils
{
    public class BranchNameHelper
    {
        public BranchInfo ObterBranchInfo(string branchInput)
        {
            if (string.IsNullOrWhiteSpace(branchInput))
                throw new ArgumentException("Branch inválida", nameof(branchInput));

            // Novo padrão Azure DevOps:
            // Ex: sms/dev/09.00.00  → dev/09.00.00
            // Ex: sms/prd/09.00     → prd/09.00
            // Ex: dev/09.00.00      → dev/09.00.00
            // Ex: prd/09.00         → prd/09.00
            string branchFinal;
            var match = Regex.Match(branchInput, @"^sms/(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                branchFinal = match.Groups[1].Value;
            }
            else
            {
                branchFinal = branchInput;
            }

            // Detecta se é produção pelo prefixo prd/
            bool isProducao = branchFinal.StartsWith("prd/", StringComparison.OrdinalIgnoreCase)
                           || branchFinal.Equals("prd", StringComparison.OrdinalIgnoreCase);

            return new BranchInfo
            {
                Producao = isProducao,
                Branch = branchFinal
            };
        }
    }
}
