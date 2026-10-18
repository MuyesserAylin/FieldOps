using FieldOps.Api.Time;

namespace FieldOps.Api.Tests;

public class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; }
}