using System.Diagnostics;

namespace CodexUniversalLauncher;

internal sealed record AppShutdownResult(int Detected, int GracefullyClosed, int ForcedClosed);

internal static class AppProcessManager
{
    private static readonly string[] TargetProcessNames = ["ChatGPT"];

    internal static bool IsTargetProcessName(string processName) =>
        TargetProcessNames.Contains(processName, StringComparer.OrdinalIgnoreCase);

    public static bool HasRunningApp()
    {
        var processes = GetRunningProcesses();
        try
        {
            return processes.Count > 0;
        }
        finally
        {
            DisposeAll(processes);
        }
    }

    public static async Task<AppShutdownResult> CloseRunningAppAsync(
        Action<string>? progress,
        CancellationToken cancellationToken)
    {
        var initial = GetRunningProcesses();
        var detected = initial.Count;
        if (detected == 0)
            return new AppShutdownResult(0, 0, 0);

        var initialIds = initial.Select(process => process.Id).ToHashSet();
        progress?.Invoke($"检测到 {detected} 个 ChatGPT 进程，正在自动正常关闭……");
        try
        {
            foreach (var process in initial)
            {
                try { process.CloseMainWindow(); } catch { }
            }
        }
        finally
        {
            DisposeAll(initial);
        }

        var deadline = DateTime.UtcNow.AddSeconds(8);
        IReadOnlyList<Process> remaining;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            remaining = GetRunningProcesses();
            if (remaining.Count == 0 || DateTime.UtcNow >= deadline)
                break;
            DisposeAll(remaining);
            await Task.Delay(400, cancellationToken);
        }

        var remainingInitialIds = remaining
            .Where(process => initialIds.Contains(process.Id))
            .Select(process => process.Id)
            .ToHashSet();
        var gracefullyClosed = detected - remainingInitialIds.Count;
        if (remaining.Count == 0)
            return new AppShutdownResult(detected, gracefullyClosed, 0);

        progress?.Invoke($"仍有 {remaining.Count} 个后台进程，正在自动结束……");
        var forcedIds = new HashSet<int>();
        try
        {
            foreach (var process in remaining)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    forcedIds.Add(process.Id);
                }
                catch
                {
                    // A final survivor check below provides the actionable error.
                }
            }
        }
        finally
        {
            DisposeAll(remaining);
        }

        await Task.Delay(1000, cancellationToken);
        var survivors = GetRunningProcesses();
        try
        {
            if (survivors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"仍有 {survivors.Count} 个 ChatGPT 进程无法关闭；已取消重新启动。");
            }
        }
        finally
        {
            DisposeAll(survivors);
        }

        return new AppShutdownResult(detected, gracefullyClosed, forcedIds.Count);
    }

    private static List<Process> GetRunningProcesses()
    {
        var processes = new List<Process>();
        foreach (var processName in TargetProcessNames)
        {
            try
            {
                processes.AddRange(Process.GetProcessesByName(processName)
                    .Where(process => process.Id != Environment.ProcessId));
            }
            catch
            {
                // An unavailable process snapshot is handled by the final launch error path.
            }
        }

        return processes
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static void DisposeAll(IEnumerable<Process> processes)
    {
        foreach (var process in processes)
            process.Dispose();
    }
}
