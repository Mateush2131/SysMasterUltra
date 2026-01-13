using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SysMasterUltra.Services
{
    public class PerformanceMonitor : IDisposable
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;
        private bool initialized = false;

        public PerformanceMonitor()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                initialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize performance counters", ex);
            }
        }

        public float GetCpuUsage()
        {
            if (!initialized) return 0;
            try
            {
                return cpuCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        public float GetRamUsage()
        {
            if (!initialized) return 0;
            try
            {
                return ramCounter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        public int GetProcessCount()
        {
            try
            {
                return Process.GetProcesses().Length;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            cpuCounter?.Dispose();
            ramCounter?.Dispose();
        }
    }
}