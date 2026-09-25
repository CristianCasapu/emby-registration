using System.Security.Cryptography;
using System.Text;

namespace Registration.Security;

/// <summary>
/// Proof-of-work fara serviciu extern: browserul cauta un numar pentru care
/// SHA-256(token + ":" + numar) incepe cu N biti de zero. Pentru un om e o asteptare de
/// cateva secunde, facuta in fundal cat completeaza formularul; pentru cineva care
/// trimite mii de cereri devine scump. Tokenul e semnat, deci dificultatea nu se poate
/// micsora din client.
/// </summary>
public static class ProofOfWork
{
    public static bool Verify(string token, string? solution, int bits)
    {
        if (bits <= 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(solution) || solution.Length > 20 || !solution.All(char.IsAsciiDigit))
        {
            return false;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token + ":" + solution));
        return LeadingZeroBits(hash) >= bits;
    }

    public static int LeadingZeroBits(ReadOnlySpan<byte> hash)
    {
        var bits = 0;
        foreach (var b in hash)
        {
            if (b == 0)
            {
                bits += 8;
                continue;
            }

            return bits + System.Numerics.BitOperations.LeadingZeroCount((uint)b) - 24;
        }

        return bits;
    }

    /// <summary>Rezolvare in C#, pentru teste.</summary>
    internal static string Solve(string token, int bits)
    {
        for (long i = 0; ; i++)
        {
            var candidate = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (Verify(token, candidate, bits))
            {
                return candidate;
            }
        }
    }
}
