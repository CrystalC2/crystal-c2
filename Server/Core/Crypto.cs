using System.Security.Cryptography;

namespace Server.Core;

public static class Crypto
{
    private const int HmacSize = 32;
    private const int IvSize = 16;
    
    public static byte[] RsaDecrypt(byte[] data, byte[] privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(privateKey, out _);
        return rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);
    }

    public static byte[] AesEncrypt(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        var encrypted = aes.EncryptCbc(data, aes.IV);

        // iv | ciphertext — this is what the hmac covers
        var ivAndCt = new byte[IvSize + encrypted.Length];
        aes.IV.CopyTo(ivAndCt, 0);
        encrypted.CopyTo(ivAndCt, IvSize);

        var hmac = HMACSHA256.HashData(key, ivAndCt);

        // final layout: hmac | iv | ciphertext
        var final = new byte[HmacSize + ivAndCt.Length];
        hmac.CopyTo(final, 0);
        ivAndCt.CopyTo(final, HmacSize);

        return final;
    }

    public static byte[] AesDecrypt(byte[] data, byte[] key)
    {
        if (data.Length < HmacSize + IvSize)
            return [];

        var hmac       = data[..HmacSize];
        var ivAndCt    = data[HmacSize..]; // hmac covers iv || ciphertext 
        var iv         = ivAndCt[..IvSize];
        var ciphertext = ivAndCt[IvSize..];

        // verify hmac before decrypting
        var expected = HMACSHA256.HashData(key, ivAndCt);

        if (!CryptographicOperations.FixedTimeEquals(hmac, expected))
            return [];

        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptCbc(ciphertext, iv);
    }
}