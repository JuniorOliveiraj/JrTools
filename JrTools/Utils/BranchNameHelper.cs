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

            // Remove prefixos tipo sms/ ou smsproducao_
            // Ex: sms/dev-08.05.13/0910239-nome  → dev-08.05.13
            // Ex: smsproducao_08.05/0910239-nome → producao_08.05
            string pattern = @"(?:^sms/?|^smsproducao_)([^/]+)";
            var match = Regex.Match(branchInput, pattern, RegexOptions.IgnoreCase);

            string branchFinal;
            if (match.Success)
            {
                branchFinal = match.Groups[1].Value;
            }
            else
            {
                // Caso já venha normalizada (ex: producao_08.05 ou dev-08.05.13)
                branchFinal = branchInput.Split('/')[0];
            }

            // Detecta se é produção
            bool isProducao = branchFinal.StartsWith("producao", StringComparison.OrdinalIgnoreCase);

            return new BranchInfo
            {
                Producao = isProducao,
                Branch = branchFinal
            };
        }
    }
}
