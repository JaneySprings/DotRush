using DotRush.Roslyn.CodeAnalysis.Reflection;
using DotRush.Roslyn.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace DotRush.Roslyn.Server.Extensions;

public static class CompletionServiceExtensions {
    public static Task<CompletionList> GetCompletionsAsync(this CompletionService completionService, Document document, int position, ConfigurationService configurationService, CancellationToken cancellationToken) {
        var completionOptions = InternalCompletionOptions.CreateNew();
        if (completionOptions != null) {
            InternalCompletionOptions.AssignValues(completionOptions,
                                                   configurationService.ShowItemsFromUnimportedNamespaces,
                                                   configurationService.TargetTypedCompletionFilter);
        }

        if (!InternalCompletionService.IsInitialized || completionOptions == null)
            return completionService.GetCompletionsAsync(document, position, cancellationToken: cancellationToken);
        
        return InternalCompletionService.GetCompletionsAsync(completionService, document, position, completionOptions, cancellationToken);
    }
}