using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentFormatting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using RoslynFormattingOptions = Microsoft.CodeAnalysis.Formatting.FormattingOptions;

namespace DotRush.Roslyn.Server.Extensions;

public static class FormattingExtensions {
    public static async Task<OptionSet> ToOptionSetAsync(this FormattingOptions? options, Document document, CancellationToken cancellationToken) {
        var optionSet = await document.GetOptionsAsync(cancellationToken);
        if (options == null)
            return optionSet;

        return optionSet
            .WithChangedOption(RoslynFormattingOptions.UseTabs, document.Project.Language, !options.InsertSpaces)
            .WithChangedOption(RoslynFormattingOptions.TabSize, document.Project.Language, (int)options.TabSize)
            .WithChangedOption(RoslynFormattingOptions.IndentationSize, document.Project.Language, (int)options.TabSize);
    }
}
