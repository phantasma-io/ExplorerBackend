using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Xunit;

namespace GhostDevs.Service.Converters.Tests;

public class EnumerableJsonConverterTests
{
    private readonly JsonSerializerOptions _defaultOptions = new()
    {
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = {new EnumerableJsonConverterFactory()}
    };


    [Fact]
    public void Write_should_return_json_string_with_no_results_property()
    {
        // Arrange
        var apiResult = new TestApiResult {total_results = 0, assets = Array.Empty<TestAsset>()};

        // Act
        var result = JsonSerializer.Serialize(apiResult, _defaultOptions);

        // Assert
        result.ShouldBeEquivalentTo(@"{""total_results"":0}");
    }


    [Fact]
    public void Write_should_return_json_string_with_results_property()
    {
        // Arrange
        var apiResult = new TestApiResult
        {
            total_results = 2, assets = new[] {new TestAsset {name = "test1"}, new TestAsset {name = "test2"}}
        };

        // Act
        var result = JsonSerializer.Serialize(apiResult, _defaultOptions);

        // Assert
        result.ShouldBeEquivalentTo(@"{""assets"":[{""name"":""test1""},{""name"":""test2""}],""total_results"":2}");
    }


    [Fact]
    public void Read_should_return_object_with_no_results_when_enumerable_property_provided_with_null()
    {
        // Arrange
        var json = @"{""assets"":null,""total_results"":0}";

        // Act
        var result = JsonSerializer.Deserialize<TestApiResult>(json, _defaultOptions);

        // Assert
        result.total_results.ShouldBe(0);
        result.assets.ShouldBeNull();
    }


    [Fact]
    public void Read_should_return_object_with_no_results_when_enumerable_property_not_provided()
    {
        // Arrange
        var json = @"{""total_results"":0}";

        // Act
        var result = JsonSerializer.Deserialize<TestApiResult>(json, _defaultOptions);

        // Assert
        result.total_results.ShouldBe(0);
        result.assets.ShouldBeNull();
    }


    [Fact]
    public void Read_should_return_object_with_results()
    {
        // Arrange
        var json = @"{""assets"":[{""name"":""test1""},{""name"":""test2""}],""total_results"":2}";

        // Act
        var result = JsonSerializer.Deserialize<TestApiResult>(json, _defaultOptions);

        // Assert
        result.total_results.ShouldBe(2);
        result.assets.ShouldNotBeNull();
        result.assets.ShouldContain(a => a.name == "test1");
        result.assets.ShouldContain(a => a.name == "test2");
    }


    internal struct TestApiResult
    {
        public IEnumerable<TestAsset> assets;
        public long total_results;
    }

    internal struct TestAsset
    {
        public string name { get; set; }
    }
}
