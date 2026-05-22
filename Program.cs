using System.Diagnostics;
using TD2Dumper;

public class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║     The Division 2 — Offset Dumper   ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();

        // ── 0. Locate the game process ───────────────────────────────────────────────

        Process? proc = ProcessHelper.FindByName("TheDivision2");
        if (proc == null)
        {
            Console.Error.WriteLine("[!] TheDivision2.exe is not running.");
            Console.Error.WriteLine("    Start the game and try again.");
            return 1;
        }
        Console.WriteLine($"[+] Process found — PID {proc.Id}");

        // ── 2. Open a read handle ────────────────────────────────────────────────────

        MemoryReader mem;
        try
        {
            mem = new MemoryReader(proc.Id);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[!] {ex.Message}");
            return 1;
        }

        using (mem)
        {
            // ── 3. Locate the code section we need to scan ───────────────────────────

            (ulong moduleBase, int scanSize) region;
            try
            {
                region = ProcessHelper.GetScanRegion(mem, proc);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[!] {ex.Message}");
                return 1;
            }

            Console.WriteLine($"[+] Module base : 0x{region.moduleBase:X16}");
            Console.WriteLine($"[+] Scan size   : 0x{region.scanSize:X} ({region.scanSize / 1024 / 1024} MB)");

            // ── 4. Read the module bytes ─────────────────────────────────────────────

            Console.WriteLine("[*] Reading module into memory ...");
            byte[]? moduleData = mem.ReadBytes(region.moduleBase, region.scanSize);
            if (moduleData == null)
            {
                Console.Error.WriteLine("[!] ReadProcessMemory failed.");
                Console.Error.WriteLine("    Make sure the game is still running and the dumper");
                Console.Error.WriteLine("    is launched as Administrator.");
                return 1;
            }
            Console.WriteLine($"[+] Read 0x{moduleData.Length:X} bytes.");

            // ── 5. Run the signature scanner ─────────────────────────────────────────

            var dumper = new OffsetDumper(moduleData, region.moduleBase);
            DumpResult? result = dumper.Run();
            if (result == null)
            {
                Console.Error.WriteLine("[!] One or more signatures were not found.");
                Console.Error.WriteLine(
                    "    The game may have been patched. Update the signatures in OffsetDumper.cs.");
                return 2;
            }

            // ── 6. Write output files ─────────────────────────────────────────────────

            string hppPath  = Path.Combine(AppContext.BaseDirectory, "TD2_offsets.hpp");

            result.WriteHpp(hppPath);

            Console.WriteLine($"[+] Dump Saved  : {hppPath}");
            Console.WriteLine();
            Console.WriteLine("Done. Press CTRL+C to close the Console...");

            while (true)
            {
                Thread.Sleep(1);
            }
        }
    }
}
