using Microsoft.CodeAnalysis;

namespace DotRush.Server.Services;

public interface ISolutionChangeHandler {
    void CreateCSharpDocument(string file);
    void CreateAdditionalDocument(string file);

    void DeleteCSharpDocument(string file);
    void DeleteCSharpDocument(IEnumerable<DocumentId>? documentIds);
    void DeleteAdditionalDocument(string file);
    void DeleteAdditionalDocument(IEnumerable<DocumentId>? documentIds);

    void UpdateCSharpDocument(string file, string? text);
    void UpdateAdditionalDocument(string file, string? text);
}