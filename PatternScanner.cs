namespace TD2Dumper;

/// <summary>
/// Fast byte-pattern scanner with wildcard ('?' or '??') support.
/// Byte tokens are hex strings separated by whitespace.
///
/// Example pattern:  "48 8B 05 ? ? ? ? 48 85 C0 74 ? 48 8B 80 ? ? ? ? C3"
/// </summary>
internal static class PatternScanner
{
    /// <summary>
    /// Returns the offset of the first match of <paramref name="pattern"/> inside
    /// <paramref name="data"/>, or -1 if not found.
    /// The returned offset is an index into <paramref name="data"/>.
    /// </summary>
    public static int FindFirst(byte[] data, string pattern)
    {
        var (bytes, mask) = Parse(pattern);
        int limit = data.Length - bytes.Length;

        for (int i = 0; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < bytes.Length; j++)
            {
                if (mask[j] && data[i + j] != bytes[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns offsets of ALL matches.  Use FindFirst when uniqueness is expected.
    /// </summary>
    public static List<int> FindAll(byte[] data, string pattern)
    {
        var (bytes, mask) = Parse(pattern);
        var results = new List<int>();
        int limit = data.Length - bytes.Length;

        for (int i = 0; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < bytes.Length; j++)
            {
                if (mask[j] && data[i + j] != bytes[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) results.Add(i);
        }
        return results;
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private static (byte[] bytes, bool[] mask) Parse(string pattern)
    {
        var parts = pattern.Trim()
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        var bytes = new byte[parts.Length];
        var mask  = new bool[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] is "?" or "??")
            {
                bytes[i] = 0;
                mask[i]  = false;   // wildcard → skip comparison
            }
            else
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
                mask[i]  = true;
            }
        }

        return (bytes, mask);
    }
}
