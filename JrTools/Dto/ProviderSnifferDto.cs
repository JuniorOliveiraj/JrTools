using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JrTools.Dto
{
    public class ProviderInfoItem : INotifyPropertyChanged
    {
        private string _value = "";

        public string Key { get; set; } = "";
        public string Description { get; set; } = "";
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ProviderSnapshot
    {
        public int Pid { get; set; }
        public List<ProviderInfoItem> InfoItems { get; set; } = new();
        public string LogText { get; set; } = "";
    }

    public enum ProviderLogType
    {
        BDebugAll = 0,
        BDebugSlice = 1,
        ProviderInfo = 2
    }
}
