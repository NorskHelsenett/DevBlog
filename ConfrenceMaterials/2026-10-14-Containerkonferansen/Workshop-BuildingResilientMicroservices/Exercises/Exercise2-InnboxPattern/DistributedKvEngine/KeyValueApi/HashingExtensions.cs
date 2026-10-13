public static class HashingExtensions
{
    public static string GetHashString(this byte[] input)
    {
        /* For now the hash is mostly to provide a loose bucket for comparison of values stored in plain text. So internally, there is no need to
        have a super low probability of collisions. It's not like this is for checking passwords, where accepting a wide variety of passwords (hash
        matches) would be an issue. */
        var hashBytes = System.IO.Hashing.Crc32.Hash(input);
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashHex.Substring(0,6);
    }

    public static string GetHashString(this string input)
    {
        /* For now the hash is mostly to provide a loose bucket for comparison of values stored in plain text. So internally, there is no need to
        have a super low probability of collisions. It's not like this is for checking passwords, where accepting a wide variety of passwords (hash
        matches) would be an issue. */
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        return inputBytes.GetHashString();
    }
}
