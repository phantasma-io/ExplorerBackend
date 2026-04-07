using Backend.Service.Api;
using Shouldly;
using Xunit;

namespace Backend.Service.Api.Tests.Legacy;

public class EventPayloadVisibilityTests
{
    [Fact]
    public void RedactRawPayloadFields_should_strip_raw_payload_fields_from_list_results()
    {
        var projection = new EventPayloadMapper.EventProjection
        {
            ApiEvent = new Event
            {
                payload_json = "{\"special_resolution_event\":{\"resolution_id\":\"35\"}}",
                raw_data = "DEADBEEF",
                unknown_event = new UnknownEvent
                {
                    payload_json = "{\"raw\":true}",
                    raw_data = "CAFEBABE"
                }
            }
        };

        EventPayloadMapper.RedactRawPayloadFields(new[] { projection });

        projection.ApiEvent.payload_json.ShouldBeNull();
        projection.ApiEvent.raw_data.ShouldBeNull();
        projection.ApiEvent.unknown_event.ShouldBeNull();
    }
}
