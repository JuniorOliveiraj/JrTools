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
