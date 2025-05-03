using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalCompletionService {
    internal static readonly Type? completionServiceType;
    internal static readonly MethodInfo? getCompletionsAsyncMethod;

    public static bool IsInitialized => completionServiceType != null && getCompletionsAsyncMethod != null;

    static InternalCompletionService() {
        completionServiceType = typeof(CompletionService);
        getCompletionsAsyncMethod = completionServiceType?.GetMethod("GetCompletionsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    public static Task<CompletionList> GetCompletionsAsync(CompletionService completionService, Document document, int position, object completionOptions, CancellationToken cancellationToken) {
        if (getCompletionsAsyncMethod == null)
            return Task.FromResult(CompletionList.Empty);

        var passThroughOptions = document.Project.Solution.Options;
        var completionTrigger = CompletionTrigger.Invoke;

        var result = getCompletionsAsyncMethod.Invoke(completionService, new object?[] {
            document, /// <param name="document">The document that completion is occurring within.</param>
            position, /// <param name="caretPosition">The position of the caret after the triggering action.</param>
            completionOptions,
            passThroughOptions,/// <param name="options">The CompletionOptions that override the default options.</param>
            completionTrigger, /// <param name="trigger">The triggering action.</param>
            null,              /// <param name="roles">Optional set of roles associated with the editor state.</param>
            cancellationToken  /// <param name="cancellationToken"></param>
        });

        return result == null ? Task.FromResult(CompletionList.Empty) : (Task<CompletionList>)result;
    }
}