using System;
using System.Linq;
using System.Text.Json;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Protocol.ExtendedEvents;
using PhantasmaPhoenix.RPC.Models;
using Serilog;

namespace Backend.Blockchain;

internal static class ExtendedEventParser
{
    private static readonly JsonSerializerOptions TokenCreateJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    public static TokenCreateData? GetTokenCreateData(EventExResult[] extendedEvents)
    {
        if (extendedEvents == null || extendedEvents.Length == 0)
            return null;

        var tokenCreateEvent = extendedEvents.FirstOrDefault(x => x.Kind == EventKind.TokenCreate);

        if (tokenCreateEvent == null)
            return null;

        try
        {
            switch (tokenCreateEvent.Data)
            {
                case JsonElement el:
                    return JsonSerializer.Deserialize<TokenCreateData>(el.GetRawText(), TokenCreateJsonOptions);
                case string s when !string.IsNullOrWhiteSpace(s):
                    return JsonSerializer.Deserialize<TokenCreateData>(s, TokenCreateJsonOptions);
                case TokenCreateData data:
                    return data;
                default:
                    return null;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "[ExtendedEventParser] Failed to parse TokenCreateData from extended event");
            return null;
        }
    }

    public static TokenSeriesCreateData? GetTokenSeriesCreateData(EventExResult[] extendedEvents)
    {
        if (extendedEvents == null || extendedEvents.Length == 0)
            return null;

        var seriesCreateEvent = extendedEvents.FirstOrDefault(x => x.Kind == EventKind.TokenSeriesCreate);

        if (seriesCreateEvent == null)
            return null;

        try
        {
            switch (seriesCreateEvent.Data)
            {
                case JsonElement el:
                    return JsonSerializer.Deserialize<TokenSeriesCreateData>(el.GetRawText(), TokenCreateJsonOptions);
                case string s when !string.IsNullOrWhiteSpace(s):
                    return JsonSerializer.Deserialize<TokenSeriesCreateData>(s, TokenCreateJsonOptions);
                case TokenSeriesCreateData data:
                    return data;
                default:
                    return null;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "[ExtendedEventParser] Failed to parse TokenSeriesCreateData from extended event");
            return null;
        }
    }

    public static TokenMintData? GetTokenMintData(EventExResult[] extendedEvents)
    {
        if (extendedEvents == null || extendedEvents.Length == 0)
            return null;

        var mintEvent = extendedEvents.FirstOrDefault(x => x.Kind == EventKind.TokenMint);

        if (mintEvent == null)
            return null;

        try
        {
            switch (mintEvent.Data)
            {
                case JsonElement el:
                    return JsonSerializer.Deserialize<TokenMintData>(el.GetRawText(), TokenCreateJsonOptions);
                case string s when !string.IsNullOrWhiteSpace(s):
                    return JsonSerializer.Deserialize<TokenMintData>(s, TokenCreateJsonOptions);
                case TokenMintData data:
                    return data;
                default:
                    return null;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "[ExtendedEventParser] Failed to parse TokenMintData from extended event");
            return null;
        }
    }

    public static SpecialResolutionData? GetSpecialResolutionData(EventExResult[] extendedEvents)
    {
        if (extendedEvents == null || extendedEvents.Length == 0)
            return null;

        var specialResolutionEvent = extendedEvents.FirstOrDefault(x => x.Kind == EventKind.SpecialResolution);

        if (specialResolutionEvent == null)
            return null;

        try
        {
            switch (specialResolutionEvent.Data)
            {
                case JsonElement el:
                    return JsonSerializer.Deserialize<SpecialResolutionData>(el.GetRawText(), TokenCreateJsonOptions);
                case string s when !string.IsNullOrWhiteSpace(s):
                    return JsonSerializer.Deserialize<SpecialResolutionData>(s, TokenCreateJsonOptions);
                case SpecialResolutionData data:
                    return data;
                default:
                    return null;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "[ExtendedEventParser] Failed to parse SpecialResolutionData from extended event");
            return null;
        }
    }
}
