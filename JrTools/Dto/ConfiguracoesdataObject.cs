using System.Collections.Generic;

namespace JrTools.Dto
{
    public class ConfiguracoesdataObject
    {
        public string? ProjetoSelecionado { get; set; }
        public string DiretorioBinarios { get; set; }
        public string DiretorioProducao { get; set; }
        public string DiretorioEspecificos { get; set; }
        public string MsBuildPadraoPath { get; set; }
        public string CaminhoCSReportImport { get; set; }
            = @"D:\Benner\Servicos\ReportKeeper.V1\CSReportImport.exe";
        public string WesExePath { get; set; }
            = @"D:\Benner\fontes\rh\prod\WES\WebApp\Bin\wes.exe";

        public bool NotificarHorasToggl { get; set; } = true;
        public string? PoolIisPadrao { get; set; }

        // Subir Ambiente Manual
        public string? UltimaPastaAmbiente { get; set; }
        public List<string> HistoricoPastasAmbiente { get; set; } = new();
        public string? UltimaBranchAmbiente { get; set; }

        // Branches editáveis (compartilhado entre RhProd e SubirAmbienteManual)
        public List<string> ListaBranches { get; set; } = new()
        {
            "prd/09.00", "prd/08.06", "prd/08.05", "prd/08.04",
            "dev/09.00.00", "dev/08.06.00", "dev/08.05.00", "dev/08.04.00",
            "Outro"
        };
    }
}
