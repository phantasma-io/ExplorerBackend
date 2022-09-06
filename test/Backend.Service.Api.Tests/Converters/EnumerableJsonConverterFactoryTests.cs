using System;
using System.Collections.Generic;
using Backend.Service.Api;
using Backend.Service.Api.Converters;
using Shouldly;
using Xunit;

namespace Backend.Service.Converters.Tests;

public class EnumerableJsonConverterFactoryTests
{
    [Theory]
    [InlineData(typeof(IEnumerable<Nft>))]
    [InlineData(typeof(IEnumerable<Event>))]
    public void CanConvert_should_return_true_when_array_type_is_provided(Type type)
    {
        // Arrange
        var sut = new EnumerableJsonConverterFactory();

        // Act
        var result = sut.CanConvert(type);

        // Assert
        result.ShouldBeTrue();
    }


    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(string[]))]
    public void CanConvert_should_return_false_when_non_array_type_is_provided(Type type)
    {
        // Arrange
        var sut = new EnumerableJsonConverterFactory();

        // Act
        var result = sut.CanConvert(type);

        // Assert
        result.ShouldBeFalse();
    }


    [Theory]
    [InlineData(typeof(IEnumerable<Nft>))]
    [InlineData(typeof(IEnumerable<Event>))]
    public void CreateConverter_should_return_converter(Type type)
    {
        // Arrange
        //var sut = new EnumerableJsonConverterFactory();

        // Act
        //var result = sut.CreateConverter(type, new JsonSerializerOptions());

        // Assert
        //result.ShouldNotBeNull();
    }
}
