// Em um novo arquivo: Services/ProcessInspectorService.cs
using JrTools.Dto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public static class ProcessInspectorService
    {
        // --- Estruturas e Constantes para a API do Windows (Iphlpapi.dll) ---

        private const int AF_INET = 2; // IPv4
        private const int AF_INET6 = 23; // IPv6

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] remotePort;
            public uint owningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int TableClass, uint Reserved);

        // --- Métodos do Serviço ---

        public static Task<List<ProcessoDetalhadoDto>> GetDetailedProcessesByNameAsync(string processName)
        {
            return Task.Run(() =>
            {
                var list = new List<ProcessoDetalhadoDto>();
                var processes = Process.GetProcessesByName(processName);

                foreach (var p in processes)
                {
                    try
                    {
                        list.Add(new ProcessoDetalhadoDto
                        {
                            PID = p.Id,
                            CaminhoExecucao = p.MainModule?.FileName ?? "Acesso negado",
                            MemoriaRamMB = Math.Round(p.WorkingSet64 / 1024.0 / 1024.0, 2),
                            HoraInicio = p.StartTime.ToString("HH:mm:ss")
                        });
                    }
                    catch (Exception)
                    {
                        // Ignora processos que não podem ser acessados (ex: processos de sistema)
                    }
                }
                return list.OrderBy(p => p.PID).ToList();
            });
        }

        public static Task<List<ConexaoRedeDto>> GetNetworkConnectionsForProcessAsync(int processId)
        {
            return Task.Run(() =>
            {
                var connections = new List<ConexaoRedeDto>();
                int bufferSize = 0;
                // Obter o tamanho do buffer necessário
                GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, 5, 0);

                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    if (GetExtendedTcpTable(buffer, ref bufferSize, true, AF_INET, 5, 0) == 0)
                    {
                        int rowCount = Marshal.ReadInt32(buffer);
                        IntPtr tablePtr = IntPtr.Add(buffer, sizeof(int));

                        for (int i = 0; i < rowCount; i++)
                        {
                            var row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(tablePtr, typeof(MIB_TCPROW_OWNER_PID));
                            if (row.owningPid == processId)
                            {
                                connections.Add(new ConexaoRedeDto
                                {
                                    Protocolo = "TCP",
                                    EnderecoLocal = new IPAddress(row.localAddr).ToString(),
                                    PortaLocal = BitConverter.ToUInt16(new byte[2] { row.localPort[1], row.localPort[0] }, 0).ToString(),
                                    EnderecoRemoto = new IPAddress(row.remoteAddr).ToString(),
                                    PortaRemota = BitConverter.ToUInt16(new byte[2] { row.remotePort[1], row.remotePort[0] }, 0).ToString(),
                                    Estado = ((TcpState)row.state).ToString()
                                });
                            }
                            tablePtr = IntPtr.Add(tablePtr, Marshal.SizeOf<MIB_TCPROW_OWNER_PID>());
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                return connections;
            });
        }

        private enum TcpState
        {
            Closed = 1, Listen = 2, Syn_Sent = 3, Syn_Rcvd = 4,
            Established = 5, Fin_Wait1 = 6, Fin_Wait2 = 7, Close_Wait = 8,
            Closing = 9, Last_Ack = 10, Time_Wait = 11, Delete_TCB = 12
        }
    }
}