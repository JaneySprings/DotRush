using System.Xml.Linq;
using DotRush.Common.Extensions;
using DotRush.Common.Logging;

namespace DotRush.Debugging.Host.Extensions;

// https://github.com/nunit/nunit-vs-adapter/issues/71
public static class NUnitFilterExtensions {

    private static readonly CurrentClassLogger currentClassLogger = new(nameof(NUnitFilterExtensions));

    public static string? UpdateRunSettingsWithNUnitFilter(string? runSettings, string[] testAssemblies, string[] typeFilters) {
        currentClassLogger.Debug($"UpdateRunSettingsWithNUnitFilter: {testAssemblies.Length} assemblies, {typeFilters.Length} filters");

        if (!testAssemblies.Any(IsNUnitAssembly)) {
            currentClassLogger.Debug("No NUnit assemblies found");
            return runSettings;
        }

        var whereParts = new List<string>();
        if (typeFilters.Length > 0) {
            var vsFilters = typeFilters.Select(f => $"test==/{f}/").ToArray();
            whereParts.Add("(" + string.Join(" or ", vsFilters) + ")");
        }

        var runSettingsFilter = ExtractTestCaseFilterFromRunSettings(runSettings);
        if (!string.IsNullOrEmpty(runSettingsFilter)) {
            var translated = TranslateRunSettingsCategoryFilter(runSettingsFilter);
            if (!string.IsNullOrEmpty(translated))
                whereParts.Add(translated);
        }

        var whereFilter = string.Join(" and ", whereParts);

        // If we have NUnit assemblies but no specific filters, create empty NUnit settings
        if (whereParts.Count == 0) {
            currentClassLogger.Debug("Creating empty NUnit settings");
            if (string.IsNullOrEmpty(runSettings))
                return GetNUnitRunSettings("");
            return UpdateNUnitRunSettings(runSettings, "");
        }

        currentClassLogger.Debug($"Creating NUnit settings with filter: '{whereFilter}'");
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
        try {
            if (Path.GetFileName(assemblyPath).Contains("NUnit", StringComparison.OrdinalIgnoreCase))
                return true;

            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrEmpty(assemblyDirectory) || !Directory.Exists(assemblyDirectory))
                return false;

            var assemblies = Directory.GetFiles(assemblyDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            return assemblies.Any(a => Path.GetFileName(a).Contains("NUnit", StringComparison.OrdinalIgnoreCase));
        } catch (Exception) {
            return false;
        }
    }

    private static string? ExtractTestCaseFilterFromRunSettings(string? runSettings) {
        if (string.IsNullOrEmpty(runSettings))
            return null;

        try {
            var xdoc = System.Xml.Linq.XDocument.Parse(runSettings);
            var filter = xdoc.Root?
                .Element("RunConfiguration")?
                .Element("TestCaseFilter")?
                .Value?
                .Trim();
            currentClassLogger.Debug($"ExtractTestCaseFilterFromRunSettings: '{filter}'");
            return filter;
        } catch (Exception ex) {
            currentClassLogger.Debug($"ExtractTestCaseFilterFromRunSettings failed: {ex.Message}");
            return null;
        }
    }

    public static string? TranslateRunSettingsCategoryFilter(string filter) {
        if (string.IsNullOrWhiteSpace(filter))
            return null;

        currentClassLogger.Debug($"TranslateRunSettingsCategoryFilter input: '{filter}'");
        filter = filter.Replace(" ", string.Empty);

        string? Parse(string expr) {
            if (expr.Length == 0)
                return null;

            if (expr[0] == '(' && expr[^1] == ')' && MatchingParens(expr))
                return Parse(expr.Substring(1, expr.Length - 2));

            var orParts = SplitTop(expr, '|');
            if (orParts.Count > 1) {
                var parsedParts = orParts.Select(p => Parse(p) ?? string.Empty).Where(p => !string.IsNullOrEmpty(p));
                var result = string.Join(" or ", parsedParts);
                return "(" + result + ")";
            }

            var andParts = SplitTop(expr, '&');
            if (andParts.Count > 1) {
                var parsedParts = andParts.Select(p => Parse(p) ?? string.Empty).Where(p => !string.IsNullOrEmpty(p));
                var result = string.Join(" and ", parsedParts);
                return "(" + result + ")";
            }

            if (expr.StartsWith("Category=", StringComparison.OrdinalIgnoreCase))
                return string.Concat("cat==", expr.AsSpan("Category=".Length));

            if (expr.StartsWith("Category!=", StringComparison.OrdinalIgnoreCase))
                return string.Concat("cat!=", expr.AsSpan("Category!=".Length));

            // unrecognized leaf (e.g., FullyQualifiedName, traits other than Category) → ignore
            return null;
        }

        static bool MatchingParens(string s) {
            int depth = 0;
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') {
                    depth--;
                    if (depth == 0 && i != s.Length - 1)
                        return false;
                }
            }
            return depth == 0;
        }

        static List<string> SplitTop(string s, char op) {
            var parts = new List<string>();
            int depth = 0, last = 0;
            for (int i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == op && depth == 0) {
                    parts.Add(s.Substring(last, i - last));
                    last = i + 1;
                }
            }
            if (last < s.Length)
                parts.Add(s.Substring(last));
            return parts;
        }

        var result = Parse(filter);
        currentClassLogger.Debug($"TranslateRunSettingsCategoryFilter result: '{result}'");
        return result;
    }

}