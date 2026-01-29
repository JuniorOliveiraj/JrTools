using JrTools.Dto;
using JrTools.Services;
using JrTools.Workflows;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace JrTools.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class CopyViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private static CopyViewModel _instance;
        private static readonly object _lock = new object();

        public static CopyViewModel Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CopyViewModel();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly CopyProfileService _profileService;
        private PastaEspelhador _espelhador;
        private CancellationTokenSource _cts;
        private DispatcherQueue _dispatcher;

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }

        public ObservableCollection<PerfilEspelhamento> Perfis { get; } = new();

        private PerfilEspelhamento _selectedPerfil;
        public PerfilEspelhamento SelectedPerfil
        {
            get => _selectedPerfil;
            set
            {
                if (_selectedPerfil == value) return;
                _selectedPerfil = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleMirroringCommand).RaiseCanExecuteChanged();
            }
        }

        private bool _isMirroring;
        public bool IsMirroring
        {
            get => _isMirroring;
            set
            {
                _isMirroring = value;
                OnPropertyChanged();
                ((RelayCommand)ToggleMirroringCommand).RaiseCanExecuteChanged();
            }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; OnPropertyChanged(); }
        }

        private string _statusText = "Pronto";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private string _logs;
        public string Logs
        {
            get => _logs;
            set { _logs = value; OnPropertyChanged(); }
        }

        public ICommand ToggleMirroringCommand { get; }
        public ICommand AddProfileCommand { get; }
        public ICommand RemoveProfileCommand { get; }

        private CopyViewModel()
        {
            _profileService = new CopyProfileService();
            
            ToggleMirroringCommand = new RelayCommand(async () => await ToggleMirroringAsync(), () => SelectedPerfil != null);
            // Comandos Add/Remove seriam conectados aqui na prática, simplificado para o exemplo
        }

        public void InitializeDispatcher()
        {
            if (_dispatcher == null)
            {
                _dispatcher = DispatcherQueue.GetForCurrentThread();
            }
        }

        public async Task LoadProfilesAsync()
        {
            var loaded = await _profileService.CarregarPerfisAsync();
            Perfis.Clear();
            foreach (var p in loaded) Perfis.Add(p);
        }

        public async Task AddProfileAsync(PerfilEspelhamento perfil)
        {
            Perfis.Add(perfil);
            await SaveProfilesAsync();
        }

        public async Task RemoveProfileAsync(PerfilEspelhamento perfil)
        {
            if (perfil != null && Perfis.Contains(perfil))
            {
                Perfis.Remove(perfil);
                await SaveProfilesAsync();
            }
        }

        private async Task SaveProfilesAsync()
        {
            await _profileService.SalvarPerfisAsync(Perfis.ToList());
        }

        private async Task ToggleMirroringAsync()
        {
            if (IsMirroring)
            {
                await PararEspelhamentoAsync();
            }
            else
            {
                await IniciarEspelhamentoAsync();
            }
        }

        private async Task IniciarEspelhamentoAsync()
        {
            if (SelectedPerfil == null) return;
            IsMirroring = true;
            StatusText = "Iniciando...";
            ProgressValue = 0;
            Logs = "";

            _cts = new CancellationTokenSource();
            _espelhador = new PastaEspelhador();

            var progresso = new Progress<ProgressoEspelhamento>(p =>
            {
                // Safely update UI, even if dispatcher changes during navigation
                var currentDispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
                if (currentDispatcher != null)
                {
                    currentDispatcher.TryEnqueue(() =>
                    {
                        ProgressValue = p.Percentual;
                        StatusText = p.Status;
                        if (!string.IsNullOrEmpty(p.Mensagem))
                            Logs += $"[{DateTime.Now:HH:mm:ss}] {p.Mensagem}\n";
                    });
                }
            });

            try
            {
                // Run the heavy I/O work on a background thread
                await Task.Run(async () =>
                {
                    await _espelhador.IniciarEspelhamentoAsync(
                        SelectedPerfil.DiretorioOrigem,
                        SelectedPerfil.DiretorioDestino,
                        progresso,
                        _cts.Token);
                }, _cts.Token);
                
                // Update UI when complete
                var dispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
                dispatcher?.TryEnqueue(() =>
                {
                    StatusText = "Espelhamento concluído!";
                    IsMirroring = false;
                });
            }
            catch (OperationCanceledException)
            {
                var dispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
                dispatcher?.TryEnqueue(() =>
                {
                    StatusText = "Espelhamento cancelado.";
                    IsMirroring = false;
                });
            }
            catch (Exception ex)
            {
                var dispatcher = _dispatcher ?? DispatcherQueue.GetForCurrentThread();
                dispatcher?.TryEnqueue(() =>
                {
                    StatusText = "Erro: " + ex.Message;
                    Logs += $"[{DateTime.Now:HH:mm:ss}] ERRO: {ex.Message}\n";
                    IsMirroring = false;
                });
            }
        }

        private async Task PararEspelhamentoAsync()
        {
            StatusText = "Parando...";
            if (_espelhador != null) await _espelhador.PararAsync();
            IsMirroring = false;
            StatusText = "Parado pelo usuário.";
        }
    }
}
