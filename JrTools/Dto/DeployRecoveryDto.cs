using JrTools.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JrTools.Dto
{
    public class DeployRecoveryDto : INotifyPropertyChanged
    {
        private bool _isVisible;

        public DateTime BuildFinishedAt { get; set; }
        public ObservableCollection<ProviderRecoveryResult> Results { get; set; } = new();

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        public string Titulo => $"Pós-deploy — build finalizado às {BuildFinishedAt:HH:mm:ss}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
