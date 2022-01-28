using System;
using System.Collections.Generic;
using System.Linq;
using Database.Main;
using LunarLabs.Parser;
using Phantasma.Numerics;
using Phantasma.VM;
using Serilog;

namespace GhostDevs.Blockchain;

internal class Utils
{
    private static void PrintAuctionsMaxFieldLengths(DataNode auctions)
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

        Log.Information($"creatorAddressMaxLength: {creatorAddressMaxLength}\n" +
                        $"chainAddressMaxLength: {chainAddressMaxLength}\n" +
                        $"baseSymbolMaxLength: {baseSymbolMaxLength}\n" +
                        $"quoteSymbolMaxLength: {quoteSymbolMaxLength}\n" +
                        $"tokenIdMaxLength: {tokenIdMaxLength}\n" +
                        $"priceMaxLength: {priceMaxLength}\n" +
                        $"romMaxLength: {romMaxLength}\n" +
                        $"ramMaxLength: {ramMaxLength}");
    }


    public static List<string> GetInstructionsFromScript(string scriptRaw)
    {
        var disassembler = new Disassembler(scriptRaw.Decode());
        var instructions = disassembler.Instructions.ToList();
        return instructions.Select(instruction => instruction.ToString()).ToList();
    }


    public static Event CreateEvent(MainDbContext context, out bool eventCreated, uint timestamp,
        int index, int chainId, Transaction transaction, int contractId, int eventKindId, string address)
    {
        var eventItem = EventMethods.Upsert(context,
            out var created,
            timestamp,
            index,
            chainId,
            transaction,
            contractId,
            eventKindId,
            address);
        eventCreated = created;
        return eventItem;
    }
}
