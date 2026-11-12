using System;

namespace FieldOps.Api.Domain;

public class Shift
{
    private Shift() { }

    public Shift(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Vardiya bitişi başlangıçtan sonra olmalı.", nameof(endsAt));
        }

        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public int Id { get; private set; }
    public int TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
}
