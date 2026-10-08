using Sharp.Shared.Enums;

namespace BulletIgnore;

internal static class HumanSurfacePolicy
{
    internal static bool IsHumanTeam(CStrikeTeam team) => team == CStrikeTeam.CT;

    internal static bool IsPlayerPawnClass(string? classname)
        => !string.IsNullOrWhiteSpace(classname)
            && (classname.Equals("player", StringComparison.OrdinalIgnoreCase)
                || classname.Equals("C_CSPlayerPawn", StringComparison.OrdinalIgnoreCase)
                || classname.StartsWith("CCSPlayerPawn", StringComparison.OrdinalIgnoreCase)
                || classname.StartsWith("CBasePlayerPawn", StringComparison.OrdinalIgnoreCase));
}
