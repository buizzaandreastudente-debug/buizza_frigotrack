using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace FrigoTrack.Common;


public static class DiffieHellman
{
    public static readonly BigInteger P = FromHex(
        "FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74" +
        "020BBEA63B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F1437" +
        "4FE1356D6D51C245E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7ED" +
        "EE386BFB5A899FA5AE9F24117C4B1FE649286651ECE45B3DC2007CB8A163BF05" +
        "98DA48361C55D39A69163FA8FD24CF5F83655D23DCA3AD961C62F356208552BB" +
        "9ED529077096966D670C354E4ABC9804F1746C08CA18217C32905E462E36CE3B" +
        "E39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9DE2BCBF695581718" +
        "3995497CEA956AE515D2261898FA051015728E5A8AACAA68FFFFFFFFFFFFFFFF");

    public static readonly BigInteger G = 2;

    public static BigInteger GeneratePrivate()
    {
        while (true)
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            var x = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            if (x > 1) return x;
        }
    }

    public static BigInteger ComputePublic(BigInteger g, BigInteger secret, BigInteger p)
        => BigInteger.ModPow(g, secret, p);

    public static BigInteger ComputeShared(BigInteger otherPublic, BigInteger secret, BigInteger p)
        => BigInteger.ModPow(otherPublic, secret, p);

    public static bool IsValidPublic(BigInteger y, BigInteger p) => y > 1 && y < p - 1;

    public static byte[] DeriveKey(BigInteger k)
        => SHA256.HashData(k.ToByteArray(isUnsigned: true, isBigEndian: true));

    public static string ToHex(BigInteger v) => v.ToString("X");

    public static BigInteger FromHex(string hex)
        => BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
