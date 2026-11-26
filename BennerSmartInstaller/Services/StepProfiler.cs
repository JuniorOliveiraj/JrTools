using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BennerSmartInstaller.Services
{
    /// <summary>
    /// Fase 0 - instrumentação: acumula o tempo por etapa e imprime um resumo "[PROFILE]".
    /// Uso: var p = new StepProfiler("INSTALL"); p.Measure("etapa", () => ...); p.PrintSummary();
    /// </summary>
    public class StepProfiler
    {
        private readonly string _title;
        private readonly Stopwatch _total = Stopwatch.StartNew();
        private readonly List<KeyValuePair<string, TimeSpan>> _steps = new List<KeyValuePair<string, TimeSpan>>();

        public StepProfiler(string title)
        {
            _title = title;
        }

        /// <summary>Quando SMARTINSTALL_PROFILE=1, o instalador executa as sub-etapas internas da plataforma separadamente.</summary>
        public static bool DeepMode
        {
            get { return Environment.GetEnvironmentVariable("SMARTINSTALL_PROFILE") == "1"; }
        }

        public void Measure(string step, Action action)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                sw.Stop();
                Add(step, sw.Elapsed);
            }
        }

        public T Measure<T>(string step, Func<T> func)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return func();
            }
            finally
            {
                sw.Stop();
                Add(step, sw.Elapsed);
            }
        }

        public void Add(string step, TimeSpan elapsed)
        {
            lock (_steps)
            {
                _steps.Add(new KeyValuePair<string, TimeSpan>(step, elapsed));
            }
            Console.WriteLine($"[PROFILE] {_title} | {step}: {elapsed.TotalMilliseconds:F0} ms");
        }

        public void PrintSummary()
        {
            _total.Stop();
            double totalMs = _total.Elapsed.TotalMilliseconds;
            Console.WriteLine($"\n[PROFILE] ===== {_title}: resumo (total {totalMs:F0} ms) =====");
            List<KeyValuePair<string, TimeSpan>> copy;
            lock (_steps)
            {
                copy = _steps.ToList();
            }
            foreach (var s in copy.OrderByDescending(x => x.Value))
            {
                double ms = s.Value.TotalMilliseconds;
                double pct = totalMs > 0 ? ms * 100.0 / totalMs : 0;
                Console.WriteLine($"[PROFILE]   {ms,9:F0} ms  {pct,5:F1}%  {s.Key}");
            }
            Console.WriteLine("[PROFILE] ==========================================");
        }
    }
}
