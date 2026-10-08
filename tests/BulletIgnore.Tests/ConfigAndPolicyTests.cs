using System.Text.Json;
using Sharp.Shared.Enums;
using Xunit;

namespace BulletIgnore.Tests;

public sealed class ConfigAndPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void OnlyCtTeamIsFiltered(int team, bool expected)
        => Assert.Equal(expected, HumanSurfacePolicy.IsHumanTeam((CStrikeTeam)team));

    [Fact]
    public void EmptyConfigurationIsDisabled()
    {
        var config = PluginConfig.Parse("{}");
        Assert.False(config.Enabled);
        Assert.False(config.CtOnlyPolicyAcknowledged);
    }

    [Fact]
    public void ExplicitOptInIsAccepted()
    {
        var config = PluginConfig.Parse("{\"enabled\":true,\"ct_only_policy_acknowledged\":true}");
        Assert.True(config.Enabled);
        Assert.True(config.CtOnlyPolicyAcknowledged);
    }

    [Fact]
    public void EnablingWithoutAcknowledgingCtOnlyBehaviorIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PluginConfig.Parse("{\"enabled\":true}"));

    [Fact]
    public void SupportsCommentsAndTrailingComma()
        => Assert.False(PluginConfig.Parse("{ // explanation\n \"enabled\": false, }").Enabled);

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{broken")]
    [InlineData("{\"enable\":true}")]
    [InlineData("{\"enabled\":\"true\"}")]
    public void InvalidOrMisspelledConfigurationIsRejected(string json)
        => Assert.Throws<JsonException>(() => PluginConfig.Parse(json));

    [Theory]
    [InlineData("player")]
    [InlineData("PLAYER")]
    [InlineData("C_CSPlayerPawn")]
    [InlineData("CCSPlayerPawn")]
    [InlineData("CCSPlayerPawnDerived")]
    [InlineData("CBasePlayerPawn")]
    [InlineData("CBasePlayerPawnDerived")]
    public void RecognizesOriginalPawnClassPolicy(string name)
        => Assert.True(HumanSurfacePolicy.IsPlayerPawnClass(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("CCSPlayerController")]
    [InlineData("prop_dynamic")]
    [InlineData("weapon_ak47")]
    [InlineData("hegrenade_projectile")]
    public void NonPawnClassesAreNotIgnored(string? name)
        => Assert.False(HumanSurfacePolicy.IsPlayerPawnClass(name));
}
