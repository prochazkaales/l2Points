using System.Security.Cryptography;

namespace L2Points.Infrastructure.Identity;

/// <summary>Generates readable initial passwords without easily confused characters (0 O o 1 l I).</summary>
public static class PasswordGenerator
{
    public const int Length = 12;

    private const string Lower = "abcdefghijkmnpqrstuvwxyz";

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    private const string Digits = "23456789";

    private const string All = Lower + Upper + Digits;

    public static string Generate()
    {
        // One character of each required class first, the rest from the full alphabet, then shuffle.
        var chars = new char[Length];
        chars[0] = Pick(Lower);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Digits);
        for (var i = 3; i < Length; i++)
            chars[i] = Pick(All);

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
