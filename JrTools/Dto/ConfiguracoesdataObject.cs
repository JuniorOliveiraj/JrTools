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

        // Subir Ambiente Manual
        public string? UltimaPastaAmbiente { get; set; }
        public List<string> HistoricoPastasAmbiente { get; set; } = new();
    }
}
