using JrTools.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace JrTools.Services
{
    /// <summary>
    /// Serviço de monitoramento de processos usando WMI Events (event-driven)
    /// Muito mais eficiente que polling tradicional
    /// </summary>
    public class ProcessMonitorService : IDisposable
    {
        private readonly Dictionary<string, ManagementEventWatcher> _startWatchers = new();
        private readonly Dictionary<string, ManagementEventWatcher> _stopWatchers = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _disposed = false;

        public event EventHandler<ProcessEventArgs>? ProcessStarted;
        public event EventHandler<ProcessEventArgs>? ProcessStopped;

        /// <summary>
        /// Inicia monitoramento event-driven de um processo específico
        /// </summary>
        public async Task StartMonitoringAsync(string processName)
        {
            await _lock.WaitAsync();
            try
            {
                if (_startWatchers.ContainsKey(processName))
                {
                    // Já está sendo monitorado
                    return;
                }

                await Task.Run(() =>
                {
                    // WMI Query para detectar CRIAÇÃO de processos
                    var startQuery = new WqlEventQuery(
                        "__InstanceCreationEvent",
                        TimeSpan.FromSeconds(1),
                        $"TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{processName}.exe'"
                    );

                    var startWatcher = new ManagementEventWatcher(startQuery);
                    startWatcher.EventArrived += (s, e) =>
                    {
                        try
                        {
                            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                            int pid = Convert.ToInt32(process["ProcessId"]);
                            ProcessStarted?.Invoke(this, new ProcessEventArgs(processName, pid));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error in ProcessStarted event: {ex.Message}");
                        }
                    };
                    startWatcher.Start();
                    _startWatchers[processName] = startWatcher;

                    // WMI Query para detectar TÉRMINO de processos
                    var stopQuery = new WqlEventQuery(
                        "__InstanceDeletionEvent",
                        TimeSpan.FromSeconds(1),
                        $"TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{processName}.exe'"
                    );

                    var stopWatcher = new ManagementEventWatcher(stopQuery);
                    stopWatcher.EventArrived += (s, e) =>
                    {
                        try
                        {
                            var process = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                            int pid = Convert.ToInt32(process["ProcessId"]);
                            ProcessStopped?.Invoke(this, new ProcessEventArgs(processName, pid));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error in ProcessStopped event: {ex.Message}");
                        }
                    };
                    stopWatcher.Start();
                    _stopWatchers[processName] = stopWatcher;
                });
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Para o monitoramento de um processo específico
        /// </summary>
        public async Task StopMonitoringAsync(string processName)
        {
            await _lock.WaitAsync();
            try
            {
                if (_startWatchers.TryGetValue(processName, out var startWatcher))
                {
                    startWatcher.Stop();
                    startWatcher.Dispose();
                    _startWatchers.Remove(processName);
                }

                if (_stopWatchers.TryGetValue(processName, out var stopWatcher))
                {
                    stopWatcher.Stop();
                    stopWatcher.Dispose();
                    _stopWatchers.Remove(processName);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Obtém a contagem atual de processos com o nome especificado
        /// </summary>
        public int GetProcessCount(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName).Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Obtém todos os processos com o nome especificado
        /// </summary>
        public Process[] GetProcesses(string processName)
        {
            try
            {
                return Process.GetProcessesByName(processName);
            }
            catch
            {
                return Array.Empty<Process>();
            }
        }

        /// <summary>
        /// Obtém informações detalhadas de um processo específico por PID
        /// </summary>
        public ProcessInfo? GetProcessInfo(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                var commandLine = GetCommandLine(pid);

                return new ProcessInfo
                {
                    PID = process.Id,
                    ProcessName = process.ProcessName,
                    CommandLine = commandLine ?? "N/A",
                    MachineName = Environment.MachineName,
                    TotalProcessorTime = process.TotalProcessorTime,
                    WorkingSetBytes = process.WorkingSet64,
                    StartTime = process.StartTime
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Obtém a linha de comando de um processo usando WMI
        /// </summary>
        private static string? GetCommandLine(int pid)
        {
            try
            {
                string query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}";
                using var searcher = new ManagementObjectSearcher(query);
                using var results = searcher.Get();
                var mo = results.Cast<ManagementObject>().FirstOrDefault();
                return mo?["CommandLine"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var watcher in _startWatchers.Values)
            {
                watcher.Stop();
                watcher.Dispose();
            }
            _startWatchers.Clear();

            foreach (var watcher in _stopWatchers.Values)
            {
                watcher.Stop();
                watcher.Dispose();
            }
            _stopWatchers.Clear();

            _lock.Dispose();
        }
    }
}
