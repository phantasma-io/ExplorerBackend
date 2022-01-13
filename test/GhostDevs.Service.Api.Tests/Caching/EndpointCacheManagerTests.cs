using System.Collections.Generic;
using System.Threading.Tasks;
using Foundatio.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Shouldly;
using Xunit;

namespace GhostDevs.Service.Caching.Tests;

public class EndpointCacheManagerTests
{
    private readonly EndpointCacheManager _cacheManager;


    public EndpointCacheManagerTests()
    {
        _cacheManager = new EndpointCacheManager(new NullLogger<EndpointCacheManager>(),
            new InMemoryCacheClient(builder => builder.CloneValues(true)));
    }


    [Theory]
    [InlineData(0)]
    public async Task Add_should_return_false_when_duration_is_zero(int duration)
    {
        // Act
        var result = await _cacheManager.Add("cachkey", @"{""test"":""content""}", duration);

        // Assert
        result.ShouldBeFalse();
    }


    [Theory]
    [InlineData(10)]
    [InlineData(-10)]
    public async Task Add_should_return_true_when_duration_is_non_zero(int duration)
    {
        // Act
        var result = await _cacheManager.Add("cachkey", @"{""test"":""content""}", duration);

        // Assert
        result.ShouldBeTrue();
    }


    [Fact]
    public async Task Get_should_not_return_cache_content_when_data_is_not_cached()
    {
        // Arrange
        var route = "/api/v1/test";
        var queryParameters = new List<KeyValuePair<string, StringValues>>
        {
            new("q", new StringValues("test")), new("p", new StringValues(new[] {"3", "1", "2"}))
        };
        var tag = "test";

        // Act
        var result = await _cacheManager.Get(route, queryParameters, tag);

        // Assert
        result.ShouldNotBeNull();
        result.Cached.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.Key.ShouldNotBeEmpty();
    }


    [Fact]
    public async Task Get_should_return_cache_content_when_data_is_cached()
    {
        // Arrange
        var route = "/api/v1/test";
        var queryParameters = new List<KeyValuePair<string, StringValues>>
        {
            new("q", new StringValues("test")), new("p", new StringValues(new[] {"3", "1", "2"}))
        };
        var tag = "test";
        await _cacheManager.Add("/api/v1/test/[p, 1,2,3]/[q, test]", @"{""test"":""data""}", 10, tag);

        // Act
        var result = await _cacheManager.Get(route, queryParameters, tag);

        // Assert
        result.ShouldNotBeNull();
        result.Cached.ShouldBeTrue();
        result.Content.ShouldBe(@"{""test"":""data""}");
        result.Key.ShouldBe("/api/v1/test/[p, 1,2,3]/[q, test]");
    }


    [Fact]
    public async Task Invalidate_should_remove_cache_for_tag()
    {
        // Arrange
        var route = "/api/v1/test";
        var queryParameters = new List<KeyValuePair<string, StringValues>>
        {
            new("q", new StringValues("test")), new("p", new StringValues(new[] {"3", "1", "2"}))
        };
        var tag = "test";
        await _cacheManager.Add("/api/v1/test/[p, 1,2,3]/[q, test]", @"{""test"":""data""}", 10, tag);

        // Act
        await _cacheManager.Invalidate(tag);
        var result = await _cacheManager.Get(route, queryParameters, tag);

        // Assert
        result.ShouldNotBeNull();
        result.Cached.ShouldBeFalse();
        result.Content.ShouldBeNull();
        result.Key.ShouldNotBeEmpty();
    }


    [Fact]
    public async Task Invalidate_should_remove_cache_for_tag1_but_keep_tag2()
    {
        // Arrange
        var route1 = "/api/v1/user";
        var queryParameters1 =
            new List<KeyValuePair<string, StringValues>> {new("id", new StringValues("123"))};
        var tag1 = "user";
        var route2 = "/api/v1/assets";
        var queryParameters2 =
            new List<KeyValuePair<string, StringValues>> {new("p", new StringValues("10"))};
        var tag2 = "assets";
        await _cacheManager.Add("/api/v1/user/[id, 123]", @"{""username"":""test""}", 10, tag1);
        await _cacheManager.Add("/api/v1/assets/[p, 10]", @"{""test"":""data""}", 10, tag2);

        // Act
        await _cacheManager.Invalidate(tag1);
        var result1 = await _cacheManager.Get(route1, queryParameters1, tag1);
        var result2 = await _cacheManager.Get(route2, queryParameters2, tag2);

        // Assert
        result1.ShouldNotBeNull();
        result1.Cached.ShouldBeFalse();
        result1.Content.ShouldBeNull();
        result1.Key.ShouldNotBeEmpty();
        result2.ShouldNotBeNull();
        result2.Cached.ShouldBeTrue();
        result2.Content.ShouldBe(@"{""test"":""data""}");
        result2.Key.ShouldBe("/api/v1/assets/[p, 10]");
    }
}
