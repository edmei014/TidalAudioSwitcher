using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using TidalAudioSwitcher.App.Models;
using TidalAudioSwitcher.App.Services.Cdp;

namespace TidalAudioSwitcher.App.Services;

public interface ITidalCdpService
{
    int DebugPort { get; }

    Task<TidalCdpHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TidalDevice>> ListDevicesAsync(CancellationToken cancellationToken = default);

    Task SelectDeviceAsync(TidalDevice device, string mode, CancellationToken cancellationToken = default);
}

public sealed class TidalCdpService : ITidalCdpService, IDisposable
{
    private readonly HttpClient _http;
    private readonly int _debugPort;

    public TidalCdpService(int debugPort = TidalLaunchConstants.DebugPort)
    {
        _debugPort = debugPort;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public int DebugPort => _debugPort;

    public async Task<TidalCdpHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var tidalRunning = TidalProcessService.IsTidalRunning();

        try
        {
            var targets = await GetTargetsAsync(cancellationToken);
            if (FindMainPageTarget(targets) is not null)
            {
                return new TidalCdpHealthResult
                {
                    Status = TidalCdpHealthStatus.Ready,
                    UserMessage = string.Empty
                };
            }

            if (tidalRunning)
            {
                return new TidalCdpHealthResult
                {
                    Status = TidalCdpHealthStatus.NoDesktopTarget,
                    UserMessage =
                        "TIDAL läuft, aber die Desktop-Oberfläche ist noch nicht bereit.\n\n" +
                        "Bitte warten, bis TIDAL vollständig geladen ist, und dann erneut versuchen.",
                    Detail = "CDP erreichbar, aber kein desktop.tidal.com-Target gefunden."
                };
            }

            return new TidalCdpHealthResult
            {
                Status = TidalCdpHealthStatus.NoDesktopTarget,
                UserMessage =
                    "TIDAL wurde nicht mit den benötigten Startparametern gestartet.",
                Detail = "CDP erreichbar, aber kein desktop.tidal.com-Target gefunden."
            };
        }
        catch (Exception ex) when (IsCdpConnectionFailure(ex))
        {
            StartupLogger.LogException("TidalCdpService.CheckHealthAsync", ex);

            if (tidalRunning)
            {
                return new TidalCdpHealthResult
                {
                    Status = TidalCdpHealthStatus.TidalRunningWithoutCdp,
                    UserMessage =
                        "Bitte TIDAL schließen und über „Automatisch starten“ neu starten.\n\n" +
                        "TIDAL läuft derzeit ohne die benötigten Startparameter " +
                        "(--remote-debugging-port=9222).",
                    Detail = ex.Message
                };
            }

            return new TidalCdpHealthResult
            {
                Status = TidalCdpHealthStatus.TidalNotRunning,
                UserMessage =
                    "TIDAL wurde nicht mit den benötigten Startparametern gestartet.",
                Detail = ex.Message
            };
        }
    }

    public async Task<IReadOnlyList<TidalDevice>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectToMainPageAsync(cancellationToken);

        const string script = """
            (async () => {
              const p = window.NativePlayerComponent.Player();
              return await new Promise((resolve, reject) => {
                const timeout = setTimeout(() => reject(new Error('listDevices timeout after 10s')), 10000);
                p.on('devices', (event, data) => {
                  clearTimeout(timeout);
                  resolve(event?.target ?? event);
                });
                p.listDevices();
              });
            })()
            """;

        var result = await client.EvaluateAsync(script, awaitPromise: true, cancellationToken);
        return ParseDevices(result);
    }

    public async Task SelectDeviceAsync(TidalDevice device, string mode, CancellationToken cancellationToken = default)
    {
        await using var client = await ConnectToMainPageAsync(cancellationToken);

        var payload = device.Raw.Count > 0
            ? device.Raw
            : new Dictionary<string, object?> { ["id"] = device.Id, ["name"] = device.Name };

        var deviceJson = JsonSerializer.Serialize(payload);
        var modeJson = JsonSerializer.Serialize(mode);
        var script = $$"""
            (() => {
              const p = window.NativePlayerComponent.Player();
              p.selectDevice({{deviceJson}}, {{modeJson}});
              return { ok: true };
            })()
            """;

        var result = await client.EvaluateAsync(script, cancellationToken: cancellationToken);
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        if (result.TryGetProperty("type", out var type) && type.GetString() == "object"
            && result.TryGetProperty("value", out var value) && value.TryGetProperty("ok", out var ok)
            && ok.GetBoolean())
        {
            return;
        }

        throw new InvalidOperationException($"selectDevice returned unexpected result: {result}");
    }

    private async Task<CdpWebSocketClient> ConnectToMainPageAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CdpTarget> targets;
        try
        {
            targets = await GetTargetsAsync(cancellationToken);
        }
        catch (Exception ex) when (IsCdpConnectionFailure(ex))
        {
            throw CreateConnectionUserException(ex);
        }

        var target = FindMainPageTarget(targets);
        if (target?.WebSocketDebuggerUrl is null)
        {
            throw CreateMissingTargetUserException();
        }

        try
        {
            return await CdpWebSocketClient.ConnectAsync(new Uri(target.WebSocketDebuggerUrl), cancellationToken);
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("TidalCdpService.ConnectToMainPageAsync", ex);
            throw new TidalCdpUserException(
                "Die Verbindung zu TIDAL konnte nicht hergestellt werden.\n\n" +
                "Bitte TIDAL schließen und „Automatisch starten“ verwenden.",
                ex);
        }
    }

    private async Task<IReadOnlyList<CdpTarget>> GetTargetsAsync(CancellationToken cancellationToken)
    {
        var json = await _http.GetStringAsync($"http://127.0.0.1:{_debugPort}/json/list", cancellationToken);
        return JsonSerializer.Deserialize<List<CdpTarget>>(json, JsonOptions) ?? [];
    }

    private static CdpTarget? FindMainPageTarget(IReadOnlyList<CdpTarget> targets) =>
        targets.FirstOrDefault(t =>
            t.Type == "page"
            && t.Title == "TIDAL"
            && (t.Url?.Contains("desktop.tidal.com", StringComparison.OrdinalIgnoreCase) ?? false))
        ?? targets.FirstOrDefault(t =>
            t.Type == "page"
            && (t.Url?.Contains("desktop.tidal.com", StringComparison.OrdinalIgnoreCase) ?? false));

    private TidalCdpUserException CreateConnectionUserException(Exception ex)
    {
        StartupLogger.LogException("TidalCdpService CDP connection", ex);

        if (TidalProcessService.IsTidalRunning())
        {
            return new TidalCdpUserException(
                "TIDAL läuft ohne Debug-Port. Bitte TIDAL schließen und „Automatisch starten“ verwenden.",
                ex);
        }

        return new TidalCdpUserException(
            "TIDAL wurde nicht mit den benötigten Startparametern gestartet.",
            ex);
    }

    private TidalCdpUserException CreateMissingTargetUserException()
    {
        if (TidalProcessService.IsTidalRunning())
        {
            return new TidalCdpUserException(
                "TIDAL läuft, aber die Desktop-Oberfläche ist noch nicht bereit.\n\n" +
                "Bitte warten, bis TIDAL vollständig geladen ist, und dann erneut versuchen.");
        }

        return new TidalCdpUserException(
            "TIDAL wurde nicht mit den benötigten Startparametern gestartet.");
    }

    private static bool IsCdpConnectionFailure(Exception ex) =>
        ex is HttpRequestException
            or TaskCanceledException
            or SocketException
            or IOException;

    private static IReadOnlyList<TidalDevice> ParseDevices(JsonElement result)
    {
        if (result.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("listDevices returned no value.");
        }

        if (!result.TryGetProperty("value", out var value))
        {
            throw new InvalidOperationException($"Unexpected listDevices result: {result}");
        }

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("target", out var target)
            && target.ValueKind == JsonValueKind.Array)
        {
            value = target;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("listDevices value is not an array.");
        }

        var devices = new List<TidalDevice>();
        foreach (var item in value.EnumerateArray())
        {
            devices.Add(ParseDevice(item));
        }

        return devices;
    }

    private static TidalDevice ParseDevice(JsonElement item)
    {
        var raw = new Dictionary<string, object?>();
        foreach (var prop in item.EnumerateObject())
        {
            raw[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                _ => prop.Value.GetRawText()
            };
        }

        var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
        var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? id : id;
        var type = item.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        var isDefault = item.TryGetProperty("default", out var defProp) && defProp.GetBoolean();

        return new TidalDevice
        {
            Id = id,
            Name = name,
            Type = type,
            IsDefault = isDefault,
            Raw = raw
        };
    }

    public void Dispose() => _http.Dispose();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class CdpTarget
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Type { get; set; }
        public string? WebSocketDebuggerUrl { get; set; }
    }
}
