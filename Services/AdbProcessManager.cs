using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace Qatalyst.Services;

public class AdbProcessManager : IDisposable
{
    private readonly ConcurrentDictionary<int, Process> _managedProcesses = new();

    public void AddToManagedProcess(Process process)
    {
        _managedProcesses[process.Id] = process;
    }

    public void KillAllManagedProcesses()
    {
        foreach (var process in _managedProcesses.Values.ToList())
        {
            try
            {
                if (process.HasExited) continue;

                Console.WriteLine($"Error killing process - name: {process.ProcessName} | id: {process.Id}");
                process.Kill();
                process.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error killing process {process.Id}: {ex.Message}");
            }
        }
        _managedProcesses.Clear();
    }

    public void Dispose()
    {
        KillAllManagedProcesses();
        GC.SuppressFinalize(this);
    }
}