using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace JrTools.Services
{
    /// <summary>
    /// Serviço responsável por encerrar processos
    /// </summary>
    public class ProcessKillerService
    {
        public event EventHandler<ProcessKilledEventArgs>? ProcessKilled;
        public event EventHandler<ProcessKillFailedEventArgs>? ProcessKillFailed;

        /// <summary>
        /// Encerra todos os processos com o nome especificado
        /// </summary>
        public async Task<int> KillProcessesByNameAsync(string processName)
        {
            return await Task.Run(() =>
            {
                int killedCount = 0;
                var processes = Process.GetProcessesByName(processName);

                foreach (var proc in processes)
                {
                    try
                    {
                        int pid = proc.Id;
                        string name = proc.ProcessName;
                        
                        proc.Kill(entireProcessTree: true);
                        proc.WaitForExit(5000); // Wait up to 5 seconds
                        
                        killedCount++;
                        ProcessKilled?.Invoke(this, new ProcessKilledEventArgs(name, pid));
                    }
                    catch (Exception ex)
                    {
                        ProcessKillFailed?.Invoke(this, new ProcessKillFailedEventArgs(
                            proc.ProcessName, 
                            proc.Id, 
                            ex.Message));
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                return killedCount;
            });
        }

        /// <summary>
        /// Encerra um processo específico por PID
        /// </summary>
        public async Task<bool> KillProcessByIdAsync(int pid)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    string name = process.ProcessName;
                    
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                    process.Dispose();
                    
                    ProcessKilled?.Invoke(this, new ProcessKilledEventArgs(name, pid));
                    return true;
                }
                catch (Exception ex)
                {
                    ProcessKillFailed?.Invoke(this, new ProcessKillFailedEventArgs(
                        "Unknown",
                        pid,
                        ex.Message));
                    return false;
                }
            });
        }

        /// <summary>
        /// Verifica se o processo ainda está em execução
        /// </summary>
        public bool IsProcessRunning(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Event args para quando um processo é encerrado com sucesso
    /// </summary>
    public class ProcessKilledEventArgs : EventArgs
    {
        public string ProcessName { get; }
        public int ProcessId { get; }

        public ProcessKilledEventArgs(string processName, int processId)
        {
            ProcessName = processName;
            ProcessId = processId;
        }
    }

    /// <summary>
    /// Event args para quando falha ao encerrar um processo
    /// </summary>
    public class ProcessKillFailedEventArgs : EventArgs
    {
        public string ProcessName { get; }
        public int ProcessId { get; }
        public string ErrorMessage { get; }

        public ProcessKillFailedEventArgs(string processName, int processId, string errorMessage)
        {
            ProcessName = processName;
            ProcessId = processId;
            ErrorMessage = errorMessage;
        }
    }
}
