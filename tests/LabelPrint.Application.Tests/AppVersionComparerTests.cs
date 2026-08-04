using FluentAssertions;
using LabelPrint.Application.Updates;
using Xunit;

namespace LabelPrint.Application.Tests;

public sealed class AppVersionComparerTests
{
    [Theory]
    [InlineData("v0.9.0", "0.8.0", true)]
    [InlineData("0.8.1", "0.8.0", true)]
    [InlineData("0.8.0", "0.8.0", false)]
    [InlineData("0.7.9", "0.8.0", false)]
    [InlineData("v1.0.0-beta", "0.9.0", true)]
    public void IsNewer_compares_semver_core(string latest, string current, bool expected) =>
        AppVersionComparer.IsNewer(latest, current).Should().Be(expected);

    [Theory]
    [InlineData("v0.8.0", "0.8.0")]
    [InlineData("0.9.0-rc.1", "0.9.0")]
    public void Normalize_strips_prefix_and_prerelease(string input, string expected) =>
        AppVersionComparer.Normalize(input).Should().Be(expected);
}
