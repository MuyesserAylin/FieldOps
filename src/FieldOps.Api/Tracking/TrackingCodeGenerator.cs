using System;
using System.Security.Cryptography;

namespace FieldOps.Api.Tracking;

public class TrackingCodeGenerator : ITrackingCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLenght = 12;
    public string Generate()
    {
        return RandomNumberGenerator.GetString(Alphabet, CodeLenght);
    }
}
