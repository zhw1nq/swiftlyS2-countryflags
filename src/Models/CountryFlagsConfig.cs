using System.Text.Json.Serialization;

namespace swiftlyS2_countryflags.Models;

public sealed class CountryFlagsConfig
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("Debug")]
    public bool Debug { get; set; } = false;

    [JsonPropertyName("DefaultStatus")]
    public bool DefaultStatus { get; set; } = true;

    [JsonPropertyName("EnableToggleCommand")]
    public bool EnableToggleCommand { get; set; } = true;

    [JsonPropertyName("CacheExpiryHours")]
    public int CacheExpiryHours { get; set; } = 168;

    [JsonPropertyName("AutoSaveIntervalSeconds")]
    public int AutoSaveIntervalSeconds { get; set; } = 120;

    [JsonPropertyName("DefaultBadgeId")]
    public int DefaultBadgeId { get; set; } = 1079;

    [JsonPropertyName("CountryBadges")]
    public Dictionary<string, int> CountryBadges { get; set; } = new();
}
