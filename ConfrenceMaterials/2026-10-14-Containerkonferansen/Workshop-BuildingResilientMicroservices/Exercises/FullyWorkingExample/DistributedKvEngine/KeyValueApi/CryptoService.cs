using System.Security.Cryptography;

// Mostly based on https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=net-8.0
// Don't DI it in this project, because there is almost no savings in having a single instance (only real state is the aes key), and it allows dynamically referring to it's members.
// If you'd DI-it with different implementations, you'd run into issues where you simultaneously want the noop version and this version at the same time, one for the topics and the other for disk.
public class CryptoService
{
    // At this point (using specific implementations like this is deprecated now) 128 bits (16 bytes) is the only valid size in .Net
    // https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesmanaged.blocksize?view=net-8.0#remarks
    // Seems to be the general size anyways: https://crypto.stackexchange.com/a/50786
    // But, if this is to survive over some time, specify it explicitly so that stuff doesn't randomly stop working.
    private const int AesBlockSizeBytes = 16;
    private readonly byte[] _aesKey;

    public CryptoService()
    {
        // To generate 256 bit/32 byte hex string securely enough:
        // `openssl rand -hex 32`
        // Or for rapid proof of concepting on machine without openssl this js also works:
        // `Array.from(crypto.getRandomValues(new Uint8Array(32))).map(b => b.toString(16).padStart(2, '0')).join('');`
        var aesKeyHexString = string.Empty;
        var aesKeyLocation = Environment.GetEnvironmentVariable(KV_API_AES_KEY_LOCATION);
        if(!string.IsNullOrWhiteSpace(aesKeyLocation))
        {
            if(File.Exists(aesKeyLocation))
            {
                var aesKeyStringFromFile = File.ReadAllLines(aesKeyLocation).First();
                aesKeyHexString = aesKeyStringFromFile;
            }
            else
            {
                throw new InvalidOperationException(message: $"Env variable {KV_API_AES_KEY_LOCATION} must be a location of a file containing a line with the hex representation of 256 bits/32 bytes, that is 64 characters long. The supplied value was not the path to an existing file");
            }
        }
        var aesKeyHexFromEnvVar = Environment.GetEnvironmentVariable(KV_API_AES_KEY);
        if(!string.IsNullOrWhiteSpace(aesKeyHexFromEnvVar))
        {
            aesKeyHexString = aesKeyHexFromEnvVar;
        }
        if(aesKeyHexString?.Length != 64)
        {
            throw new InvalidOperationException(message: $"AES Key obtained from env variable {KV_API_AES_KEY} or {KV_API_AES_KEY_LOCATION} must be hex representation of 256 bits/32 bytes, that is 64 characters long, the supplied value was not");
        }
        if(!aesKeyHexString.All(c => "0123456789abcdefABCDEF".Contains(c)))
        {
            throw new InvalidOperationException(message: $"AES Key obtained from env variable {KV_API_AES_KEY} or {KV_API_AES_KEY_LOCATION} must be hex value, the supplied value contained characters that are not legal hex (0-9a-fA-F)");
        }
        _aesKey = Convert.FromHexString(aesKeyHexString);
    }

    public byte[] Encrypt(byte[] unencryptedInput)
    {
        if(unencryptedInput.Length <= 0)
        {
            throw new ArgumentException(message: "Unencrypted input has to be supplied", paramName: nameof(unencryptedInput));
        }
        using Aes aesAlg = Aes.Create();
        var randomPerMessageIv = GenerateKnownSizeAesIv();
        // aesAlg.KeySize = 256; // Note: regenerates the key when set
        // aesAlg.BlockSize = 128; // 128 bits is the only valid size for AES.
        // aesAlg.Mode = CipherMode.CBC; // The default is CBC.
        aesAlg.Key = _aesKey;
        aesAlg.IV = randomPerMessageIv;
        // aesAlg.Padding = PaddingMode.PKCS7; // The default is PKCS7

        ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

        using var ms = new MemoryStream();
        using var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cryptoStream.Write(unencryptedInput, 0, unencryptedInput.Length);
        cryptoStream.FlushFinalBlock();

        var encrypted = ms.ToArray();

        // This could maybe be avoided by starting with writing the IV to the ms MemoryStream right after creating it.
        // But, it's late, and I don't have time to experiment with it now or make up opinions about future behaviour of CryptoStream.
        // (If it doesn't overwrite the stream now but only appends, will it continue to do so forever?)
        var encryptedWithPrependedIv = new byte[randomPerMessageIv.Length + encrypted.Length];
        randomPerMessageIv.CopyTo(encryptedWithPrependedIv, 0);
        encrypted.CopyTo(encryptedWithPrependedIv, randomPerMessageIv.Length);
        return encryptedWithPrependedIv;
    }

    public byte[] Decrypt(byte[] encryptedInput)
    {
        if(encryptedInput.Length <= AesBlockSizeBytes + 1)
        {
            throw new ArgumentNullException(message: "Encrypted input must be at least the length of the prepended IV and an additional encrypted byte", paramName: nameof(encryptedInput));
        }

        // Range indices are fun! https://learn.microsoft.com/en-us/dotnet/csharp/tutorials/ranges-indexes
        var aesIv = encryptedInput[0..AesBlockSizeBytes];
        var encryptedMessage = encryptedInput[AesBlockSizeBytes..];

        using Aes aesAlg = Aes.Create();
        aesAlg.Key = _aesKey;
        aesAlg.IV = aesIv;
        ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

        using var ms = new MemoryStream();
        using var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Write);
        cryptoStream.Write(encryptedMessage, 0, encryptedMessage.Length);
        cryptoStream.FlushFinalBlock();

        return ms.ToArray();
    }

    private static byte[] GenerateKnownSizeAesIv()
    {
        return System.Security.Cryptography.RandomNumberGenerator.GetBytes(AesBlockSizeBytes);
    }
}
