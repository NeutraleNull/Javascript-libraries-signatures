// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);

using System.Text.Json.Serialization;

namespace Infrastructure.Database;

public class DatasetEntry
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("cdnjs")] public bool Cdnjs { get; set; }
    [JsonPropertyName("mostDepended")] public bool MostDepended { get; set; }
    [JsonPropertyName("dependedCount")] public object DependedCount { get; set; }
}