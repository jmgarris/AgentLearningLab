using System.Text.Json;

namespace AgentLearningLab.Tools;

internal static class ToolJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SerializerOptions);

    public static JsonDocument ParseValidated(string json) => JsonDocument.Parse(json);
}
