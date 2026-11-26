using System;

namespace JrTools.Enums
{
    /// <summary>
    /// Representa as camadas padrão de governança da Benner.
    /// </summary>
    public enum CamadaArtefato
    {
        Desconhecida = 0,
        Camada10 = 10, // Builder
        Camada15 = 15, // Tecnologia
        Camada20 = 20, // Benner (Produto RH)
        Camada30 = 30, // Vertical
        Camada40 = 40, // Específico / Customização
        Camada50 = 50, // Cliente
        Camada60 = 60  // Usuário
    }

    public static class CamadaArtefatoExtensions
    {
        public static string ToDescricao(this CamadaArtefato camada) => camada switch
        {
            CamadaArtefato.Camada10 => "Camada 10 (Builder)",
            CamadaArtefato.Camada15 => "Camada 15 (Tecnologia)",
            CamadaArtefato.Camada20 => "Camada 20 (Benner)",
            CamadaArtefato.Camada30 => "Camada 30 (Vertical)",
            CamadaArtefato.Camada40 => "Camada 40 (Específico)",
            CamadaArtefato.Camada50 => "Camada 50 (Cliente)",
            CamadaArtefato.Camada60 => "Camada 60 (Usuário)",
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
                    15 => CamadaArtefato.Camada15,
                    20 => CamadaArtefato.Camada20,
                    30 => CamadaArtefato.Camada30,
                    40 => CamadaArtefato.Camada40,
                    50 => CamadaArtefato.Camada50,
                    60 => CamadaArtefato.Camada60,
                    _ => CamadaArtefato.Desconhecida
                };
            }
            return CamadaArtefato.Desconhecida;
        }
    }
}

