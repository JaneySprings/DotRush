using DotRush.Server.Extensions;
using DotRush.Server.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using CodeAnalysis = Microsoft.CodeAnalysis;

namespace DotRush.Server.Handlers;

public class HoverHandler : HoverHandlerBase {
    private readonly SolutionService solutionService;
    private static readonly SymbolDisplayFormat DefaultFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.None)
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly SymbolDisplayFormat MinimalFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeParameters 
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeConstantValue
        );


    public HoverHandler(SolutionService solutionService) {
        this.solutionService = solutionService;
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities) {
        return new HoverRegistrationOptions {
            DocumentSelector = DocumentSelector.ForLanguage("csharp")
        };
    }

    public override async Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var offset = request.Position.ToOffset(sourceText);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, offset);
        if (symbol == null || semanticModel == null)
            return null;

        // switch (symbol) {
        //     case IParameterSymbol parameter:
        //         return GetParameterDocumentation(parameter);
        //     case ITypeParameterSymbol typeParam:
        //         return GetTypeParameterDocumentation(typeParam);
        //     case IAliasSymbol alias:
        //         return GetAliasDocumentation(alias);
        //     default:
        //         return MakeHover(symbol.GetDocumentationCommentXml());
        // }

        var displayString = symbol.Kind == CodeAnalysis.SymbolKind.NamedType 
            ? symbol.ToDisplayString(DefaultFormat) 
            : symbol.ToMinimalDisplayString(semanticModel, offset, MinimalFormat);

        return new Hover {
            Contents = new MarkedStringsOrMarkupContent(new MarkupContent {
                Kind = MarkupKind.Markdown,
                Value = $"```csharp\n{displayString}\n```"
            })
        };

    }

    // private Hover? GetParameterDocumentation(IParameterSymbol parameter) {
    //     var contaningSymbolDef = parameter.ContainingSymbol.OriginalDefinition;
    //     var documentationComment = contaningSymbolDef.GetDocumentationCommentXml();
    //     return MakeHover(XmlConverter.ConvertDocumentation(documentationComment, Environment.NewLine));
    // }
    // private Hover? GetTypeParameterDocumentation(ITypeParameterSymbol typeParam, string lineEnding = "\n") {
    //     var contaningSymbol = typeParam.ContainingSymbol;
    //     var documentationComment = contaningSymbol.GetDocumentationCommentXml();
    //     return MakeHover(XmlConverter.ConvertDocumentation(documentationComment, Environment.NewLine));
    // }

    // private Hover? GetAliasDocumentation(IAliasSymbol alias, string lineEnding = "\n") {
    //     var target = alias.Target;
    //     var documentationComment = target.GetDocumentationCommentXml();
    //     return MakeHover(XmlConverter.ConvertDocumentation(documentationComment, Environment.NewLine));
    // }
}