using System;
using System.IO;
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

            // Sem isso, qualquer exceção não tratada em qualquer thread (inclusive
            // dentro de async void) derruba o processo inteiro sem log nem aviso.
            this.UnhandledException += App_UnhandledException;
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };

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

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception, "Application.UnhandledException");
            e.Handled = true;
        }

        /// <summary>
        /// Grava no mesmo crash.log usado para exceções não tratadas. Exposto para serviços
        /// como <see cref="UpdateService"/> registrarem falhas que eles mesmos escolhem engolir
        /// (ex.: checagem de atualização sem rede) — sem isso, essas falhas não deixavam
        /// nenhum rastro, dificultando diagnosticar por que uma atualização não foi detectada.
        /// </summary>
        internal static void LogCrash(Exception ex, string origem)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "JrTools");
                Directory.CreateDirectory(folder);
                var linha = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({origem}) {ex}\n{new string('-', 80)}\n";
                File.AppendAllText(Path.Combine(folder, "crash.log"), linha);
            }
            catch
            {
                // Se nem o log der certo, não há nada a fazer — mas não deixa isso derrubar o app.
            }
            System.Diagnostics.Debug.WriteLine($"[{origem}] {ex}");
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

            // Checa atualização uma única vez, na abertura do app — nenhuma outra ação
            // dentro da sessão dispara uma nova checagem.
            _ = VerificarAtualizacaoNaAberturaAsync();
        }

        private async Task VerificarAtualizacaoNaAberturaAsync()
        {
            try
            {
                var atualizacao = await new UpdateService().VerificarAsync();
                // Referência qualificada pra não colidir com a propriedade estática
                // MainWindow (mesmo identificador, tipo diferente) desta classe.
                if (atualizacao != null && _window is JrTools.MainWindow mainWindow)
                {
                    mainWindow.DispatcherQueue.TryEnqueue(() => mainWindow.ExibirAtualizacaoDisponivel(atualizacao));
                }
            }
            catch (Exception ex)
            {
                LogCrash(ex, "VerificarAtualizacaoNaAberturaAsync");
            }
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
