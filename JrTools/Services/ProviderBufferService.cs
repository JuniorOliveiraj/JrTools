using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace JrTools.Services
{
    /// <summary>
    /// Lê os buffers de memória compartilhada escritos pelos providers (BPrv230, etc.)
    /// Porta limpa do BDebbuggerReader do ProviderSniffer legado para .NET 8
    /// </summary>
    public class ProviderBufferService : IDisposable
    {
        #region P/Invoke

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess,
            uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint FILE_MAP_ALL_ACCESS = 0x000F001F;
        private const int MAP_SIZE = 128 * 1024;

        #endregion

        /// <summary>
        /// Lê o buffer de um provider e retorna o texto bruto.
        /// </summary>
        public string ReadRaw(int pid, ProviderLogType logType)
        {
            string bufferName = GetBufferName(pid, logType);
            string mapName = $"Global\\MAP_{bufferName}";
            string semName = $"Global\\SEM_{bufferName}";

            IntPtr mapHandle = OpenFileMapping(FILE_MAP_ALL_ACCESS, false, mapName);
            if (mapHandle == IntPtr.Zero) return string.Empty;

            IntPtr mapBuffer = IntPtr.Zero;
            try
            {
                mapBuffer = MapViewOfFile(mapHandle, FILE_MAP_ALL_ACCESS, 0, 0, MAP_SIZE);
                if (mapBuffer == IntPtr.Zero) return string.Empty;

                return ReadCircularBuffer(mapBuffer, semName);
            }
            finally
            {
                if (mapBuffer != IntPtr.Zero) UnmapViewOfFile(mapBuffer);
                CloseHandle(mapHandle);
            }
        }

        /// <summary>
        /// Lê e parseia o ProviderInfo de um provider, retornando lista de itens chave/desc/valor.
        /// </summary>
        public List<ProviderInfoItem> ReadProviderInfo(int pid)
        {
            string raw = ReadRaw(pid, ProviderLogType.ProviderInfo);
            return ParseProviderInfo(raw);
        }

        /// <summary>
        /// Retorna um snapshot completo: info + log de debug.
        /// </summary>
        public ProviderSnapshot ReadSnapshot(int pid, ProviderLogType logType = ProviderLogType.BDebugAll)
        {
            return new ProviderSnapshot
            {
                Pid = pid,
                InfoItems = ReadProviderInfo(pid),
                LogText = ReadRaw(pid, logType).TrimEnd()
            };
        }

        private static string GetBufferName(int pid, ProviderLogType logType) => logType switch
        {
            ProviderLogType.BDebugAll => $"ProviderBDebuggerBuffer{pid}",
            ProviderLogType.BDebugSlice => $"ProviderBDebuggerWebSliceBuffer{pid}",
            ProviderLogType.ProviderInfo => $"ProviderBProviderInfoBuffer{pid}",
            _ => throw new ArgumentException("Tipo de log não suportado")
        };

        private static string ReadCircularBuffer(IntPtr mapBuffer, string semName)
        {
            bool createdNew = false;
            Semaphore? semaphore = new Semaphore(1, 1, semName, out createdNew);
            if (createdNew) { semaphore.Close(); semaphore = null; }

            if (semaphore != null && !semaphore.WaitOne(500, true))
                return string.Empty;

            try
            {
                uint size = (uint)Marshal.ReadInt32(mapBuffer, 0);
                uint start = (uint)Marshal.ReadInt32(mapBuffer, 4);
                uint end = (uint)Marshal.ReadInt32(mapBuffer, 8);
                IntPtr buffer = new IntPtr(mapBuffer.ToInt64() + 12);

                if (size == 0) return string.Empty;

                var sb = new StringBuilder();
                while (true)
                {
                    if (start == 0) break;
                    sb.Append((char)Marshal.ReadByte(buffer, (int)(start - 1)));
                    if (start == end) { start = 0; end = 0; }
                    else start = (start % size) + 1;
                }

                // Remove line-breaks iniciais
                while (sb.Length > 0 && (sb[0] == '\r' || sb[0] == '\n'))
                    sb.Remove(0, 1);

                return sb.ToString();
            }
            finally
            {
                semaphore?.Release();
                semaphore?.Close();
            }
        }

        private static List<ProviderInfoItem> ParseProviderInfo(string raw)
        {
            var result = new List<ProviderInfoItem>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (var line in raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                int p1 = line.IndexOf('=');
                if (p1 < 0) continue;
                int p2 = line.IndexOf('|', p1);
                if (p2 < 0) continue;

                result.Add(new ProviderInfoItem
                {
                    Key = line[..p1],
                    Description = line[(p1 + 1)..p2],
                    Value = line[(p2 + 1)..]
                });
            }
            return result;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
