using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace DotRush.Debugging.Host.TestPlatform.Protocol;

public class TestNodeUpdate {
    [JsonPropertyName("parent")] public string? Parent { get; set; }
    [JsonPropertyName("node")] public TestNode? Node { get; set; }
}

public class TestNode {
    [JsonPropertyName("uid")] public string? Id { get; set; }
    [JsonPropertyName("display-name")] public string? DisplayName { get; set; }
    [JsonPropertyName("location.type")] public string? LocationType { get; set; }
    [JsonPropertyName("location.method")] public string? LocationMethod { get; set; }
    [JsonPropertyName("location.method-arity")] public int? LocationMethodArity { get; set; }
    [JsonPropertyName("location.file")] public string? LocationFile { get; set; }
    [JsonPropertyName("location.line-start")] public int? LocationLineStart { get; set; }
    [JsonPropertyName("location.line-end")] public int? LocationLineEnd { get; set; }
    [JsonPropertyName("node-type")] public string? NodeType { get; set; }
    [JsonPropertyName("execution-state")] public string? ExecutionState { get; set; }
    [JsonPropertyName("time.duration-ms")] public double TimeDurationMs { get; set; }
    [JsonPropertyName("error.message")] public string? ErrorMessage { get; set; }
    [JsonPropertyName("error.stacktrace")] public string? ErrorStackTrace { get; set; }
    [JsonPropertyName("assert.actual")] public string? AssertActual { get; set; }
    [JsonPropertyName("assert.expected")] public string? AssertExpected { get; set; }

    public bool InProgress => ExecutionState == ExecutionStates.InProgress || ExecutionState == ExecutionStates.Discovered;
    public string GetFullyQualifiedName() {
        if (string.IsNullOrEmpty(LocationType) || string.IsNullOrEmpty(LocationMethod))
            return "<unknown>";

        var typeName = RemoveBrackets(LocationType);
        var methodName = RemoveBrackets(LocationMethod);
        return $"{typeName}.{methodName}";
    }

    public static TestResult ToTestResult(TestNode node) {
        var testResult = new TestResult(new TestCase(
            node.GetFullyQualifiedName(),
            new Uri($"executor://{nameof(TestingPlatformToVSTestBridge)}"),
            node.LocationFile ?? "source"
        ));

        testResult.DisplayName = node.DisplayName;
        testResult.ErrorStackTrace = node.ErrorStackTrace;
        testResult.ErrorMessage = node.ErrorMessage;
        testResult.Duration = TimeSpan.FromMilliseconds(node.TimeDurationMs);
        testResult.Outcome = ToTestOutcome(node.ExecutionState);
        return testResult;
    }
    private static TestOutcome ToTestOutcome(string? state) {
        switch (state) {
            case ExecutionStates.Passed: return TestOutcome.Passed;
            case ExecutionStates.Failed: return TestOutcome.Failed;
            case ExecutionStates.Skipped: return TestOutcome.Skipped;
            case ExecutionStates.TimedOut: return TestOutcome.Failed;
            case ExecutionStates.Error: return TestOutcome.Failed;
            case ExecutionStates.Cancelled: return TestOutcome.Skipped;
            case ExecutionStates.InProgress: return TestOutcome.None;
            case ExecutionStates.Discovered: return TestOutcome.None;
            default: return TestOutcome.None;
        }
    }
    private static string RemoveBrackets(string input) {
        var bracketIndex = input.IndexOf('(');
        if (bracketIndex > 0)
            return input.Substring(0, bracketIndex);
        return input;
    }
}

public static class ExecutionStates {
    public const string Discovered = "discovered";
    public const string InProgress = "in-progress";
    public const string Passed = "passed";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string TimedOut = "timed-out";
    public const string Error = "error";
    public const string Cancelled = "cancelled";
}