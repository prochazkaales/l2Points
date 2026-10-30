using System.Text;
using L2Points.Web.Discord;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace L2Points.Web.Tests.Discord;

public class DiscordSignatureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static (string PublicKeyHex, Ed25519PrivateKeyParameters Private) NewKeyPair()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var publicKey = (Ed25519PublicKeyParameters)pair.Public;
        return (Convert.ToHexString(publicKey.GetEncoded()).ToLowerInvariant(), (Ed25519PrivateKeyParameters)pair.Private);
    }

    private static string Sign(Ed25519PrivateKeyParameters key, string timestamp, byte[] body)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        var message = Encoding.UTF8.GetBytes(timestamp).Concat(body).ToArray();
        signer.BlockUpdate(message, 0, message.Length);
        return Convert.ToHexString(signer.GenerateSignature()).ToLowerInvariant();
    }

    private static string Timestamp(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString();

    [Fact]
    public void Valid_signature_passes()
    {
        var (publicKey, key) = NewKeyPair();
        var body = Encoding.UTF8.GetBytes("{\"type\":1}");
        var timestamp = Timestamp(Now);

        Assert.True(DiscordSignature.Verify(publicKey, Sign(key, timestamp, body), timestamp, body, Now));
    }

    [Fact]
    public void Tampered_body_fails()
    {
        var (publicKey, key) = NewKeyPair();
        var body = Encoding.UTF8.GetBytes("{\"type\":1}");
        var timestamp = Timestamp(Now);
        var signature = Sign(key, timestamp, body);

        Assert.False(DiscordSignature.Verify(publicKey, signature, timestamp, Encoding.UTF8.GetBytes("{\"type\":2}"), Now));
    }

    [Fact]
    public void Missing_headers_fail()
    {
        var (publicKey, key) = NewKeyPair();
        var body = Encoding.UTF8.GetBytes("{}");
        var timestamp = Timestamp(Now);
        var signature = Sign(key, timestamp, body);

        Assert.False(DiscordSignature.Verify(publicKey, null, timestamp, body, Now));
        Assert.False(DiscordSignature.Verify(publicKey, signature, null, body, Now));
    }

    [Theory]
    [InlineData("not hex")]
    [InlineData("abcd")]
    [InlineData("")]
    public void Invalid_hex_fails(string signature)
    {
        var (publicKey, _) = NewKeyPair();

        Assert.False(DiscordSignature.Verify(publicKey, signature, Timestamp(Now), "{}"u8, Now));
        Assert.False(DiscordSignature.Verify("zz", new string('0', 128), Timestamp(Now), "{}"u8, Now));
    }

    [Fact]
    public void Non_numeric_timestamp_fails()
    {
        var (publicKey, key) = NewKeyPair();
        var body = "{}"u8.ToArray();

        Assert.False(DiscordSignature.Verify(publicKey, Sign(key, "yesterday", body), "yesterday", body, Now));
    }

    [Theory]
    [InlineData(-6)]
    [InlineData(6)]
    public void Old_or_future_timestamp_fails(int minutes)
    {
        var (publicKey, key) = NewKeyPair();
        var body = "{}"u8.ToArray();
        var timestamp = Timestamp(Now.AddMinutes(minutes));

        Assert.False(DiscordSignature.Verify(publicKey, Sign(key, timestamp, body), timestamp, body, Now));
    }

    [Fact]
    public void Timestamp_within_five_minutes_passes()
    {
        var (publicKey, key) = NewKeyPair();
        var body = "{}"u8.ToArray();
        var timestamp = Timestamp(Now.AddMinutes(-4));

        Assert.True(DiscordSignature.Verify(publicKey, Sign(key, timestamp, body), timestamp, body, Now));
    }
}
