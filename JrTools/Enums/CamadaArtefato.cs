using System;

namespace JrTools.Enums
{
    /// <summary>
    /// Representa as camadas padrão de governança da Benner.
    /// </summary>
    public enum CamadaArtefato
    {
        Desconhecida = 0,
        Camada10 = 10,
        Camada20 = 20, // Produto (Benner RH)
        Camada30 = 30, // Vertical
        Camada40 = 40, // Específico / Customização
        Camada50 = 50  // Cliente
    }

    public static class CamadaArtefatoExtensions
    {
        public static string ToDescricao(this CamadaArtefato camada) => camada switch
        {
            CamadaArtefato.Camada10 => "Camada 10",
            CamadaArtefato.Camada20 => "Camada 20 (Produto)",
            CamadaArtefato.Camada30 => "Camada 30 (Vertical)",
            CamadaArtefato.Camada40 => "Camada 40 (Específico)",
            CamadaArtefato.Camada50 => "Camada 50 (Cliente)",
            _ => "Camada Desconhecida"
        };

        public static CamadaArtefato ParseCamada(string? camadaStr)
        {
            if (string.IsNullOrWhiteSpace(camadaStr)) return CamadaArtefato.Camada20;
            if (int.TryParse(camadaStr.Trim(), out int val))
            {
                return val switch
                {
                    10 => CamadaArtefato.Camada10,
                    20 => CamadaArtefato.Camada20,
                    30 => CamadaArtefato.Camada30,
                    40 => CamadaArtefato.Camada40,
                    50 => CamadaArtefato.Camada50,
                    _ => CamadaArtefato.Desconhecida
                };
            }
            return CamadaArtefato.Desconhecida;
        }
    }
}
