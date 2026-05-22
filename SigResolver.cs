namespace TD2Dumper;

/// <summary>
/// Helpers for resolving RIP-relative addresses and extracting structural
/// offsets that are embedded directly in x86-64 instruction encodings.
///
/// Buffer layout assumption: data[0] corresponds to the module base address,
/// so data[i] == byte at RVA i.  This means the returned target RVA needs no
/// further base adjustment.
/// </summary>
internal static class SigResolver
{
    /// <summary>
    /// Resolves a RIP-relative memory operand to an RVA.
    ///
    /// The instruction encoding is:
    ///   [sigOff + 0 .. sigOff + insnDispOff - 1]  = opcode / prefix bytes
    ///   [sigOff + insnDispOff .. +3]              = signed 32-bit displacement (little-endian)
    ///   instruction length = insnLen
    ///   RIP (after instruction) = sigOff + insnLen
    ///   target VA = moduleBase + sigOff + insnLen + disp32
    ///   target RVA = sigOff + insnLen + disp32   (base cancels)
    /// </summary>
    /// <param name="data">Full module bytes, data[i] = RVA i.</param>
    /// <param name="sigOff">RVA / buffer index of the first byte of the instruction.</param>
    /// <param name="insnDispOff">Byte offset inside the instruction where disp32 starts.</param>
    /// <param name="insnLen">Total byte length of the instruction.</param>
    /// <returns>Target RVA, or throws on out-of-range inputs.</returns>
    public static ulong ResolveRipRva(byte[] data, int sigOff, int insnDispOff, int insnLen)
    {
        int dispAt = sigOff + insnDispOff;
        if (dispAt + 4 > data.Length)
            throw new ArgumentOutOfRangeException(nameof(sigOff),
                $"disp32 read at 0x{dispAt:X} exceeds buffer length 0x{data.Length:X}.");

        int  disp32    = BitConverter.ToInt32(data, dispAt);
        long targetRva = (long)sigOff + insnLen + disp32;

        // The scan buffer covers only the .text section, but the resolved target
        // (e.g. rlclient_global, viewmatrix_base) typically lives in .data/.rdata
        // at a higher RVA.  We only validate non-negative — the target is a return
        // value, not an index into this buffer.
        if (targetRva < 0)
            throw new InvalidOperationException(
                $"Resolved target RVA 0x{targetRva:X} is negative — bad signature match.");

        return (ulong)targetRva;
    }

    /// <summary>
    /// Reads a 4-byte unsigned integer that is embedded as the displacement
    /// of an instruction — e.g. the 0x0130 in <c>mov rax, [rax+0x130]</c>.
    ///
    /// Use this to extract structural member offsets that the compiler baked
    /// directly into the opcode stream.  When those offsets change after a
    /// game update, this method will read the new value automatically.
    /// </summary>
    /// <param name="data">Module byte buffer.</param>
    /// <param name="sigOff">Buffer index of the first byte of the SIGNATURE MATCH
    ///     (not necessarily the same instruction that contains the disp).</param>
    /// <param name="embeddedOff">Byte offset from <paramref name="sigOff"/> to the
    ///     4-byte field to read.</param>
    public static uint ReadEmbeddedU32(byte[] data, int sigOff, int embeddedOff)
    {
        int at = sigOff + embeddedOff;
        if (at + 4 > data.Length)
            throw new ArgumentOutOfRangeException(nameof(embeddedOff),
                $"Embedded u32 at 0x{at:X} exceeds buffer length.");

        return BitConverter.ToUInt32(data, at);
    }

    /// <summary>
    /// Reads a single byte embedded in the instruction stream — used for 8-bit
    /// signed displacements (e.g. <c>mov rax,[rax+0x10]</c> where 0x10 is the disp8).
    /// </summary>
    public static uint ReadEmbeddedU8(byte[] data, int sigOff, int embeddedOff)
    {
        int at = sigOff + embeddedOff;
        if (at >= data.Length)
            throw new ArgumentOutOfRangeException(nameof(embeddedOff),
                $"Embedded u8 at 0x{at:X} exceeds buffer length.");

        return data[at];
    }

    /// <summary>
    /// Reads <paramref name="byteSize"/> bytes (1 or 4) at the specified offset.
    /// </summary>
    public static uint ReadEmbedded(byte[] data, int sigOff, int embeddedOff, int byteSize) =>
        byteSize switch
        {
            1 => ReadEmbeddedU8(data, sigOff, embeddedOff),
            4 => ReadEmbeddedU32(data, sigOff, embeddedOff),
            _ => throw new ArgumentOutOfRangeException(nameof(byteSize),
                     $"Supported sizes are 1 and 4, got {byteSize}.")
        };

    /// <summary>
    /// Verifies that the three bytes at <paramref name="bufferOff"/> inside
    /// <paramref name="data"/> match <paramref name="b0"/>, <paramref name="b1"/>,
    /// <paramref name="b2"/> (opcode check).
    /// </summary>
    public static bool CheckBytes(byte[] data, int bufferOff, byte b0, byte b1, byte b2)
    {
        if (bufferOff + 2 >= data.Length) return false;
        return data[bufferOff] == b0 &&
               data[bufferOff + 1] == b1 &&
               data[bufferOff + 2] == b2;
    }
}
