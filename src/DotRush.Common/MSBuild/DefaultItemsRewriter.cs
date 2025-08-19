using System.Xml.Linq;

namespace DotRush.Common.MSBuild;

public static class DefaultItemsRewriter {
    private static char pathSeparator = '\\';

    public static void AddCompilerItem(string itemPath) {
        foreach (var xmlPath in GetProjectFiles(itemPath)) {
            var document = LoadXmlDocument(xmlPath);
            if (document?.Root == null)
                continue;

            var itemGroup = FindOrCreateCompileItemGroup(document);
            var relativePath = GetItemPath(itemPath, xmlPath);

            if (CompileItemExists(itemGroup, relativePath))
                continue;

            InsertCompileItemAlphabetically(itemGroup, relativePath);
            document.Save(xmlPath);
        }
    }
    public static void RemoveCompilerItem(string itemPath) {
        foreach (var xmlPath in GetProjectFiles(itemPath)) {
            var document = LoadXmlDocument(xmlPath);
            if (document?.Root == null)
                continue;

            var relativePath = GetItemPath(itemPath, xmlPath);
            var itemGroups = GetItemGroups(document);

            bool documentModified = false;
            foreach (var itemGroup in itemGroups) {
                var compileItems = GetCompileItems(itemGroup)
                    .Where(c => c.Attribute("Include")?.Value?.StartsWith(relativePath, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();

                foreach (var compileItem in compileItems) {
                    compileItem.Remove();
                    documentModified = true;
                }
            }

            if (documentModified)
                document.Save(xmlPath);
        }
    }

    private static string GetItemPath(string itemPath, string xmlPath) {
        var xmlDirectory = Path.GetDirectoryName(xmlPath)!;
        if (!itemPath.StartsWith(xmlDirectory, StringComparison.OrdinalIgnoreCase))
            return itemPath;

        var relativePath = itemPath.Substring(xmlDirectory.Length + 1);
        return relativePath.Replace('/', pathSeparator).Replace('\\', pathSeparator);
    }

    private static string[] GetProjectFiles(string itemPath) {
        var itemDirectory = Path.GetDirectoryName(itemPath);
        while (itemDirectory != null) {
            var projectFiles = Directory.GetFiles(itemDirectory, "*.csproj", SearchOption.TopDirectoryOnly);
            if (projectFiles.Length > 0)
                return projectFiles;

            itemDirectory = Path.GetDirectoryName(itemDirectory);
        }
        return Array.Empty<string>();
    }

    private static XDocument? LoadXmlDocument(string xmlPath) {
        try {
            return XDocument.Load(xmlPath);
        } catch {
            return null;
        }
    }

    private static IEnumerable<XElement> GetItemGroups(XDocument document) {
        return document.Root?.Elements().Where(e => e.Name.LocalName == "ItemGroup") ?? Enumerable.Empty<XElement>();
    }

    private static IEnumerable<XElement> GetCompileItems(XElement itemGroup) {
        return itemGroup.Elements().Where(e => e.Name.LocalName == "Compile");
    }

    private static XElement FindOrCreateCompileItemGroup(XDocument document) {
        var itemGroups = GetItemGroups(document);
        var existingGroup = itemGroups.FirstOrDefault(g => GetCompileItems(g).Any());

        if (existingGroup != null)
            return existingGroup;

        // Create new ItemGroup
        var newItemGroup = new XElement(document.Root!.GetDefaultNamespace() + "ItemGroup");
        document.Root.Add(newItemGroup);
        return newItemGroup;
    }

    private static bool CompileItemExists(XElement itemGroup, string relativePath) {
        return GetCompileItems(itemGroup).Any(c => c.Attribute("Include")?.Value == relativePath);
    }

    private static void InsertCompileItemAlphabetically(XElement itemGroup, string relativePath) {
        var newCompileItem = new XElement(itemGroup.GetDefaultNamespace() + "Compile",
            new XAttribute("Include", relativePath));

        var compileItems = GetCompileItems(itemGroup).ToList();
        var insertIndex = compileItems
            .Select((item, index) => new { item, index })
            .FirstOrDefault(x => string.Compare(relativePath, x.item.Attribute("Include")?.Value, StringComparison.OrdinalIgnoreCase) < 0)
            ?.index;

        if (insertIndex.HasValue && insertIndex.Value < compileItems.Count) {
            compileItems[insertIndex.Value].AddBeforeSelf(newCompileItem);
        }
        else {
            itemGroup.Add(newCompileItem);
        }
    }
}