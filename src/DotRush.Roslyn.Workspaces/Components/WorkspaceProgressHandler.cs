namespace DotRush.Roslyn.Workspaces.Components;

public class WorkspaceProgressHandler {
    private int totalOperations;
    private int completedOperations;
    private int progress;

    public int GetProgress() {
        if (totalOperations == 0)
            return 0;

        progress = Math.Clamp(completedOperations * 100 / totalOperations, progress, 100);
        return progress;
    }

    public void ScheduleOperations(int operationsCount) {
        totalOperations += operationsCount;
    }
    public void CompleteOperation() {
        completedOperations++;
    }
    public void Reset() {
        totalOperations = 0;
        completedOperations = 0;
    }
}
