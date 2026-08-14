using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using JrTools.Services;
using System.Threading.Tasks;

namespace JrTools
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        public static Window MainWindow { get; private set; }
        private DispatcherQueueTimer? _notificationTimer;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            
            // Registra o gerenciador de notificações ao iniciar
            try 
            {
                AppNotificationManager.Default.Register();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao registrar notificações: {ex.Message}");
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();

            StartNotificationTimer();
        }

        private void StartNotificationTimer()
        {
            // Cria um timer para verificar as horas a cada 5 minutos
            _notificationTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _notificationTimer.Interval = TimeSpan.FromMinutes(5);
            _notificationTimer.Tick += async (s, e) =>
            {
                await NotificationService.Instance.CheckAndNotifyTogglHoursAsync();
            };
            _notificationTimer.Start();

            // Primeira execução imediata em background
            Task.Run(async () => await NotificationService.Instance.CheckAndNotifyTogglHoursAsync());
        }
    }
}
