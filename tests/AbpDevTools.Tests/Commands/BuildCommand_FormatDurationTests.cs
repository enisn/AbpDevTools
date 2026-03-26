using AbpDevTools.Commands;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands;

public class BuildCommand_FormatDurationTests
{
    [Theory]
    [InlineData(0, 0, "0.0s")]
    [InlineData(0, 500, "0.5s")]
    [InlineData(0, 499, "0.5s")]
    [InlineData(0, 50, "0.1s")]
    [InlineData(0, 100, "0.1s")]
    [InlineData(3, 200, "3.2s")]
    [InlineData(45, 0, "45.0s")]
    public void FormatDuration_UnderOneMinute_ReturnsSeconds(int seconds, int ms, string expected)
    {
        var elapsed = TimeSpan.FromMilliseconds(seconds * 1000 + ms);
        BuildCommand.FormatDuration(elapsed).Should().Be(expected);
    }

    [Fact]
    public void FormatDuration_RoundingTo60Seconds_ReturnsOneMinute()
    {
        var elapsed = TimeSpan.FromMilliseconds(59_950);
        BuildCommand.FormatDuration(elapsed).Should().Be("1m 00s");
    }

    [Theory]
    [InlineData(1, 0, "1m 00s")]
    [InlineData(1, 5, "1m 05s")]
    [InlineData(2, 15, "2m 15s")]
    [InlineData(59, 59, "59m 59s")]
    public void FormatDuration_MinutesRange_ReturnsMinutesAndSeconds(int minutes, int seconds, string expected)
    {
        var elapsed = TimeSpan.FromSeconds(minutes * 60 + seconds);
        BuildCommand.FormatDuration(elapsed).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 0, 0, "1h 00m 00s")]
    [InlineData(1, 5, 30, "1h 05m 30s")]
    [InlineData(2, 30, 45, "2h 30m 45s")]
    public void FormatDuration_HoursRange_ReturnsHoursMinutesAndSeconds(int hours, int minutes, int seconds, string expected)
    {
        var elapsed = TimeSpan.FromSeconds(hours * 3600 + minutes * 60 + seconds);
        BuildCommand.FormatDuration(elapsed).Should().Be(expected);
    }
}
