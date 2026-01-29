using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JrTools.ViewModels
{
    public class ProcessViewModel : INotifyPropertyChanged
    {
        private int _count;
        private bool _isEnabled;

        public string Name { get; }
        public string NameDisplay { get; set; } = "";
        public bool DefaultEnabled { get; }

        public int Count
        {
            get => _count;
            set { _count = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public ProcessViewModel(string name, bool enabledByDefault)
        {
            Name = name;
            DefaultEnabled = enabledByDefault;
            IsEnabled = DefaultEnabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
