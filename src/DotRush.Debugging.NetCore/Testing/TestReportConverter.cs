using System.Xml;
using DotRush.Debugging.NetCore.Testing.Models;

namespace DotRush.Debugging.NetCore.Testing;

public static class ReportConverter {

    public static List<TestResult> ReadReport(string trxPath) {
        var result = new List<TestResult>();
        if (!File.Exists(trxPath))
            return result;

        var doc = new XmlDocument();
        doc.Load(trxPath);
        var testResults = doc.GetElementsByTagName("UnitTestResult");
        var tests = doc.GetElementsByTagName("UnitTest").Cast<XmlNode>().ToArray();
        if (testResults == null)
            return result;

        CreateTestIdCache(tests);

        foreach (XmlNode testResult in testResults) {
            if (testResult.Attributes == null)
                continue;

            result.Add(new TestResult {
                FullName = GetTestFullName(testResult.Attributes),
                State = testResult.Attributes["outcome"]?.Value,
                Duration = testResult.Attributes["duration"]?.Value,
                StackTrace =  testResult.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='StackTrace']")?.InnerText,
                ErrorMessage = testResult.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")?.InnerText,
            });
        }
        return result;
    }

    private static string GetTestFullName(XmlAttributeCollection testNodeAttributes) {
        var testId = testNodeAttributes["testId"]?.Value;
        if (string.IsNullOrEmpty(testId))
            return RemoveInlineData(testNodeAttributes["testName"]?.Value);

        if (!testIdCache.TryGetValue(testId, out var testNode))
            return RemoveInlineData(testNodeAttributes["testName"]?.Value);

        var testMethod = testNode.SelectSingleNode("*[local-name()='TestMethod']");
        if (testMethod == null || testMethod.Attributes == null)
            return RemoveInlineData(testNodeAttributes["testName"]?.Value);
    
        var testClassName = testMethod.Attributes["className"]?.Value;
        var testName = testMethod.Attributes["name"]?.Value;
        return RemoveInlineData($"{testClassName}.{testName}");
    }
    private static string RemoveInlineData(string? fullName) {
        if (string.IsNullOrEmpty(fullName))
            return string.Empty;

        var index = fullName.IndexOf('(', StringComparison.Ordinal);
        return index > 0 ? fullName.Substring(0, index) : fullName;
    }

    private static Dictionary<string, XmlNode> testIdCache = new();
    private static void CreateTestIdCache(XmlNode[] testNodes) {
        testIdCache = new Dictionary<string, XmlNode>();
        foreach (XmlNode testNode in testNodes) {
            if (testNode.Attributes == null)
                continue;

            var testId = testNode.Attributes["id"]?.Value;
            if (string.IsNullOrEmpty(testId))
                continue;

            testIdCache[testId] = testNode;
        }
    }
}