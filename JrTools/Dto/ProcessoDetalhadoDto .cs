using System;
using System.ComponentModel;

namespace JrTools.Dto
{
    // DTO para representar uma instância de processo em execução
    public class ProcessoDetalhadoDto : INotifyPropertyChanged
    {
        private double _memoriaRamMB;
        private string _tempoCPU;
        private string _logCompleto;

        public int PID { get; set; }
        public string NomeProcesso { get; set; }
        public string Host { get; set; }
        public string CaminhoExecucao { get; set; }
        public string VersaoArquivo { get; set; }
        public string LinhaComando { get; set; }
        public string HoraInicio { get; set; }

        public string LogCompleto
        {
            get => _logCompleto;
            set
            {
                if (_logCompleto != value)
                {
                    _logCompleto = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogCompleto)));
                }
            }
        }

        public string TempoCPU
        {
            get => _tempoCPU;
            set
            {
                if (_tempoCPU != value)
                {
                    _tempoCPU = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TempoCPU)));
                }
            }
        }

        public double MemoriaRamMB
        {
            get => _memoriaRamMB;
            set
            {
                if (Math.Abs(_memoriaRamMB - value) > 0.01)
                {
                    _memoriaRamMB = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MemoriaRamMB)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    // DTO para representar uma conexão de rede (mantido para compatibilidade)
    public class ConexaoRedeDto
    {
        public string Protocolo { get; set; }
        public string EnderecoLocal { get; set; }
        public string PortaLocal { get; set; }
        public string EnderecoRemoto { get; set; }
        public string PortaRemota { get; set; }
        public string Estado { get; set; }
    }
}