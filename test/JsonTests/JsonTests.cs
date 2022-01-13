using System.Linq;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace GhostDevs.Tests;

public class JsonTests
{
    [Fact]
    public void Pha_auctions_chain_response_error_should_be_extracted_properly()
    {
        // Arrange
        var phaChainResponse =
            @"{""error"" : ""Constraint failed: nft GHOST 88952507113204945896685671555612179007301652577970006258682895062464815798701 does not exist""}";
        var node = JsonDocument.Parse(phaChainResponse);

        // Act
        var result = "";
        if ( node.RootElement.TryGetProperty("error", out var property) ) result = property.GetString();

        // Assert
        result.ShouldContain("does not exist");
    }


    [Fact]
    public void Pha_auctions_chain_response_should_be_parsed_properly()
    {
        // Arrange
        var phaChainResponse =
            @"{""creatorAddress"" : ""P2KCrRRGie8REQsNrDWPA6f82XuFiyMtisVc9XXgits77DS"",""chainAddress"" : ""S3d7TbZxtNPdXy11hfmBLJLYn67gZTG2ibL7fJBcVdihWU4"",""startDate"" : 1636498039,""endDate"" : 1639090030,""baseSymbol"" : ""GHOST"",""quoteSymbol"" : ""SOUL"",""tokenId"" : ""88952507113204945896685671555612179007301652577970006258682895062464815798700"",""price"" : ""2500000000"",""endPrice"" : ""0"",""extensionPeriod"" : ""0"",""type"" : ""Fixed"",""rom"" : ""010E0407637265617465640577FA8A610409726F79616C7469657303020A0004076E6674547970650302010004046E616D65040D5363617272792042616E616E61040B6465736372697074696F6E049054686520666972737420696D616765206F662074686520756E697175652073657269657320697320746865207363617272792062616E616E612E20616C6C2068616E6420647261776E20616E64206E6F2067656E657261746F72206170706C69636174696F6E207573656420616E6420726561647920746F2061646420746F20796F757220636F6C6C656374696F6E2E0408696D61676555524C0435697066733A2F2F516D62734145335434546271704D70793458336A78334136326539676163316E71613561644A69386F52706639570407696E666F55524C040004066174747254310400040661747472563104000406617474725432040004066174747256320400040661747472543304000406617474725633040004096861734C6F636B65640600"",""ram"" : """",""listingFee"" : ""0"",""currentWinner"" : """"}";
        var node = JsonDocument.Parse(phaChainResponse);

        // Act
        var startDate = node.RootElement.GetProperty("startDate").GetInt32();
        var endDate = node.RootElement.GetProperty("endDate").GetInt32();
        var type = node.RootElement.GetProperty("type").GetString();
        var quoteSymbol = node.RootElement.GetProperty("quoteSymbol").GetString();
        var price = node.RootElement.GetProperty("price").GetString();
        var endPrice = node.RootElement.GetProperty("endPrice").GetString();
        var extensionPeriod = int.Parse(node.RootElement.GetProperty("extensionPeriod").GetString());
        var listingFee = int.Parse(node.RootElement.GetProperty("listingFee").GetString()) * 100;
        var currentWinner = node.RootElement.GetProperty("currentWinner").GetString();

        // Assert
        startDate.ShouldBeEquivalentTo(1636498039);
        endDate.ShouldBeEquivalentTo(1639090030);

        type.ShouldBeEquivalentTo("Fixed");
        quoteSymbol.ShouldBeEquivalentTo("SOUL");
        price.ShouldBeEquivalentTo("2500000000");
        endPrice.ShouldBeEquivalentTo("0");
        extensionPeriod.ShouldBeEquivalentTo(0);
        listingFee.ShouldBeEquivalentTo(0);
        currentWinner.ShouldBeEquivalentTo("");
    }


    [Fact]
    public void Pha_getBlockByHeight_parsing_working()
    {
        // Arrange
        var phaChainResponse =
            @"{""hash"" : ""B3BB661FBC58B1E6E2CDD9F02A86BD4A32F0B180C5F1553C1B55281E8C9D8E05"",""previousHash"" : ""664E69067C2249AC95827D66552FED1572A66B9B5E2795653B9158B80E42741A"",""timestamp"" : 1580746293,""height"" : 20000,""chainAddress"" : ""S3d7TbZxtNPdXy11hfmBLJLYn67gZTG2ibL7fJBcVdihWU4"",""protocol"" : 1,""txs"" : [{""hash"" : ""CC94739D526AD2E0CDA5B182528620315B04EBD124069EC22F8C930A88D9398B"",""chainAddress"" : ""S3d7TbZxtNPdXy11hfmBLJLYn67gZTG2ibL7fJBcVdihWU4"",""timestamp"" : 1580746293,""blockHeight"" : 20000,""blockHash"" : ""B3BB661FBC58B1E6E2CDD9F02A86BD4A32F0B180C5F1553C1B55281E8C9D8E05"",""script"" : ""0D00030320030003000D000304A086010003000D000223220000000000000000000000000000000000000000000000000000000000000000000003000D000223220100646803129686C6D228B54F2263D6D9CB01489E27EFF3D7E08529D80CC7923E8003000D000408416C6C6F7747617303000D0004036761732D00012E010D000306001EDC0C170003000D000223220100646803129686C6D228B54F2263D6D9CB01489E27EFF3D7E08529D80CC7923E8003000D000407556E7374616B6503000D0004057374616B652D00012E010D000223220100646803129686C6D228B54F2263D6D9CB01489E27EFF3D7E08529D80CC7923E8003000D0004085370656E6447617303000D0004036761732D00012E010B"",""payload"" : ""504754312E32"",""events"" : [{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""TokenStake"",""data"" : ""044B43414C0500B4C40400046D61696E""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""GasEscrow"",""data"" : ""2202000D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE360504A086010003200300""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""stake"",""kind"" : ""TokenClaim"",""data"" : ""04534F554C06001EDC0C1700046D61696E""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""GasPayment"",""data"" : ""2202000D6E4079E36703EBD37C00722F5891D28B0E2811DC114B129215123ADCCE360504A086010003BD0100""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""TokenClaim"",""data"" : ""044B43414C0500B4C40400046D61696E""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""TokenBurn"",""data"" : ""044B43414C02DE00046D61696E""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""TokenStake"",""data"" : ""044B43414C04605FA900046D61696E""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""CrownRewards"",""data"" : ""044B43414C04605FA900046D61696E""},{""address"" : ""P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R"",""contract"" : ""gas"",""kind"" : ""TokenSend"",""data"" : ""044B43414C0400E6AA00046D61696E""},{""address"" : ""S3dBVkyE9kdfbBjh7HMEr1BfPTg53CeSWaj3srYzBTZ4vyK"",""contract"" : ""gas"",""kind"" : ""TokenReceive"",""data"" : ""044B43414C0400E6AA00046D61696E""}],""result"" : """",""fee"" : ""44500000"",""signatures"" : [{""Kind"" : ""Ed25519"",""Data"" : ""406ECD3F0F9F094F60257A8354A4022738983B0C281945F45C9CDED70FD34C06F0C6215424E492C0C714D391ED76E18A9F00EBECA797D5D22490B92BE32151CE0A""}],""expiration"" : 1580747493}],""validatorAddress"" : ""P2KFNXEbt65rQiWqogAzqkVGMqFirPmqPw8mQyxvRKsrXV8"",""reward"" : ""0"",""events"" : [{""address"" : ""P2KFNXEbt65rQiWqogAzqkVGMqFirPmqPw8mQyxvRKsrXV8"",""contract"" : ""block"",""kind"" : ""TokenClaim"",""data"" : ""044B43414C0400E6AA00046D61696E""}]}";
        var node = JsonDocument.Parse(phaChainResponse);

        // Act
        var timestampUnixSeconds = node.RootElement.GetProperty("timestamp").GetUInt32();
        var address = "";

        if ( node.RootElement.TryGetProperty("txs", out var txsProperty) )
        {
            var txs = txsProperty.EnumerateArray();
            for ( var txIndex = 0; txIndex < txs.Count(); txIndex++ )
            {
                var tx = txs.ElementAt(txIndex);

                var events = new JsonElement.ArrayEnumerator();
                if ( tx.TryGetProperty("events", out var eventsProperty) ) events = eventsProperty.EnumerateArray();

                for ( var eventIndex = 0; eventIndex < events.Count(); eventIndex++ )
                {
                    var eventNode = events.ElementAt(eventIndex);
                    address = eventNode.GetProperty("address").GetString();
                    break;
                }

                break;
            }
        }

        // Assert
        timestampUnixSeconds.ShouldBeEquivalentTo(( uint ) 1580746293);

        address.ShouldBeEquivalentTo("P2KAY2e4bTxEQoLhdcYcH4y7JMZahHnPo2VtjQGiqjxkD9R");
    }


    [Fact]
    public void Rpc_request_serialization_works_correctly()
    {
        // Arrange

        object[] parameters = {"par1", "par2"};

        // System.Text.Json
        var rpcRequest = new RpcRequest
        {
            jsonrpc = "2.0", method = "testMethod", id = "1", @params = parameters.ToArray()
        };

        // Act
        var json = JsonSerializer.Serialize(rpcRequest);

        // Assert
        json.ShouldBeEquivalentTo(
            @"{""jsonrpc"":""2.0"",""method"":""testMethod"",""id"":""1"",""params"":[""par1"",""par2""]}");
    }


    private class RpcRequest
    {
        public string jsonrpc { get; set; }
        public string method { get; set; }
        public string id { get; set; }
        public object[] @params { get; set; }
    }
}
