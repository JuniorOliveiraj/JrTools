using System.ComponentModel;
using Windows.UI;

namespace JrTools.Dto
{
    public enum StatusServidor { Desconhecido, Verificando, Online, Offline }

    public class ServidorStatusDto : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Nome { get; set; } = string.Empty;

        private StatusServidor _status = StatusServidor.Desconhecido;
        public StatusServidor Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CorStatus)));
            }
        }

        public Color CorStatus => Status switch
        {
            StatusServidor.Online      => Color.FromArgb(255, 34,  197,  94),  // verde
            StatusServidor.Offline     => Color.FromArgb(255, 239,  68,  68),  // vermelho
            StatusServidor.Verificando => Color.FromArgb(255, 245, 158,  11),  // amarelo
            _                          => Color.FromArgb(255, 107, 114, 128)   // cinza
        };
    }
}
