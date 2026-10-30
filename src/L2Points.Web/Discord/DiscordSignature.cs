using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace L2Points.Web.Discord;

/// <summary>Verifies the Ed25519 signature Discord puts on every interaction request.</summary>
public static class DiscordSignature
{
    private static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    /// <summary>Signature covers the UTF-8 timestamp header followed by the raw body. Never throws; any malformed input is a failed verification.</summary>
    public static bool Verify(string publicKeyHex, string? signatureHex, string? timestamp, ReadOnlySpan<byte> body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(signatureHex) || string.IsNullOrEmpty(timestamp))
            return false;

        // Before the epoch is never valid; checking it also keeps the subtraction below from overflowing.
        if (!long.TryParse(timestamp, out var seconds) || seconds < 0)
            return false;

        if (Math.Abs(now.ToUnixTimeSeconds() - seconds) > MaxSkew.TotalSeconds)
            return false;

        try
        {
            var publicKey = Convert.FromHexString(publicKeyHex);
            var signature = Convert.FromHexString(signatureHex);
            if (publicKey.Length != Ed25519PublicKeyParameters.KeySize || signature.Length != Ed25519PrivateKeyParameters.SignatureSize)
                return false;

            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
            verifier.BlockUpdate(Encoding.UTF8.GetBytes(timestamp));
            verifier.BlockUpdate(body);
            return verifier.VerifySignature(signature);
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return false;
        }
    }
}
