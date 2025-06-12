using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace DotRush.Debugging.Mono.Extensions;

public static class ExceptionsFilter {
    public static ExceptionBreakpointsFilter AllExceptions => new ExceptionBreakpointsFilter {
        Filter = "all",
        Label = "All Exceptions",
        Description = "Break when an exception is thrown.",
        ConditionDescription = "Comma-separated list of exception types to break on, or if the list starts with '!', a list of exception types to ignore.",
        SupportsCondition = true
    };
}