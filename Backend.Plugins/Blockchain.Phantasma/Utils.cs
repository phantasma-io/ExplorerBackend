using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Phantasma.Business.VM;
using Phantasma.Core.Numerics;
using Serilog;

namespace Backend.Blockchain;

internal static class Utils
{
    /*private static void PrintAuctionsMaxFieldLengths(DataNode auctions)
    {
        if ( auctions == null ) return;

        var auctionsArray = auctions.GetNode("result");

        var creatorAddressMaxLength = 0;
        var chainAddressMaxLength = 0;
        var baseSymbolMaxLength = 0;
        var quoteSymbolMaxLength = 0;
        var tokenIdMaxLength = 0;
        var priceMaxLength = 0;
        var romMaxLength = 0;
        var ramMaxLength = 0;

        foreach ( var auction in auctionsArray.Children )
        {
            var creatorAddress = auction.GetString("creatorAddress");
            creatorAddressMaxLength = Math.Max(creatorAddressMaxLength, creatorAddress.Length);

            var chainAddress = auction.GetString("chainAddress");
            chainAddressMaxLength = Math.Max(chainAddressMaxLength, chainAddress.Length);

            var baseSymbol = auction.GetString("baseSymbol");
            baseSymbolMaxLength = Math.Max(baseSymbolMaxLength, baseSymbol.Length);

            var quoteSymbol = auction.GetString("quoteSymbol");
            quoteSymbolMaxLength = Math.Max(quoteSymbolMaxLength, quoteSymbol.Length);

            var tokenId = auction.GetString("tokenId");
            tokenIdMaxLength = Math.Max(tokenIdMaxLength, tokenId.Length);

            var price = auction.GetString("price");
            priceMaxLength = Math.Max(priceMaxLength, price.Length);

            var rom = auction.GetString("rom");
            romMaxLength = Math.Max(romMaxLength, rom.Length);

            var ram = auction.GetString("ram");
            ramMaxLength = Math.Max(ramMaxLength, ram.Length);
        }

        Log.Information("creatorAddressMaxLength: {CreatorAddressMaxLength}\n" +
                        "chainAddressMaxLength: {ChainAddressMaxLength}\n" +
                        "baseSymbolMaxLength: {BaseSymbolMaxLength}\n" +
                        "quoteSymbolMaxLength: {QuoteSymbolMaxLength}\n" +
                        "tokenIdMaxLength: {TokenIdMaxLength}\n" +
                        "priceMaxLength: {PriceMaxLength}\n" +
                        "romMaxLength: {RomMaxLength}\n" +
                        "ramMaxLength: {RamMaxLength}", creatorAddressMaxLength, chainAddressMaxLength,
            baseSymbolMaxLength, quoteSymbolMaxLength, tokenIdMaxLength, priceMaxLength, romMaxLength, ramMaxLength);
    }*/


    public static List<string> GetInstructionsFromScript(string scriptRaw)
    {
        var disassembler = new Disassembler(scriptRaw.Decode());
        var instructions = disassembler.Instructions.ToList();
        return instructions.Select(instruction => instruction.ToString()).ToList();
    }


    public static void ReplaceCharacter(ref string stringResponse, ref JsonDocument response, string toReplace,
        string logString)
    {
        if ( !stringResponse.Contains(toReplace) ) return;

        stringResponse = stringResponse.Replace(toReplace, "");

        Log.Verbose("[{Name}] Hack tried to remove {Chars}", logString, toReplace);

        try
        {
            response = JsonDocument.Parse(stringResponse);
        }
        catch ( Exception e )
        {
            Log.Error(
                "[{Name}] hack tried to replace not set unicode character, parsing error:\n{Message}\nHacked response: {StringResponse}",
                logString, e.Message, stringResponse);
        }
    }
}
