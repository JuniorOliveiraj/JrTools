using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace JrTools
{
    public class Program
    {
        [System.STAThread]
        static void Main(string[] args)
        {
            // Modo headless: consultado como processo filho para listar sistemas do BServer
            // sem travar DLLs no processo principal.
            if (args.Length >= 3 && args[0] == "--bserver")
            {
                Services.BServerWorker.Executar(args[1], args[2]);
                return;
            }

            // Precisa rodar antes do AppNotificationManager.Default.Register() (chamado no
            // construtor de App) — sem uma identidade AUMID registrada, notificações ficam
            // com Setting = Unsupported e o app nem aparece em Configurações > Notificações.
            Services.AppIdentityService.GarantirIdentidade();

            ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
    }
}
