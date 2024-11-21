using System.Xml;
using DotRush.Essentials.TestExplorer.Models;

namespace DotRush.Essentials.TestExplorer;

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

        foreach (XmlNode testResult in testResults) {
            if (testResult.Attributes == null)
                continue;

            result.Add(new TestResult {
                State = testResult.Attributes["outcome"]?.Value,
                Duration = testResult.Attributes["duration"]?.Value,
                FullName = GetTestFullName(testResult.Attributes, tests),
                StackTrace =  testResult.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='StackTrace']")?.InnerText,
                ErrorMessage = testResult.SelectSingleNode("*[local-name()='Output']/*[local-name()='ErrorInfo']/*[local-name()='Message']")?.InnerText,
            });
        }
        return result;
    }

     private static string GetTestFullName(XmlAttributeCollection testNodeAttributes, XmlNode[] testNodes) {
        var testId = testNodeAttributes["testId"]?.Value;
        if (string.IsNullOrEmpty(testId))
            return RemoveInlineData(testNodeAttributes["testName"]?.Value);

        var testNode = testNodes.FirstOrDefault(p => p.Attributes?["id"]?.Value == testId);
        var testMethod = testNode?.SelectSingleNode("*[local-name()='TestMethod']");
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
}