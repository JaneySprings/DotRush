using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace DotRush.Roslyn.Server.Extensions;

// https://github.com/OmniSharp/omnisharp-roslyn/blob/ed467d0ad2d877b837380a849f856dcd210d69f7/src/OmniSharp.Roslyn.CSharp/Services/DocumentationConverter.cs#L154
public static class XmlConverter {
    public static string ConvertDocumentation(string? xmlDocumentation, string lineEnding) {
        if (string.IsNullOrEmpty(xmlDocumentation))
            return string.Empty;

        var reader = new StringReader("<docroot>" + xmlDocumentation + "</docroot>");
        using (var xml = XmlReader.Create(reader)) {
            var ret = new StringBuilder();

            try {
                xml.Read();
                string? elementName = null;
                do {
                    if (xml.NodeType == XmlNodeType.Element) {
                        elementName = xml.Name.ToLowerInvariant();
                        switch (elementName) {
                            case "filterpriority":
                                xml.Skip();
                                break;
                            case "remarks":
                                ret.Append(lineEnding);
                                ret.Append("Remarks:");
                                ret.Append(lineEnding);
                                break;
                            case "example":
                                ret.Append(lineEnding);
                                ret.Append("Example:");
                                ret.Append(lineEnding);
                                break;
                            case "exception":
                                ret.Append(lineEnding);
                                ret.Append(GetCref(xml["cref"]!).TrimEnd());
                                ret.Append(": ");
                                break;
                            case "returns":
                                ret.Append(lineEnding);
                                ret.Append("Returns: ");
                                break;
                            case "see":
                                ret.Append(GetCref(xml["cref"]!));
                                ret.Append(xml["langword"]);
                                break;
                            case "seealso":
                                ret.Append(lineEnding);
                                ret.Append("See also: ");
                                ret.Append(GetCref(xml["cref"]!));
                                break;
                            case "paramref":
                                ret.Append(xml["name"]);
                                ret.Append(" ");
                                break;
                            case "typeparam":
                                ret.Append(lineEnding);
                                ret.Append("<");
                                ret.Append(TrimMultiLineString(xml["name"]!, lineEnding));
                                ret.Append(">: ");
                                break;
                            case "param":
                                ret.Append(lineEnding);
                                ret.Append(TrimMultiLineString(xml["name"]!, lineEnding));
                                ret.Append(": ");
                                break;
                            case "value":
                                ret.Append(lineEnding);
                                ret.Append("Value: ");
                                ret.Append(lineEnding);
                                break;
                            case "br":
                            case "para":
                                ret.Append(lineEnding);
                                break;
                        }
                    } else if (xml.NodeType == XmlNodeType.Text) {
                        if (elementName == "code") {
                            ret.Append(xml.Value);
                        } else {
                            ret.Append(TrimMultiLineString(xml.Value, lineEnding));
                        }
                    }
                } while (xml.Read());
            } catch (Exception) {
                return xmlDocumentation;
            }
            return ret.ToString();
        }
    }

    private static string TrimMultiLineString(string input, string lineEnding) {
        var lines = input.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(lineEnding, lines.Select(l => l.TrimStart()));
    }

    private static string GetCref(string cref) {
        if (cref == null || cref.Trim().Length == 0)
            return "";
    
        if (cref.Length < 2)
            return cref;

        if (cref.Substring(1, 1) == ":")
            return cref.Substring(2, cref.Length - 2) + " ";

        return cref + " ";
    }
}