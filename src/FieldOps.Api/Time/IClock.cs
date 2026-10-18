using System;

namespace FieldOps.Api.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
