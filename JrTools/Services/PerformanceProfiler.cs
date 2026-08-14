using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace JrTools.Services
{
    /// <summary>
    /// Profiler para medir performance de operações no mapeamento de caminhos.
    /// </summary>
    public class PerformanceProfiler
    {
        private readonly Dictionary<string, ProfileMetric> _metrics = new();
        private readonly Stopwatch _globalStopwatch = new();

        public class ProfileMetric
        {
            public string Name { get; set; } = string.Empty;
            public long TotalMilliseconds { get; set; }
            public int CallCount { get; set; }
            public long MinMilliseconds { get; set; } = long.MaxValue;
            public long MaxMilliseconds { get; set; }
            public double AverageMilliseconds => CallCount > 0 ? TotalMilliseconds / (double)CallCount : 0;
        }

        public void Start()
        {
            _globalStopwatch.Restart();
        }

        public void Stop()
        {
            _globalStopwatch.Stop();
        }

        public IDisposable Measure(string operationName)
        {
            return new ProfileScope(this, operationName);
        }

        private void RecordMetric(string name, long milliseconds)
        {
            if (!_metrics.ContainsKey(name))
            {
                _metrics[name] = new ProfileMetric { Name = name };
            }

            var metric = _metrics[name];
            metric.TotalMilliseconds += milliseconds;
            metric.CallCount++;
            metric.MinMilliseconds = Math.Min(metric.MinMilliseconds, milliseconds);
            metric.MaxMilliseconds = Math.Max(metric.MaxMilliseconds, milliseconds);
        }

        public string GetReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PERFORMANCE PROFILING REPORT ===");
            sb.AppendLine($"Total Execution Time: {_globalStopwatch.ElapsedMilliseconds:N0} ms");
            sb.AppendLine();
            sb.AppendLine("Operation Breakdown:");
            sb.AppendLine(new string('-', 120));
            sb.AppendLine($"{"Operation",-50} {"Calls",10} {"Total (ms)",15} {"Avg (ms)",15} {"Min (ms)",15} {"Max (ms)",15} {"% Total",10}");
            sb.AppendLine(new string('-', 120));

            var sortedMetrics = _metrics.Values
                .OrderByDescending(m => m.TotalMilliseconds)
                .ToList();

            var totalTime = _globalStopwatch.ElapsedMilliseconds;

            foreach (var metric in sortedMetrics)
            {
                var percentage = totalTime > 0 ? (metric.TotalMilliseconds * 100.0 / totalTime) : 0;
                sb.AppendLine($"{metric.Name,-50} {metric.CallCount,10:N0} {metric.TotalMilliseconds,15:N0} {metric.AverageMilliseconds,15:N2} {metric.MinMilliseconds,15:N0} {metric.MaxMilliseconds,15:N0} {percentage,9:N2}%");
            }

            sb.AppendLine(new string('-', 120));
            sb.AppendLine();
            sb.AppendLine("Top 5 Bottlenecks:");
            foreach (var metric in sortedMetrics.Take(5))
            {
                var percentage = totalTime > 0 ? (metric.TotalMilliseconds * 100.0 / totalTime) : 0;
                sb.AppendLine($"  {metric.Name}: {metric.TotalMilliseconds:N0} ms ({percentage:N2}%) - {metric.CallCount:N0} calls");
            }

            return sb.ToString();
        }

        public Dictionary<string, ProfileMetric> GetMetrics() => _metrics;

        private class ProfileScope : IDisposable
        {
            private readonly PerformanceProfiler _profiler;
            private readonly string _operationName;
            private readonly Stopwatch _stopwatch;

            public ProfileScope(PerformanceProfiler profiler, string operationName)
            {
                _profiler = profiler;
                _operationName = operationName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _profiler.RecordMetric(_operationName, _stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
