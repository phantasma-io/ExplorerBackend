using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Database.Main;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Vm;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin
{
    private static void AddIfNotEmpty(Dictionary<string, string> metadata, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return;

        metadata[key] = value.Trim();
    }


    private static Dictionary<string, string> ExtractMetadata(JsonDocument metadataDocument)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadataDocument == null)
            return result;

        try
        {
            if (metadataDocument.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in metadataDocument.RootElement.EnumerateObject())
            {
                var valueString = property.Value.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    JsonValueKind.String => property.Value.GetString(),
                    _ => property.Value.ToString()
                };

                if (string.IsNullOrWhiteSpace(valueString))
                    continue;

                result[property.Name] = valueString;
            }
        }
        catch
        {
            // ignored
        }

        return result;
    }


    private static Dictionary<string, string> ConvertPropertiesToMetadata(IEnumerable<TokenProperty> properties)
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

        if (properties == null)
            return metadata;

        foreach (var property in properties)
        {
            AddIfNotEmpty(metadata, property.Key, property.Value);
        }

        return metadata;
    }


    private static Dictionary<string, string> ConvertDictionaryToMetadata(IDictionary<string, string> values)
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

        if (values == null)
            return metadata;

        foreach (var (key, value) in values)
        {
            AddIfNotEmpty(metadata, key, value);
        }

        return metadata;
    }


    private static string GetVmValueString(VmDynamicVariable value)
    {
        try
        {
            return value.type switch
            {
                VmType.String => value.GetString(),
                VmType.Int8 => value.GetInt8().ToString(CultureInfo.InvariantCulture),
                VmType.Int16 => value.GetInt16().ToString(CultureInfo.InvariantCulture),
                VmType.Int32 => value.GetInt32().ToString(CultureInfo.InvariantCulture),
                VmType.Int64 => value.GetInt64().ToString(CultureInfo.InvariantCulture),
                VmType.Bytes => Base16.Encode(value.GetBytes()),
                _ => value.data?.ToString()
            } ?? string.Empty;
        }
        catch
        {
            return value.data?.ToString() ?? string.Empty;
        }
    }


    private static Dictionary<string, string> ConvertStructToMetadata(VmDynamicStruct? structData)
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

        if (!structData.HasValue)
            return metadata;

        foreach (var field in structData.Value.fields)
        {
            var fieldName = field.name.data?.ToString();
            var fieldValue = GetVmValueString(field.value);

            AddIfNotEmpty(metadata, fieldName, fieldValue);
        }

        return metadata;
    }


    private static Dictionary<string, string> CreateMetadataFromRomRam(byte[] romBytes, byte[] ramBytes)
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);

        if (romBytes is { Length: > 0 })
            AddIfNotEmpty(metadata, "rom", Base16.Encode(romBytes));

        if (ramBytes is { Length: > 0 })
            AddIfNotEmpty(metadata, "ram", Base16.Encode(ramBytes));

        return metadata;
    }


    private static void UpdateNftMetadata(Nft nft, params Dictionary<string, string>[] sources)
    {
        if (nft == null)
            return;

        var metadata = nft.METADATA != null
            ? ExtractMetadata(nft.METADATA)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.Where(s => s != null))
        {
            foreach (var (key, value) in source)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;

                metadata[key] = value;
            }
        }

        if (metadata.Count > 0)
            nft.METADATA = JsonSerializer.SerializeToDocument(metadata);
    }


    private static void UpdateSeriesMetadata(Series series, params Dictionary<string, string>[] sources)
    {
        if (series == null)
            return;

        var metadata = series.METADATA != null
            ? ExtractMetadata(series.METADATA)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.Where(s => s != null))
        {
            foreach (var (key, value) in source)
            {
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                    continue;

                metadata[key] = value;
            }
        }

        if (metadata.Count > 0)
            series.METADATA = JsonSerializer.SerializeToDocument(metadata);
    }
}
