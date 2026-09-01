using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JrTools.ViewModels
{
    public class ProcessViewModel : INotifyPropertyChanged
    {
        private int _count;
        private bool _isEnabled;

        private string _nameDisplay = "";
        public string Name { get; }
        public string NameDisplay
        {
            get => _nameDisplay;
            set { _nameDisplay = value; OnPropertyChanged(); }
        }
        public bool DefaultEnabled { get; }
        public bool IsCustom { get; }

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

        public ProcessViewModel(string name, bool enabledByDefault) : this(name, enabledByDefault, isCustom: false)
        {
        }

        public ProcessViewModel(string name, bool enabledByDefault, bool isCustom)
        {
            Name = name;
            DefaultEnabled = enabledByDefault;
            IsEnabled = DefaultEnabled;
            IsCustom = isCustom;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
