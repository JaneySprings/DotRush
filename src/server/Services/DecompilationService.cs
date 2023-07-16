using DotRush.Server.Extensions;
using DotRush.Server.Containers;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using FullTypeName = ICSharpCode.Decompiler.TypeSystem.FullTypeName;
using ProtocolModels = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Text;

namespace DotRush.Server.Services;

public class DecompilationService {
    private readonly string decompilationCacheDirectory;

    private readonly ILanguageServerFacade serverFacade;
    private readonly SolutionService solutionService;


    public DecompilationService(ILanguageServerFacade serverFacade, SolutionService solutionService) {
        this.serverFacade = serverFacade;
        this.solutionService = solutionService;
        this.decompilationCacheDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "decompilation");
    
        if (!Directory.Exists(this.decompilationCacheDirectory))
            Directory.CreateDirectory(this.decompilationCacheDirectory);

        foreach (var file in Directory.GetFiles(this.decompilationCacheDirectory))
            File.Delete(file);
    }


    public async Task<DecompilationContainer?> Decompile(ISymbol symbol, Project project, CancellationToken cancellationToken) {
        return await ServerExtensions.SafeHandlerAsync<DecompilationContainer?>(async () => {
            var namedTypeSymbol = symbol;
            if (symbol is not INamedTypeSymbol)
                namedTypeSymbol = symbol.ContainingType;

            var compilation = await project.GetCompilationAsync(cancellationToken);
            var metadataReference = compilation?.GetMetadataReference(namedTypeSymbol.ContainingAssembly);
            if (metadataReference is not PortableExecutableReference portableExecutableReference)
                return null;

            var resolver = new UniversalAssemblyResolver(portableExecutableReference.FilePath, false, string.Empty);
            var decompiler = new CSharpDecompiler(portableExecutableReference.FilePath, resolver, new DecompilerSettings(LanguageVersion.Latest) {
                DecompileMemberBodies = false,
                AsyncAwait = false,
            });

            decompiler.CancellationToken = cancellationToken;
            decompiler.AstTransforms.Add(new EscapeInvalidIdentifiers());

            var decompiledSourceTextBuilder = new StringBuilder(decompiler.DecompileTypeAsString(new FullTypeName(namedTypeSymbol.ToDisplayString())));
            if (decompiledSourceTextBuilder.Length == 0)
                return null;

            decompiledSourceTextBuilder.Insert(0, $"#region Assembly {namedTypeSymbol.ContainingAssembly}\n// {portableExecutableReference.FilePath}\n#endregion\n\n");
            return new DecompilationContainer(namedTypeSymbol, SourceText.From(decompiledSourceTextBuilder.ToString(), Encoding.UTF8));
        });
    }

    public async Task<ProtocolModels.Location?> DecompileSourceTextWithDefinitionRequest(DefinitionParams request, CancellationToken cancellationToken) {
        var documentId = this.solutionService.Solution?.GetDocumentIdsWithFilePath(request.TextDocument.Uri.GetFileSystemPath()).FirstOrDefault();
        if (documentId == null)
            return null;

        var document = this.solutionService.Solution?.GetDocument(documentId);
        if (document == null)
            return null;

        var sourceText = await document.GetTextAsync(cancellationToken);
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, request.Position.ToOffset(sourceText), cancellationToken);
        if (symbol == null) 
            return null;
        
        var decompilation = await Decompile(symbol, document.Project, cancellationToken);
        if (decompilation == null)
            return null;

        var filePath = Path.Combine(this.decompilationCacheDirectory, $"{decompilation.Symbol.Name}.cs");
        if (File.Exists(filePath))
            File.Delete(filePath);

        File.WriteAllText(filePath, decompilation.SourceText.ToString());
        return new ProtocolModels.Location() {
            Uri = OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri.From(filePath),
            Range = new ProtocolModels.Range() {
                Start = new ProtocolModels.Position(0, 0),
                End = new ProtocolModels.Position(0, 0),
            },
        };
    }
}