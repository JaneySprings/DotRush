namespace DotRush.Roslyn.Workspaces.Components;

public class WorkspaceProgressHandler {
    private int totalOperations;
    private int completedOperations;

    public int GetProgress() {
        if (totalOperations == 0)
            return 0;
        return Math.Clamp(completedOperations * 100 / totalOperations, 0, 100);
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
