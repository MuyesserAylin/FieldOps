using System;
using FieldOps.Api.Tracking;

namespace FieldOps.Api.Tests.Reports;

public sealed class SequenceTrackingCodeGenerator : ITrackingCodeGenerator
{
    private readonly Queue<string> _codes;

    public SequenceTrackingCodeGenerator(params string[] codes)
    {
        _codes = new Queue<string>(codes);
    }

    public string Generate() => _codes.Dequeue();
}
