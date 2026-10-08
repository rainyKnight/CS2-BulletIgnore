using System.Text.Json;
using System.Text.Json.Serialization;

namespace BulletIgnore;

internal sealed class PluginConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    // Explicitly acknowledge that this does NOT compare shooter and victim teams.
    [JsonPropertyName("ct_only_policy_acknowledged")]
    public bool CtOnlyPolicyAcknowledged { get; init; }

    internal static PluginConfig Parse(string json)
    {
        var config = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new JsonException("Configuration must be a JSON object.");
        if (config.Enabled && !config.CtOnlyPolicyAcknowledged)
        {
            throw new InvalidOperationException(
                "Enabling requires ct_only_policy_acknowledged=true. This plugin ignores CT pawns, not arbitrary teammates.");
        }
        return config;
    }
}
