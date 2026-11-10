using System.Security.Cryptography;
using System.Text;

namespace FrigoTrack.Common;


public static class SecureChannel
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public static Envelope Encrypt(string sessionId, byte[] key, string json)
    {
        var plain = Encoding.UTF8.GetBytes(json);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);  
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
      
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(sessionId));

        var blob = new byte[NonceSize + cipher.Length];
        nonce.CopyTo(blob, 0);
        cipher.CopyTo(blob, NonceSize);

        return new Envelope(sessionId, Convert.ToBase64String(blob), Convert.ToBase64String(tag));
    }

    public static string Decrypt(Envelope env, byte[] key)
    {
        try
        {
            var blob = Convert.FromBase64String(env.Ciphertext);
            var tag = Convert.FromBase64String(env.Tag);
            if (blob.Length < NonceSize || tag.Length != TagSize)
                throw new CryptographicException("Busta malformata");

            var nonce = blob.AsSpan(0, NonceSize);
            var cipher = blob.AsSpan(NonceSize);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(env.SessionId));
            return Encoding.UTF8.GetString(plain);
        }
        catch (FormatException)
        {
            throw new CryptographicException("Base64 non valido");
        }
    }
}
