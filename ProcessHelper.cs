using System.Diagnostics;

namespace TD2Dumper;

internal static class ProcessHelper
{
    public static Process? FindByName(string name)
    {
        var procs = Process.GetProcessesByName(name);
        return procs.Length > 0 ? procs[0] : null;
    }

    /// <summary>
    /// Locates the first executable (code+execute) PE section in the live process
    /// by reading and parsing the in-memory PE headers.
    ///
    /// Returns (scanBase, scanSize) where:
    ///   scanBase  = absolute VA of module base (NOT of section start) so that
    ///               buffer[i] corresponds to RVA i exactly.
    ///   scanSize  = enough to cover the code section (capped at 64 MB).
    ///
    /// Falls back to (moduleBase, 64 MB) if parsing fails.
    /// </summary>
    public static (ulong moduleBase, int scanSize) GetScanRegion(MemoryReader mem, Process process)
    {
        ProcessModule? mainMod;
        try
        {
            mainMod = process.MainModule;
        }
        catch
        {
            throw new InvalidOperationException(
                "Cannot access MainModule — run the dumper as Administrator.");
        }

        if (mainMod == null)
            throw new InvalidOperationException("MainModule is null.");

        ulong moduleBase = (ulong)(long)mainMod.BaseAddress;
        int   imageSize  = mainMod.ModuleMemorySize;

        // Read first 4 KB — enough for DOS header, NT headers and section table
        var hdr = mem.ReadBytes(moduleBase, 4096);
        if (hdr == null || hdr[0] != 'M' || hdr[1] != 'Z')
        {
            Console.Error.WriteLine("[!] PE header read/parse failed — scanning first 64 MB.");
            return (moduleBase, Math.Min(0x4000000, imageSize));
        }

        int e_lfanew = BitConverter.ToInt32(hdr, 0x3C);
        if (e_lfanew < 0 || e_lfanew + 24 + 2 > 4096 ||
            hdr[e_lfanew] != 'P' || hdr[e_lfanew + 1] != 'E')
        {
            Console.Error.WriteLine("[!] NT signature not found — scanning first 64 MB.");
            return (moduleBase, Math.Min(0x4000000, imageSize));
        }

        short numSections  = BitConverter.ToInt16(hdr, e_lfanew + 6);
        short optHdrSize   = BitConverter.ToInt16(hdr, e_lfanew + 20);
        int   secTableOff  = e_lfanew + 24 + optHdrSize;

        const uint IMAGE_SCN_CNT_CODE    = 0x00000020u;
        const uint IMAGE_SCN_MEM_EXECUTE = 0x20000000u;
        const int  SEC_HDR               = 40;

        // Find the first executable section (the .text section)
        for (int i = 0; i < numSections; i++)
        {
            int off = secTableOff + i * SEC_HDR;
            if (off + SEC_HDR > hdr.Length) break;

            uint flags = BitConverter.ToUInt32(hdr, off + 36);
            if ((flags & IMAGE_SCN_CNT_CODE) != 0 && (flags & IMAGE_SCN_MEM_EXECUTE) != 0)
            {
                uint textRva  = BitConverter.ToUInt32(hdr, off + 12);  // VirtualAddress
                uint textVsz  = BitConverter.ToUInt32(hdr, off + 8);   // VirtualSize

                // We read from module base (RVA 0) so indices == RVAs.
                // Add a small margin past the text section in case a sig's tail
                // spills slightly into a gap or header area.
                int scanSize = (int)Math.Min((ulong)textRva + textVsz + 0x2000, (ulong)imageSize);
                scanSize     = Math.Min(scanSize, 0x4000000);  // hard cap at 64 MB

                Console.WriteLine(
                    $"[+] .text: RVA=0x{textRva:X}  VSize=0x{textVsz:X}  ScanBytes=0x{scanSize:X}");

                return (moduleBase, scanSize);
            }
        }

        Console.Error.WriteLine("[!] Executable section not found — scanning first 64 MB.");
        return (moduleBase, Math.Min(0x4000000, imageSize));
    }
}
