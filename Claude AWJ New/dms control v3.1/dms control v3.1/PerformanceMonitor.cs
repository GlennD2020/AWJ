using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace dms_control_v3
{
    public class PerformanceMonitor
    {
        private Stopwatch sw = new Stopwatch();
        private List<double> history = new List<double>();

        public void Start()
        {
            sw.Restart();
        }

        public void Stop()
        {
            sw.Stop();
            history.Add(sw.Elapsed.TotalMilliseconds);
            if (history.Count > 20) history.RemoveAt(0);
        }

        public double GetAverageHz()
        {
            return (history.Count == 0) ? 0 : 1000.0 / history.Average();
        }
    }
}