using Backend.Service.Api;
using Shouldly;
using Xunit;

namespace Backend.Service.Api.Tests.Legacy;

public class EventPayloadMapperTests
{
    [Fact]
    public void TryDecodeLegacyTokenEventPayload_should_decode_token_claim_raw_data()
    {
        var ok = EventPayloadMapper.TryDecodeLegacyTokenEventPayload(
            "TokenClaim",
            "044B43414C0500B4C40400046D61696E",
            out var decoded
        );

        ok.ShouldBeTrue();
        decoded.Token.ShouldBe("KCAL");
        decoded.ValueRaw.ShouldNotBeNullOrWhiteSpace();
        decoded.ChainName.ShouldBe("main");
    }

    [Fact]
    public void TryDecodeLegacyTokenEventPayload_should_decode_token_mint_raw_data()
    {
        var ok = EventPayloadMapper.TryDecodeLegacyTokenEventPayload(
            "TokenMint",
            "044B43414C0800B34ACF095D0300046D61696E",
            out var decoded
        );

        ok.ShouldBeTrue();
        decoded.Token.ShouldBe("KCAL");
        decoded.ValueRaw.ShouldNotBeNullOrWhiteSpace();
        decoded.ChainName.ShouldBe("main");
    }

    [Fact]
    public void TryDecodeLegacyTokenEventPayload_should_reject_unsupported_kind()
    {
        var ok = EventPayloadMapper.TryDecodeLegacyTokenEventPayload(
            "GasPayment",
            "044B43414C0500B4C40400046D61696E",
            out var decoded
        );

        ok.ShouldBeFalse();
        decoded.ShouldBe(default);
    }

    [Fact]
    public void NormalizeLegacyTokenIdText_should_keep_positive_values()
    {
        var normalized = EventPayloadMapper.NormalizeLegacyTokenIdText("12345");

        normalized.ShouldBe("12345");
    }

    [Fact]
    public void NormalizeLegacyTokenIdText_should_convert_negative_values_to_unsigned_256bit()
    {
        var normalized = EventPayloadMapper.NormalizeLegacyTokenIdText("-1");

        normalized.ShouldBe("115792089237316195423570985008687907853269984665640564039457584007913129639935");
    }
}
