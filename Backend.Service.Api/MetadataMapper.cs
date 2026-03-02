#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Database.Main;
using ApiNft = Backend.Service.Api.Nft;
using ApiSeries = Backend.Service.Api.Series;
using DbNft = Database.Main.Nft;
using DbSeries = Database.Main.Series;

namespace Backend.Service.Api;

internal static class MetadataMapper
{
    private static void AddIfMissing(IDictionary<string, string> target, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            return;

        if (target.TryGetValue(key, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return;

        target[key] = value;
    }


    private static Dictionary<string, string> Extract(JsonDocument? metadata)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        if (metadata == null)
            return result;

        try
        {
            if (metadata.RootElement.ValueKind != JsonValueKind.Object)
                return result;

            foreach (var property in metadata.RootElement.EnumerateObject())
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


    private static Dictionary<string, string>? Normalize(Dictionary<string, string> metadata)
    {
        return metadata.Count > 0 ? metadata : null;
    }


    private static Dictionary<string, string>? BuildNftMetadata(JsonDocument? metadataDocument,
        NftMetadata? nftMetadata, string? creatorAddress, string? seriesId)
    {
        var metadata = Extract(metadataDocument);
        metadata.Remove("_i");
        metadata.Remove("series");

        if (nftMetadata != null)
        {
            AddIfMissing(metadata, "name", nftMetadata.name);
            AddIfMissing(metadata, "description", nftMetadata.description);
            AddIfMissing(metadata, "imageURL", nftMetadata.imageURL);
            AddIfMissing(metadata, "videoURL", nftMetadata.videoURL);
            AddIfMissing(metadata, "infoURL", nftMetadata.infoURL);
            AddIfMissing(metadata, "rom", nftMetadata.rom);
            AddIfMissing(metadata, "ram", nftMetadata.ram);
        }

        AddIfMissing(metadata, "creator", creatorAddress);

        if (!string.IsNullOrWhiteSpace(seriesId))
            AddIfMissing(metadata, "seriesId", seriesId);

        return Normalize(metadata);
    }


    public static Dictionary<string, string>? FromNft(DbNft nft)
    {
        if (nft == null)
            return null;

        var fallbackMetadata = new NftMetadata
        {
            name = nft.NAME,
            description = nft.DESCRIPTION,
            imageURL = nft.IMAGE,
            videoURL = nft.VIDEO,
            infoURL = nft.INFO_URL,
            rom = nft.ROM,
            ram = nft.RAM,
            mint_date = nft.MINT_DATE_UNIX_SECONDS > 0 ? nft.MINT_DATE_UNIX_SECONDS.ToString() : null,
            mint_number = nft.MINT_NUMBER > 0 ? nft.MINT_NUMBER.ToString() : null
        };

        var seriesId = nft.Series != null ? nft.Series.SERIES_ID : null;
        var creatorAddress = nft.CreatorAddress != null ? nft.CreatorAddress.ADDRESS : null;

        return BuildNftMetadata(nft.METADATA, fallbackMetadata, creatorAddress, seriesId);
    }


    public static Dictionary<string, string>? FromNft(JsonDocument? metadata, ApiNft apiNft)
    {
        if (apiNft == null)
            return null;

        var seriesId = apiNft.series?.series_id;
        if (string.IsNullOrWhiteSpace(seriesId) && apiNft.series != null && apiNft.series.id != 0)
            seriesId = apiNft.series.id.ToString(CultureInfo.InvariantCulture);

        return BuildNftMetadata(metadata, apiNft.nft_metadata, apiNft.creator_address, seriesId);
    }


    public static Dictionary<string, string>? FromNft(JsonDocument? metadata, NftMetadata nftMetadata,
        string? creatorAddress, string? seriesId)
    {
        return BuildNftMetadata(metadata, nftMetadata, creatorAddress, seriesId);
    }


    public static Dictionary<string, string>? FromSeries(DbSeries series)
    {
        if (series == null)
            return null;

        var metadata = Extract(series.METADATA);
        metadata.Remove("_i");
        AddIfMissing(metadata, "name", series.NAME);
        AddIfMissing(metadata, "description", series.DESCRIPTION);
        AddIfMissing(metadata, "imageURL", series.IMAGE);
        AddIfMissing(metadata, "royalties", series.ROYALTIES.ToString(CultureInfo.InvariantCulture));

        if (series.TYPE > 0)
            AddIfMissing(metadata, "type", series.TYPE.ToString(CultureInfo.InvariantCulture));

        AddIfMissing(metadata, "attrType1", series.ATTR_TYPE_1);
        AddIfMissing(metadata, "attrValue1", series.ATTR_VALUE_1);
        AddIfMissing(metadata, "attrType2", series.ATTR_TYPE_2);
        AddIfMissing(metadata, "attrValue2", series.ATTR_VALUE_2);
        AddIfMissing(metadata, "attrType3", series.ATTR_TYPE_3);
        AddIfMissing(metadata, "attrValue3", series.ATTR_VALUE_3);

        if (series.SeriesMode != null)
            AddIfMissing(metadata, "modeName", series.SeriesMode.MODE_NAME);

        if (series.CURRENT_SUPPLY > 0)
            AddIfMissing(metadata, "current_supply", series.CURRENT_SUPPLY.ToString(CultureInfo.InvariantCulture));

        if (series.MAX_SUPPLY > 0)
            AddIfMissing(metadata, "max_supply", series.MAX_SUPPLY.ToString(CultureInfo.InvariantCulture));

        if (series.CreatorAddress != null)
            AddIfMissing(metadata, "creator", series.CreatorAddress.ADDRESS);

        return Normalize(metadata);
    }


    public static Dictionary<string, string>? FromSeries(JsonDocument? metadata, ApiSeries apiSeries)
    {
        if (apiSeries == null)
            return null;

        var result = Extract(metadata);
        result.Remove("_i");
        AddIfMissing(result, "name", apiSeries.name);
        AddIfMissing(result, "description", apiSeries.description);
        AddIfMissing(result, "imageURL", apiSeries.image);
        AddIfMissing(result, "royalties", apiSeries.royalties);

        if (apiSeries.type != 0)
            AddIfMissing(result, "type", apiSeries.type.ToString());

        AddIfMissing(result, "attrType1", apiSeries.attr_type_1);
        AddIfMissing(result, "attrValue1", apiSeries.attr_value_1);
        AddIfMissing(result, "attrType2", apiSeries.attr_type_2);
        AddIfMissing(result, "attrValue2", apiSeries.attr_value_2);
        AddIfMissing(result, "attrType3", apiSeries.attr_type_3);
        AddIfMissing(result, "attrValue3", apiSeries.attr_value_3);
        AddIfMissing(result, "modeName", apiSeries.mode_name);
        AddIfMissing(result, "creator", apiSeries.creator);

        if (apiSeries.current_supply != 0)
            AddIfMissing(result, "current_supply",
                apiSeries.current_supply.ToString(CultureInfo.InvariantCulture));

        if (apiSeries.max_supply != 0)
            AddIfMissing(result, "max_supply", apiSeries.max_supply.ToString(CultureInfo.InvariantCulture));

        AddIfMissing(result, "creator", apiSeries.creator);

        return Normalize(result);
    }
}
