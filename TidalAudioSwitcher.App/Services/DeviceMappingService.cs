using System.Text.RegularExpressions;
using TidalAudioSwitcher.App.Models;

namespace TidalAudioSwitcher.App.Services;

public interface IDeviceMappingService
{
    DeviceMapping? Map(TidalDevice tidalDevice, IReadOnlyList<AudioEndpoint> windowsEndpoints);

    IReadOnlyList<DeviceMapping> MapAll(
        IReadOnlyList<TidalDevice> tidalDevices,
        IReadOnlyList<AudioEndpoint> windowsEndpoints);
}

/// <summary>
/// Heuristic name matching between TIDAL device names and Windows endpoint friendly names.
/// </summary>
public sealed partial class DeviceMappingService : IDeviceMappingService
{
    public DeviceMapping? Map(TidalDevice tidalDevice, IReadOnlyList<AudioEndpoint> windowsEndpoints)
    {
        return MapAll([tidalDevice], windowsEndpoints).FirstOrDefault();
    }

    public IReadOnlyList<DeviceMapping> MapAll(
        IReadOnlyList<TidalDevice> tidalDevices,
        IReadOnlyList<AudioEndpoint> windowsEndpoints)
    {
        var mappings = new List<DeviceMapping>();

        foreach (var tidal in tidalDevices)
        {
            (AudioEndpoint Endpoint, int Score, string Reason)? best = null;
            foreach (var endpoint in windowsEndpoints)
            {
                var scored = Score(tidal, endpoint);
                if (scored.Score <= 0)
                {
                    continue;
                }

                if (best is null || scored.Score > best.Value.Score)
                {
                    best = scored;
                }
            }

            if (best is null)
            {
                continue;
            }

            mappings.Add(new DeviceMapping
            {
                TidalDevice = tidal,
                WindowsEndpoint = best.Value.Endpoint,
                MatchScore = best.Value.Score,
                MatchReason = best.Value.Reason
            });
        }

        return mappings;
    }

    private static (AudioEndpoint Endpoint, int Score, string Reason) Score(TidalDevice tidal, AudioEndpoint endpoint)
    {
        var tidalNorm = Normalize(tidal.Name);
        var winNorm = Normalize(endpoint.FriendlyName);

        if (string.IsNullOrWhiteSpace(tidalNorm) || string.IsNullOrWhiteSpace(winNorm))
        {
            return (endpoint, 0, "empty name");
        }

        if (string.Equals(tidalNorm, winNorm, StringComparison.OrdinalIgnoreCase))
        {
            return (endpoint, 100, "exact normalized match");
        }

        if (winNorm.Contains(tidalNorm, StringComparison.OrdinalIgnoreCase)
            || tidalNorm.Contains(winNorm, StringComparison.OrdinalIgnoreCase))
        {
            return (endpoint, 80, "substring match");
        }

        var tidalTokens = Tokenize(tidalNorm);
        var winTokens = Tokenize(winNorm);
        var overlap = tidalTokens.Intersect(winTokens, StringComparer.OrdinalIgnoreCase).Count();
        var union = tidalTokens.Union(winTokens, StringComparer.OrdinalIgnoreCase).Count();

        if (overlap == 0)
        {
            return (endpoint, 0, "no token overlap");
        }

        var jaccard = (int)Math.Round(60.0 * overlap / union);
        return (endpoint, jaccard, $"token overlap {overlap}/{union}");
    }

    private static string Normalize(string value)
    {
        var s = value.Trim();
        s = WhitespaceRegex().Replace(s, " ");
        s = s.Replace("(TM)", "", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("(R)", "", StringComparison.OrdinalIgnoreCase);
        return s;
    }

    private static HashSet<string> Tokenize(string value)
    {
        return SplitRegex().Split(value.ToLowerInvariant())
            .Where(t => t.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\s\-_/\\()\[\],.:;]+")]
    private static partial Regex SplitRegex();
}
