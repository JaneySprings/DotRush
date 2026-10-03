using DotRush.Protocol.Models;

namespace DotRush.Protocol.Handlers;

public abstract class TypeHierarchyHandlerBase : IHandler {
    protected abstract Task<List<TypeHierarchyItem>?> Handle(TypeHierarchyPrepareParams request, CancellationToken cancellationToken);
    protected abstract Task<List<TypeHierarchyItem>?> Handle(TypeHierarchySupertypesParams request, CancellationToken cancellationToken);
    protected abstract Task<List<TypeHierarchyItem>?> Handle(TypeHierarchySubtypesParams request, CancellationToken cancellationToken);

    public abstract void RegisterCapability(ServerCapabilities serverCapabilities);
    public void RegisterHandler(LanguageServer server) {
        server.AddRequestHandler<TypeHierarchyPrepareParams, List<TypeHierarchyItem>?>("textDocument/prepareTypeHierarchy", Handle);
        server.AddRequestHandler<TypeHierarchySupertypesParams, List<TypeHierarchyItem>?>("typeHierarchy/supertypes", Handle);
        server.AddRequestHandler<TypeHierarchySubtypesParams, List<TypeHierarchyItem>?>("typeHierarchy/subtypes", Handle);
    }
}
