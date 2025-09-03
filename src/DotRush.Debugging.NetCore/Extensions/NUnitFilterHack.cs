using System.Xml.Linq;
using DotRush.Common.Extensions;

namespace DotRush.Debugging.NetCore.Extensions;

// https://github.com/nunit/nunit-vs-adapter/issues/71
public static class NUnitFilterExtensions {
    public static string? UpdateRunSettingsWithNUnitFilter(string? runSettings, string[] testAssemblies, string[] typeFilters) {
        if (!testAssemblies.Any(IsNUnitAssembly))
            return runSettings;

        var whereFilter = string.Join(" or ", typeFilters.Select(filter => $"test==/{filter}/"));
        if (string.IsNullOrEmpty(whereFilter))
            return runSettings;
        if (string.IsNullOrEmpty(runSettings))
            return GetNUnitRunSettings(whereFilter);

        return UpdateNUnitRunSettings(runSettings, whereFilter);
    }

    private static string GetNUnitRunSettings(string whereFilter) {
        return @$"<?xml version=""1.0"" encoding=""utf-8""?>
<RunSettings>
    <NUnit>
        <Where>{whereFilter}</Where>
    </NUnit>
</RunSettings>";
    }
    private static string UpdateNUnitRunSettings(string runSettings, string whereFilter) {
        return SafeExtensions.Invoke(runSettings, () => {
            var xmlDocument = XDocument.Parse(runSettings);
            var nunitElement = xmlDocument.Root?.Element("NUnit");
            if (nunitElement == null) {
                nunitElement = new XElement("NUnit");
                xmlDocument.Root?.Add(nunitElement);
            }

            var whereElement = nunitElement.Element("Where");
            if (whereElement == null) {
                whereElement = new XElement("Where", whereFilter);
                nunitElement.Add(whereElement);
            }
            else {
                whereElement.Value = whereFilter;
            }

            return xmlDocument.ToString();
        });
    }
    private static bool IsNUnitAssembly(string assemblyPath) {
        var assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
        var assemblies = Directory.GetFiles(assemblyDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        return assemblies.Any(a => Path.GetFileName(a).Contains("NUnit", StringComparison.OrdinalIgnoreCase));
    }
}