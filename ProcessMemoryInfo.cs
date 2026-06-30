namespace RamTreeMap
{
    /// <summary>
    /// Represents memory information for a running process.
    /// </summary>
    public class ProcessMemoryInfo
    {
        public int ProcessId { get; set; }
        public string AppName { get; set; }
        public long RamUsage { get; set; }

        public ProcessMemoryInfo(int processId, string appName, long ramUsage)
        {
            ProcessId = processId;
            AppName = appName;
            RamUsage = ramUsage;
        }
    }
}
