using System.Reflection;
using DotRush.Roslyn.CodeAnalysis.Extensions;

namespace DotRush.Roslyn.CodeAnalysis.Reflection;

public static class InternalOrganizeImportsOptions {
    internal static readonly Type? organizeImportsOptionsType;
    internal static readonly PropertyInfo? placeSystemNamespaceFirstProperty;
    internal static readonly PropertyInfo? separateImportDirectiveGroupsProperty;

    static InternalOrganizeImportsOptions() {
        organizeImportsOptionsType = ReflectionExtensions.GetTypeFromLoadedAssembly(KnownAssemblies.WorkspacesAssemblyName, "Microsoft.CodeAnalysis.OrganizeImports.OrganizeImportsOptions");
        placeSystemNamespaceFirstProperty = organizeImportsOptionsType?.GetProperty("PlaceSystemNamespaceFirst");
        separateImportDirectiveGroupsProperty = organizeImportsOptionsType?.GetProperty("SeparateImportDirectiveGroups");
    }

    public static object? CreateNew() {
        if (organizeImportsOptionsType == null)
            return null;

        return Activator.CreateInstance(organizeImportsOptionsType);
    }
    public static void AssignValues(object target, bool placeSystemNamespaceFirst, bool separateImportDirectiveGroups) {
        if (placeSystemNamespaceFirstProperty != null)
            placeSystemNamespaceFirstProperty.SetValue(target, placeSystemNamespaceFirst);
        if (separateImportDirectiveGroupsProperty != null)
            separateImportDirectiveGroupsProperty.SetValue(target, separateImportDirectiveGroups);
    }
}