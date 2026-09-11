using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Workspaces.Components;

/// <summary>
/// Shares one <see cref="PortableExecutableReference"/> (and its loaded metadata image) per assembly path between
/// all projects of a solution, the same way Roslyn's internal metadata service does for its workspaces.
/// </summary>
internal sealed class MetadataReferenceCache {
    private readonly ConcurrentDictionary<string, PortableExecutableReference> references = new ConcurrentDictionary<string, PortableExecutableReference>(StringComparer.OrdinalIgnoreCase);

    public PortableExecutableReference GetReference(string path, MetadataReferenceProperties properties) {
        var reference = references.GetOrAdd(path, CreateReference);
        return properties == MetadataReferenceProperties.Assembly ? reference : reference.WithProperties(properties);
    }

    private static PortableExecutableReference CreateReference(string path) {
        var documentationPath = Path.ChangeExtension(path, ".xml");
        var documentation = File.Exists(documentationPath) ? XmlDocumentationProvider.CreateFromFile(documentationPath) : null;
        return MetadataReference.CreateFromFile(path, MetadataReferenceProperties.Assembly, documentation);
    }
}
