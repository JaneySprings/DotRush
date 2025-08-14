using System.Xml;

namespace DotRush.Common.MSBuild;

public static class DefaultItemsRewriter {
    private static char pathSeparator = '\\';

    public static void AddCompilerItem(string itemPath) {
        foreach (var xmlPath in GetProjectFiles(itemPath)) {
            var xmlDocument = LoadXmlDocument(xmlPath);
            if (xmlDocument == null)
                return;

            var itemGroup = FindOrCreateCompileItemGroup(xmlDocument);
            if (itemGroup == null)
                return;

            var relativePath = GetItemPath(itemPath, xmlPath);
            if (CompileItemExists(itemGroup, relativePath))
                return;

            InsertCompileItemAlphabetically(itemGroup, relativePath);
            xmlDocument.Save(xmlPath);
        }
    }
    public static void RemoveCompilerItem(string itemPath) {
        foreach (var xmlPath in GetProjectFiles(itemPath)) {
            var xmlDocument = LoadXmlDocument(xmlPath);
            if (xmlDocument == null)
                return;

            var relativePath = GetItemPath(itemPath, xmlPath);
            var itemGroups = xmlDocument.SelectNodes("//Project/ItemGroup");
            if (itemGroups == null)
                return;

            foreach (XmlNode itemGroup in itemGroups) {
                var compileItem = itemGroup.SelectSingleNode($"Compile[@Include='{relativePath}']");
                if (compileItem != null) {
                    itemGroup.RemoveChild(compileItem);
                    xmlDocument.Save(xmlPath);
                    return;
                }
            }
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
    private static XmlDocument? LoadXmlDocument(string xmlPath) {
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(xmlPath);
        return xmlDocument;
    }
    private static XmlNode? FindOrCreateCompileItemGroup(XmlDocument xmlDocument) {
        var itemGroups = xmlDocument.SelectNodes("//Project/ItemGroup");
        if (itemGroups != null) {
            foreach (XmlNode itemGroup in itemGroups) {
                if (itemGroup.SelectSingleNode("Compile") != null)
                    return itemGroup;
            }
        }

        // If no ItemGroup with Compile items exists, create a new one
        var projectNode = xmlDocument.SelectSingleNode("//Project");
        if (projectNode == null)
            return null;

        var newItemGroup = xmlDocument.CreateElement("ItemGroup");
        projectNode.AppendChild(newItemGroup);
        return newItemGroup;
    }
    private static bool CompileItemExists(XmlNode itemGroup, string relativePath) {
        return itemGroup.SelectSingleNode($"Compile[@Include='{relativePath}']") != null;
    }
    private static void InsertCompileItemAlphabetically(XmlNode itemGroup, string relativePath) {
        var newCompileItem = itemGroup.OwnerDocument!.CreateElement("Compile");
        newCompileItem.SetAttribute("Include", relativePath);

        var compileItems = itemGroup.SelectNodes("Compile")?.Cast<XmlNode>().ToList() ?? new List<XmlNode>();

        XmlNode? insertBefore = null;
        foreach (var compileItem in compileItems) {
            var existingInclude = compileItem.Attributes?["Include"]?.Value;
            if (existingInclude != null && string.Compare(relativePath, existingInclude, StringComparison.OrdinalIgnoreCase) < 0) {
                insertBefore = compileItem;
                break;
            }
        }

        if (insertBefore != null)
            itemGroup.InsertBefore(newCompileItem, insertBefore);
        else
            itemGroup.AppendChild(newCompileItem);
    }
}