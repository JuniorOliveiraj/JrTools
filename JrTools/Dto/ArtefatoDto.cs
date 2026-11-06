using System;
using JrTools.Enums;

namespace JrTools.Dto
{
    public class ArtefatoDto
    {
        public string Identificador { get; set; } = string.Empty;
        public string NomeArquivo { get; set; } = string.Empty;
        public TipoArtefato Tipo { get; set; } = TipoArtefato.Outro;
        public CamadaArtefato Camada { get; set; } = CamadaArtefato.Camada20;
        public string CaminhoCompleto { get; set; } = string.Empty;
        public StatusArtefato Status { get; set; } = StatusArtefato.NaoVerificado;
        public bool IsSelecionado { get; set; }

        public bool IsPendente => Status.IsPendente();
        public string StatusFormatado => Status.ToDescricao();

        // Propriedades amigáveis para bindings XAML existentes
        public string Guia => Tipo.ToNomePasta();
        public string TipoDescricao => Tipo.ToDescricao();
        public string CamadaFormatada => $"Camada {(int)Camada}";
        public string DescricaoCompleta => $"{Identificador} [{Guia}] [{StatusFormatado}] ({CamadaFormatada})";
    }
}
