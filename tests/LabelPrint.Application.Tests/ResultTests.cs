using FluentAssertions;
using LabelPrint.Application.Common;

namespace LabelPrint.Application.Tests;

public class ResultTests
{
    [Fact]
    public void Success_Exposes_Value()
    {
        var result = Result.Success(42);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_Throws_On_Value_Access()
    {
        var result = Result.Failure<int>("broken");
        result.IsFailure.Should().BeTrue();
        var act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
