using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.Server.Extensions;

public static class CompletionServiceExtensions {
    private static MethodInfo? getCompletionsAsyncMethod;

    public static object? GetCompletionOptions() {
        var completionOptionsType = typeof(CompletionService).Assembly.GetType("Microsoft.CodeAnalysis.Completion.CompletionOptions");
        if (completionOptionsType == null)
            return null;

        var completionOptions = Activator.CreateInstance(completionOptionsType);
        completionOptionsType.GetProperty("ShowItemsFromUnimportedNamespaces")?.SetValue(completionOptions, false);
        return completionOptions;
    }

    public static Task<CompletionList> GetCompletionsAsync(this CompletionService completionService, Document document, int position, object completionOptions, CancellationToken cancellationToken) {
        if (getCompletionsAsyncMethod == null)
            getCompletionsAsyncMethod = completionService.GetType().GetMethod("GetCompletionsAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        var passThroughOptions = document.Project.Solution.Options;
        var completionTrigger = CompletionTrigger.Invoke;

        if (getCompletionsAsyncMethod == null)
            return Task.FromResult(CompletionList.Empty);

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