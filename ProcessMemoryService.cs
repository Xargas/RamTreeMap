using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RamTreeMap
{
    /// <summary>
    /// Service to gather memory information from running processes.
    /// </summary>
    public class ProcessMemoryService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        /// <summary>
        /// Gets system memory statistics using Performance Counters.
        /// Returns tuple of (totalMemory, usedMemory, freeMemory, standbyMemory).
        /// These values match Windows Task Manager display.
        /// </summary>
        public (long totalMemory, long usedMemory, long freeMemory, long standbyMemory) GetSystemMemoryStats()
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                long totalMemory = (long)memStatus.ullTotalPhys;

                try
                {
                    // Query all standby/cached memory counters that sum to the standby total
                    long standbyMemory = 0;

                    var counterNames = new[]
                    {
                        "Cache Bytes",
                        "Modified page list bytes",
                        "Standby Cache Core Bytes",
                        "Standby Cache normal Priority Bytes",
                        "Standby Cache Reserve Bytes"
                    };

                    foreach (var counterName in counterNames)
                    {
                        try
                        {
                            var counter = new PerformanceCounter("Memory", counterName, null, true);
                            standbyMemory += (long)counter.NextValue();
                            counter.Dispose();
                        }
                        catch
                        {
                            // Skip counters that don't exist or can't be read
                        }
                    }

                    // Get available memory (free + standby)
                    var availableCounter = new PerformanceCounter("Memory", "Available Bytes", null, true);
                    long availableBytes = (long)availableCounter.NextValue();
                    availableCounter.Dispose();

                    // Free memory = Available - Standby (truly free, not cached)
                    long freeMemory = availableBytes - standbyMemory;
                    if (freeMemory < 0) freeMemory = 0;

                    // Used memory = Total - Available
                    long usedMemory = totalMemory - availableBytes;
                    if (usedMemory < 0) usedMemory = 0;

                    return (totalMemory, usedMemory, freeMemory, standbyMemory);
                }
                catch
                {
                    // Fall back to simple calculation if Performance Counters unavailable
                    long availableMemory = (long)memStatus.ullAvailPhys;
                    long usedMemory = totalMemory - availableMemory;

                    return (totalMemory, usedMemory, availableMemory, 0);
                }
            }

            return (0, 0, 0, 0);
        }

        /// <summary>
        /// Gathers memory information for all running processes asynchronously.
        /// Returns a dictionary with PID as key and ProcessMemoryInfo as value.
        /// </summary>
        public async Task<Dictionary<int, ProcessMemoryInfo>> GetRunningProcessesAsync()
        {
            return await Task.Run(() =>
            {
                var processDictionary = new Dictionary<int, ProcessMemoryInfo>();
                var processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    try
                    {
                        int processId = process.Id;
                        string appName = process.ProcessName;
                        long workingSetMemory = process.WorkingSet64;
                        var workingSetMemoryPrivate = new PerformanceCounter("Process", "Working Set - Private", process.ProcessName);

                        // Filter out processes with zero working set (likely system/kernel processes)
                        if (workingSetMemory > 0)
                        {
                            processDictionary[processId] = new ProcessMemoryInfo(processId, appName, workingSetMemoryPrivate.RawValue);
                        }
                    }
                    catch
                    {
                        // Some processes may not be accessible; silently skip them
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                return processDictionary;
            });
        }
    }
}
