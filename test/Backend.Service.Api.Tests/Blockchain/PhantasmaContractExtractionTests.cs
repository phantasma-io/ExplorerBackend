using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Backend.Blockchain;
using PhantasmaPhoenix.Core.Extensions;
using PhantasmaPhoenix.Protocol;
using PhantasmaPhoenix.Protocol.ExtendedEvents;
using Shouldly;
using Xunit;
using ProtocolEventKind = PhantasmaPhoenix.Protocol.EventKind;
using RpcEventExResult = PhantasmaPhoenix.RPC.Models.EventExResult;
using RpcEventResult = PhantasmaPhoenix.RPC.Models.EventResult;
using RpcTransactionResult = PhantasmaPhoenix.RPC.Models.TransactionResult;

namespace Backend.Service.Api.Tests.Blockchain;

public sealed class PhantasmaContractExtractionTests
{
    [Fact]
    public void TokenCreateUsesExtendedSymbolWhenLegacyPayloadIsMalformed()
    {
        // Carbon TokenCreate semantics are carried by ExtendedEvents; malformed
        // legacy compatibility payloads must not leak null contract keys into DB writes.
        var tx = new RpcTransactionResult
        {
            Hash = "tx-empty-legacy-token-create",
            Events =
            [
                new RpcEventResult("P2K-address", "token", ProtocolEventKind.TokenCreate.ToString(), "TokenCreate", "")
            ],
            ExtendedEvents =
            [
                new RpcEventExResult("P2K-address", "token", ProtocolEventKind.TokenCreate,
                    new TokenCreateData("EXTTOK", "1000", 8, false, 42, new Dictionary<string, string>()))
            ]
        };
        tx.ParseData(BigInteger.One);

        var contracts = new[] { tx }.GetContracts();

        contracts.ShouldContain("token");
        contracts.ShouldContain("EXTTOK");
        contracts.ShouldNotContain((string)null!);
    }

    [Fact]
    public void TokenCreateExtendedSymbolOverridesLegacyCompatibilitySymbol()
    {
        // When both payloads exist, the extended payload is canonical for Carbon
        // token creation; legacy EventResult.Data remains only a compatibility envelope.
        var legacyData = new TokenEventData("LEGACY", BigInteger.Zero, "main").Serialize().ToHex();
        var tx = new RpcTransactionResult
        {
            Hash = "tx-conflicting-token-create",
            Events =
            [
                new RpcEventResult("P2K-address", "token", ProtocolEventKind.TokenCreate.ToString(), "TokenCreate", legacyData)
            ],
            ExtendedEvents =
            [
                new RpcEventExResult("P2K-address", "token", ProtocolEventKind.TokenCreate,
                    new TokenCreateData("CANON", "1000", 8, false, 77, new Dictionary<string, string>()))
            ]
        };
        tx.ParseData(BigInteger.One);

        var contracts = new[] { tx }.GetContracts();

        contracts.ShouldContain("token");
        contracts.ShouldContain("CANON");
        contracts.ShouldNotContain("LEGACY");
    }

    [Fact]
    public void TokenCreateFallsBackToLegacySymbolWhenExtendedPayloadIsAbsent()
    {
        // Historical legacy transactions do not have ExtendedEvents, so their
        // TokenCreate symbol still has to come from the legacy serialized payload.
        var legacyData = new TokenEventData("LEGACY", BigInteger.Zero, "main").Serialize().ToHex();
        var tx = new RpcTransactionResult
        {
            Hash = "tx-legacy-token-create",
            Events =
            [
                new RpcEventResult("P2K-address", "token", ProtocolEventKind.TokenCreate.ToString(), "TokenCreate", legacyData)
            ],
            ExtendedEvents = []
        };
        tx.ParseData(BigInteger.One);

        var contracts = new[] { tx }.GetContracts();

        contracts.ShouldContain("token");
        contracts.ShouldContain("LEGACY");
    }

    [Fact]
    public void TokenCreateWithoutExtendedPayloadRejectsMalformedLegacySymbol()
    {
        var tx = new RpcTransactionResult
        {
            Hash = "tx-empty-legacy-without-extended",
            Events =
            [
                new RpcEventResult("P2K-address", "token", ProtocolEventKind.TokenCreate.ToString(), "TokenCreate", "")
            ],
            ExtendedEvents = []
        };
        tx.ParseData(BigInteger.One);

        var ex = Should.Throw<InvalidOperationException>(() => new[] { tx }.GetContracts());

        ex.Message.ShouldContain("TokenCreate legacy symbol");
        ex.Message.ShouldContain("tx-empty-legacy-without-extended");
    }
}
