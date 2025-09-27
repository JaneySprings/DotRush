using System.Text.Json.Serialization;
using Microsoft.TemplateEngine.Abstractions;

namespace DotRush.Debugging.Host.TemplateEngine;

public class ProjectTemplate {
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("identity")] public string? Identity { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("tags")] public string[]? Classifications { get; set; }
    [JsonPropertyName("parameters")] public ParameterInfo[]? Parameters { get; set; }

    public ProjectTemplate(ITemplateInfo templateInfo) {
        Name = templateInfo.Name;
        Identity = templateInfo.Identity;
        Author = templateInfo.Author;
        Description = templateInfo.Description;
        Classifications = templateInfo.Classifications?.ToArray();
        Parameters = templateInfo.ParameterDefinitions?.Where(p => !IsUselessParameter(p)).Select(p => new ParameterInfo(p)).ToArray();
    }

    private static bool IsUselessParameter(ITemplateParameter parameter) {
        if (parameter.DataType.Equals("choice", StringComparison.OrdinalIgnoreCase) && (parameter.Choices == null || parameter.Choices.Count <= 1))
            return true;
        if (parameter.Name.Equals("name", StringComparison.OrdinalIgnoreCase))
            return true; // Handled by IDE

        return false;
    }
}

public class ParameterInfo {
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("defaultValue")] public string? DefaultValue { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("allowMultipleValues")] public bool AllowMultipleValues { get; set; }
    [JsonPropertyName("choices")] public Dictionary<string, ChoiceInfo>? Choices { get; set; }

    public ParameterInfo(ITemplateParameter parameter) {
        Name = parameter.Name;
        Type = parameter.DataType;
        DefaultValue = parameter.DefaultValue;
        Description = parameter.Description;
        AllowMultipleValues = parameter.AllowMultipleValues;
        Choices = parameter.Choices?.ToDictionary(kvp => kvp.Key, kvp => new ChoiceInfo(kvp.Value));
    }
}

public class ChoiceInfo {
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("name")] public string? DisplayName { get; set; }

    public ChoiceInfo(ParameterChoice choice) {
        DisplayName = choice.DisplayName;
        Description = choice.Description;
    }
}