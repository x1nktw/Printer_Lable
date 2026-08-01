using LabelPrint.Infrastructure.FrontPad.Api;
using Xunit;

namespace LabelPrint.Infrastructure.Tests;

public sealed class FrontPadApiTests
{
    [Theory]
    [InlineData("invalid_secret", "Неверный секрет")]
    [InlineData("api_off", "выключен")]
    [InlineData("invalid_plant", "тарифе")]
    [InlineData("requests_limit", "лимит")]
    public void MapApiError_translates_known_codes(string code, string expectedFragment)
    {
        var message = FrontPadApiClient.MapApiError(code);
        Assert.Contains(expectedFragment, message, StringComparison.OrdinalIgnoreCase);
    }
}
