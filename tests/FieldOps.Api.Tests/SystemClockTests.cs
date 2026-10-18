using System;
using FieldOps.Api.Time;

namespace FieldOps.Api.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_HasZeroOffset()
    {
        var clock = new SystemClock();

        Assert.Equal(TimeSpan.Zero, clock.UtcNow.Offset);
    }
}
