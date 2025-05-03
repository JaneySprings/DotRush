using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalCSharpOrganizeImportsService {
    internal static readonly Type? csharpOrganizeImportsServiceType;
    internal static readonly MethodInfo? organizeImportsAsyncMethod;

    public static bool IsInitialized => csharpOrganizeImportsServiceType != null && organizeImportsAsyncMethod != null;

    static InternalCSharpOrganizeImportsService() {
        csharpOrganizeImportsServiceType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.CSharpWorkspacesAssemblyName, "Microsoft.CodeAnalysis.CSharp.OrganizeImports.CSharpOrganizeImportsService");
        organizeImportsAsyncMethod = csharpOrganizeImportsServiceType?.GetMethod("OrganizeImportsAsync");
    }

    public static object? CreateNew() {
        if (csharpOrganizeImportsServiceType == null)
            return null;

        return Activator.CreateInstance(csharpOrganizeImportsServiceType);
    }
    public static Task<Document>? OrganizeImportsAsync(object organizeImportsService, Document document, object organizeImportsOptions, CancellationToken cancellationToken) {
        if (organizeImportsAsyncMethod == null)
            return null;

        var result = organizeImportsAsyncMethod?.Invoke(organizeImportsService, new object?[] { document, organizeImportsOptions, cancellationToken });
        if (result is Task<Document> task)
            return task;

        return null;
    }
}