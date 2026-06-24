using System.Text;

namespace TidalAudioSwitcher.App.Models;

public sealed class DeviceSwitchLog
{
    public required string Operation { get; init; }
    public AudioEndpoint? PreviousDefault { get; init; }
    public AudioEndpoint? RequestedDefault { get; init; }
    public AudioEndpoint? VerifiedDefault { get; init; }
    public IReadOnlyList<RoleSwitchResult> RoleResults { get; init; } = [];
    public bool AllRolesSucceeded => RoleResults.All(r => r.HResult == 0);
    public bool VerifiedMatch =>
        RequestedDefault is not null
        && VerifiedDefault is not null
        && string.Equals(RequestedDefault.Id, VerifiedDefault.Id, StringComparison.OrdinalIgnoreCase);

    public string ToDisplayText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Operation);
        sb.AppendLine($"  Vorher (Multimedia): {FormatEndpoint(PreviousDefault)}");
        sb.AppendLine($"  Ziel:                {FormatEndpoint(RequestedDefault)}");
        foreach (var role in RoleResults)
        {
            sb.AppendLine($"  SetDefaultEndpoint({role.Role}): 0x{role.HResult:X8} {(role.HResult == 0 ? "OK" : role.Message)}");
        }
        sb.AppendLine($"  Verifiziert:         {FormatEndpoint(VerifiedDefault)}");
        sb.AppendLine($"  Verifikation:        {(VerifiedMatch ? "OK — IDs stimmen überein" : "WARNUNG — Abweichung")}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatEndpoint(AudioEndpoint? endpoint) =>
        endpoint is null ? "(keines)" : $"{endpoint.FriendlyName} [{endpoint.Id}]";
}

public sealed class RoleSwitchResult
{
    public required string Role { get; init; }
    public int HResult { get; init; }
    public string Message { get; init; } = string.Empty;
}
