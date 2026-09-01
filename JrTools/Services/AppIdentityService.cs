using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace JrTools.Services
{
    /// <summary>
    /// O JrTools roda desempacotado (sem MSIX) — o zip da release é extraído direto, sem
    /// instalador. Sem um pacote MSIX, o Windows só concede identidade de notificação
    /// (AppUserModelID) a um app que tenha um atalho no Menu Iniciar com a propriedade
    /// System.AppUserModel.ID definida; sem essa identidade, AppNotificationManager.Setting
    /// fica "Unsupported" e o app nem aparece em Configurações > Notificações. Esta classe
    /// cria (uma única vez, ou quando o alvo mudou) esse atalho e registra o AUMID no
    /// processo atual, conforme o guia oficial da Microsoft para notificações em apps
    /// desempacotados.
    /// </summary>
    internal static class AppIdentityService
    {
        internal const string Aumid = "JuniorOliveiraj.JrTools";
        private const string NomeAtalho = "JrTools.lnk";

        public static void GarantirIdentidade()
        {
            SetCurrentProcessExplicitAppUserModelID(Aumid);

            try
            {
                GarantirAtalhoNoMenuIniciar();
            }
            catch
            {
                // Sem o atalho, notificações continuam indisponíveis, mas isso não pode
                // derrubar a abertura do app.
            }
        }

        private static void GarantirAtalhoNoMenuIniciar()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            var pastaMenuIniciar = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var caminhoAtalho = Path.Combine(pastaMenuIniciar, NomeAtalho);

            if (File.Exists(caminhoAtalho) && AtalhoJaApontaPara(caminhoAtalho, exePath))
                return;

            CriarAtalhoComAumid(caminhoAtalho, exePath);
        }

        private static bool AtalhoJaApontaPara(string caminhoAtalho, string exePath)
        {
            var shellLink = (IShellLinkW)new ShellLink();
            ((IPersistFile)shellLink).Load(caminhoAtalho, 0 /* STGM_READ */);

            var alvo = new StringBuilder(260);
            shellLink.GetPath(alvo, alvo.Capacity, IntPtr.Zero, 0);

            return string.Equals(alvo.ToString(), exePath, StringComparison.OrdinalIgnoreCase);
        }

        private static void CriarAtalhoComAumid(string caminhoAtalho, string exePath)
        {
            var shellLink = (IShellLinkW)new ShellLink();
            shellLink.SetPath(exePath);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? string.Empty);

            var propertyStore = (IPropertyStore)shellLink;
            using (var aumidValue = PropVariant.FromString(Aumid))
            {
                var chave = PropertyKeys.AppUserModelId;
                propertyStore.SetValue(ref chave, aumidValue);
                propertyStore.Commit();
            }

            ((IPersistFile)shellLink).Save(caminhoAtalho, true);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

        // ── Interop COM clássico (ShellLink) ────────────────────────────────────
        // Não há API gerenciada/WinRT para criar atalhos com AppUserModelID — só via
        // IShellLinkW + IPropertyStore + IPersistFile, o mesmo caminho documentado pela
        // Microsoft para dar identidade de notificação a apps desempacotados.

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey key, [Out] PropVariant pv);
            void SetValue(ref PropertyKey key, [In] PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public int PropertyId;

            public PropertyKey(Guid formatId, int propertyId)
            {
                FormatId = formatId;
                PropertyId = propertyId;
            }
        }

        private static class PropertyKeys
        {
            // PKEY_AppUserModel_ID — {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, 5
            public static PropertyKey AppUserModelId =>
                new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        }

        [StructLayout(LayoutKind.Explicit)]
        private sealed class PropVariant : IDisposable
        {
            [FieldOffset(0)] private ushort vt;
            [FieldOffset(8)] private IntPtr pointerValue;

            public static PropVariant FromString(string valor)
            {
                return new PropVariant
                {
                    vt = 31, // VT_LPWSTR
                    pointerValue = Marshal.StringToCoTaskMemUni(valor)
                };
            }

            public void Dispose()
            {
                PropVariantClear(this);
                GC.SuppressFinalize(this);
            }

            ~PropVariant() => Dispose();

            [DllImport("ole32.dll")]
            private static extern int PropVariantClear([In, Out] PropVariant pvar);
        }
    }
}
