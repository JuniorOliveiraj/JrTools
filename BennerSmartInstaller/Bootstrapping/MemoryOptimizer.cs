using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BennerSmartInstaller.Bootstrapping
{
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);

                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch
            {
                // Ignora falhas de trimming em ambientes com permissão restrita
            }
        }
    }
}

