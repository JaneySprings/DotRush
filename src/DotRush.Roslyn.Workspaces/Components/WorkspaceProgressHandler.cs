namespace DotRush.Roslyn.Workspaces.Components;

public class WorkspaceProgressHandler {
    private int totalOperations;
    private int completedOperations;
    private int progress;

    public int GetProgress() {
        var total = Volatile.Read(ref totalOperations);
        if (total == 0)
            return 0;

        progress = Math.Clamp(Volatile.Read(ref completedOperations) * 100 / total, progress, 100);
        return progress;
    }

    public void ScheduleOperations(int operationsCount) {
        Interlocked.Add(ref totalOperations, operationsCount);
    }
    public void CompleteOperation() {
        Interlocked.Increment(ref completedOperations);
    }
    public void Reset() {
        totalOperations = 0;
        completedOperations = 0;
        progress = 0;
    }
}
