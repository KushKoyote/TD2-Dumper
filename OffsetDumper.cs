using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TD2Dumper;

// ─── Result container ─────────────────────────────────────────────────────────

internal sealed class DumpResult
{
    public DateTime Timestamp  { get; init; }
    public ulong    ModuleBase { get; init; }

    // ── Signature-resolved RVAs (these change every game update) ─────────────

    /// <summary>RVA of the rlclient_global pointer — resolved from SIG_CLIENT.</summary>
    public ulong RVA_rlclient_global { get; set; }

    /// <summary>RVA of the viewmatrix base pointer — resolved from SIG_VIEW.</summary>
    public ulong RVA_viewmatrix_base { get; set; }

    /// <summary>RVA of the no-recoil float — resolved from SIG_RECOIL. Default = 30.0f</summary>
    public ulong RVA_recoil { get; set; }

    /// <summary>RVA of the no-spread float (recoil - 4).  Default = 4.0f</summary>
    public ulong RVA_spread { get; set; }

    /// <summary>RVA of the setnoon byte flag — resolved from SIG_TOD. 1=force noon, 0=normal</summary>
    public ulong RVA_setnoon { get; set; }

    /// <summary>RVA of the setnight byte flag (setnoon + 1). 1=force night, 0=normal</summary>
    public ulong RVA_setnight { get; set; }

    /// <summary>RVA of the magic bullet collision mask byte. Default = 0x98, write 0xFF to enable.</summary>
    public ulong RVA_magicbullet { get; set; }

    // ── Structural offsets extracted directly from code bytes ─────────────────

    /// <summary>
    /// Offset from rlclient to pclient.
    /// Extracted from the mov rax,[rax+OFF_pClient] instruction inside SIG_CLIENT.
    /// Will automatically reflect the correct value even after a game update.
    /// </summary>
    public uint OFF_pClient { get; set; }

    // ── Known structural offsets from current reverse-engineering ─────────────

    /// <summary>
    /// All other pointer-chain / struct offsets that are stable between updates
    /// (update manually if the game changes these).
    /// </summary>
    public Dictionary<string, ulong> StructOffsets { get; init; } = [];

    /// <summary>Names of offsets that were auto-scanned (not static fallbacks).</summary>
    public HashSet<string> ScannedOffsets { get; } = [];

    // ── Output ────────────────────────────────────────────────────────────────

    public void PrintSummary()
    {
        var line = new string('─', 52);
        Console.WriteLine();
        Console.WriteLine(line);
        Console.WriteLine("  DUMP RESULTS");
        Console.WriteLine(line);
        Console.WriteLine($"  ModuleBase          = 0x{ModuleBase:X}");
        Console.WriteLine($"  RVA_rlclient_global = 0x{RVA_rlclient_global:X}");
        Console.WriteLine($"  RVA_viewmatrix_base = 0x{RVA_viewmatrix_base:X}");
        Console.WriteLine($"  RVA_recoil          = 0x{RVA_recoil:X}");
        Console.WriteLine($"  RVA_spread          = 0x{RVA_spread:X}");
        Console.WriteLine($"  RVA_setnoon         = 0x{RVA_setnoon:X}");
        Console.WriteLine($"  RVA_setnight        = 0x{RVA_setnight:X}");
        Console.WriteLine($"  RVA_magicbullet     = 0x{RVA_magicbullet:X}");
        Console.WriteLine(line);
        foreach (var kv in StructOffsets)
        {
            string tag = ScannedOffsets.Contains(kv.Key) ? "" : " (static)";
            string name = kv.Key.Replace("OFF_", "");
            Console.WriteLine($"  {name,-30} = 0x{kv.Value:X}{tag}");
        }
        Console.WriteLine(line);
        Console.WriteLine();
    }

    /// <summary>Writes a C++ .hpp header with all discovered values.</summary>
    public void WriteHpp(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// The Division 2 — Offset Dump");
        sb.AppendLine($"// Generated on {Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine();

        sb.AppendLine("// ── Globals ──────────────────────────────────────────────────────────────────");
        sb.AppendLine($"constexpr std::uintptr_t RVA_rlclient_global = 0x{RVA_rlclient_global:X};");
        sb.AppendLine($"constexpr std::uintptr_t RVA_viewmatrix_base = 0x{RVA_viewmatrix_base:X};");
        sb.AppendLine($"constexpr std::uintptr_t RVA_recoil          = 0x{RVA_recoil:X};  // float: Default = 30.0f");
        sb.AppendLine($"constexpr std::uintptr_t RVA_spread          = 0x{RVA_spread:X};  // float: Default = 4.0f");
        sb.AppendLine($"constexpr std::uintptr_t RVA_setnoon         = 0x{RVA_setnoon:X};  // byte: 1=force noon (12:00), 0=normal");
        sb.AppendLine($"constexpr std::uintptr_t RVA_setnight        = 0x{RVA_setnight:X};  // byte: 1=force night (22:00), 0=normal");
        sb.AppendLine($"constexpr std::uintptr_t RVA_magicbullet     = 0x{RVA_magicbullet:X};  // byte: default=0x98, write 0xFF to enable");
        sb.AppendLine();

        sb.AppendLine("// ── Structural offsets ───────────────────────────────────────────────────────");
        foreach (var kv in StructOffsets)
        {
            sb.AppendLine($"constexpr std::uintptr_t {kv.Key,-30} = 0x{kv.Value:X};");
        }
        sb.AppendLine();

        sb.AppendLine("// ── Bone matrix constants ────────────────────────────────────────────────────");
        sb.AppendLine("constexpr std::uintptr_t BONE_STRIDE                   = 0x40;  // sizeof(BoneMatrix)");
        sb.AppendLine("constexpr std::uintptr_t BONE_POS_X                    = 0x30;  // float x within BoneMatrix");
        sb.AppendLine("constexpr std::uintptr_t BONE_POS_Y                    = 0x34;  // float y within BoneMatrix");
        sb.AppendLine("constexpr std::uintptr_t BONE_POS_Z                    = 0x38;  // float z within BoneMatrix");
        sb.AppendLine();

        sb.AppendLine("// ── Pointer-chain references ──────────────────────────────────────────────────");
        sb.AppendLine("//");
        sb.AppendLine("//  core:");
        sb.AppendLine("//     rlclient    = *(u64*)(base + RVA_rlclient_global)");
        sb.AppendLine("//     pclient     = *(u64*)(rlclient + OFF_pClient)");
        sb.AppendLine("//     pobject     = *(u64*)(pclient  + OFF_pObject)");
        sb.AppendLine("//");
        sb.AppendLine("//  local player:");
        sb.AppendLine("//     stage0      = *(u64*)(pobject + OFF_localStage0)");
        sb.AppendLine("//     stage1      = *(u64*)(stage0  + OFF_localStage1)");
        sb.AppendLine("//     localplayer = *(u64*)(stage1  + OFF_localStage2)");
        sb.AppendLine("//     localPos    = localplayer + OFF_entityPos    -- vec3 float[3]");
        sb.AppendLine("//     localName   = localplayer + OFF_name         -- sd_string (inline or heap)");
        sb.AppendLine("//     localFaction= *(u64*)(localplayer + OFF_factionCtx)");
        sb.AppendLine("//");
        sb.AppendLine("//  view matrix:");
        sb.AppendLine("//     viewmatrix_ptr = *(u64*)(base + RVA_viewmatrix_base)");
        sb.AppendLine("//     viewproj[16]   = (float*)(viewmatrix_ptr + OFF_viewProj)");
        sb.AppendLine("//");
        sb.AppendLine("//  entity list:");
        sb.AppendLine("//     entityArray = *(u64*)(pobject + OFF_entityArray)   -- ptr to u64[]");
        sb.AppendLine("//     entityCount = *(u32*)(pobject + OFF_entityCount)");
        sb.AppendLine("//     entity[i]   = *(u64*)(entityArray + i * 8)");
        sb.AppendLine("//");
        sb.AppendLine("//  per-entity: (ent = entity[i])");
        sb.AppendLine("//     entPos      = ent + OFF_entityPos        -- vec3 float[3]");
        sb.AppendLine("//     entName     = ent + OFF_name             -- sd_string (inline or heap)");
        sb.AppendLine("//     entType     = ent + OFF_type             -- sd_string: \"soldier\" == Player else empty or invalid string");
        sb.AppendLine("//     isPlayer    = (entType == \"soldier\")");
        sb.AppendLine("//     isDowned    = *(u32*)(ent + OFF_isDowned) -- 1 = player is downed");
        sb.AppendLine("//     hpPtr       = *(u64*)(ent + OFF_HpPtr)");
        sb.AppendLine("//     health      = *(u32*)(hpPtr + OFF_health)");
        sb.AppendLine("//     healthMax   = *(u32*)(hpPtr + OFF_healthMax)");
        sb.AppendLine("//     [Players only]:");
        sb.AppendLine("//       armor         = *(u32*)(hpPtr + OFF_agentarmor)");
        sb.AppendLine("//       armorMax      = *(u32*)(hpPtr + OFF_agentarmorMax)");
        sb.AppendLine("//       armorOverflow = *(u32*)(hpPtr + OFF_agentarmorOverflow)");
        sb.AppendLine("//     [NPCs only]:");
        sb.AppendLine("//       armor         = *(u32*)(hpPtr + OFF_npcArmor)");
        sb.AppendLine("//       armorMax      = *(u32*)(hpPtr + OFF_npcArmorMax)");
        sb.AppendLine("//");
        sb.AppendLine("//  hostility:");
        sb.AppendLine("//     [Players] (entType == \"soldier\"):");
        sb.AppendLine("//       rogueComp = *(u64*)(ent + OFF_rogueComp)");
        sb.AppendLine("//       [Dark Zone]");
        sb.AppendLine("//         rogueTeamIdx = *(u32*)(rogueComp + OFF_rogueTeamIdx)");
        sb.AppendLine("//         isHostile    = (rogueTeamIdx != 0xFFFFFFFF)  -- Rogue Status for DZ");
        sb.AppendLine("//       [Conflict mode]");
        sb.AppendLine("//         pvpData      = *(u64*)(ent + OFF_pvpData)");
        sb.AppendLine("//         pvpTeamId    = *(u32*)(pvpData + OFF_pvpTeamId)");
        sb.AppendLine("//         localPvpData = *(u64*)(localplayer + OFF_pvpData)");
        sb.AppendLine("//         localTeamId  = *(u32*)(localPvpData + OFF_pvpTeamId)");
        sb.AppendLine("//         isHostile    = (pvpTeamId != localTeamId)");
        sb.AppendLine("//      [NPCs] (all other entity types skip ):");
        sb.AppendLine("//         entFaction   = *(u64*)(ent + OFF_factionCtx)");
        sb.AppendLine("//         isHostile    = (entFaction != localFaction)");
        sb.AppendLine("//");
        sb.AppendLine("//  bones:");
        sb.AppendLine("//     bonesBase   = *(u64*)(ent + OFF_bonesBase)");
        sb.AppendLine("//     boneArray   = *(u64*)(bonesBase + OFF_boneArray)");
        sb.AppendLine("//     mat         = boneArray + boneId * BONE_STRIDE   -- BONE_STRIDE = 0x40");
        sb.AppendLine("//     bonePos     = { read_float(mat+0x30), read_float(mat+0x34), read_float(mat+0x38) }");
        sb.AppendLine("//");
        sb.AppendLine("//  loot list (dropped loot):");
        sb.AppendLine("//     lootManager     = *[pobject + OFF_lootManager]");
        sb.AppendLine("//     lootArray       = *[lootManager + OFF_lootArray]   -- ptr array of loot sources");
        sb.AppendLine("//     lootCount       = *(u32*)(lootManager + OFF_lootCount)");
        sb.AppendLine("//     lootPos         = loot + OFF_lootPos");
        sb.AppendLine("//     lootItemsCount  = *(u32*)(loot + OFF_lootItemsCount)");
        sb.AppendLine("//     lootItemsBase   = *(u64*)(loot + OFF_lootItemsBase)");
        sb.AppendLine("//     firstEntry      = *(u64*)(lootItemsBase)");
        sb.AppendLine("//     entry[i]        = firstEntry + i * OFF_lootItemStride");
        sb.AppendLine("//     itemHash        = *(u32*)(entry + OFF_lootItemHash)");
        sb.AppendLine("//     itemObj         = *(u64*)(entry + OFF_lootItemObj)");
        sb.AppendLine("//     itemData        = *(u64*)(entry + OFF_lootItemData)");
        sb.AppendLine("//");
        sb.AppendLine("//  loot item:");
        sb.AppendLine("//     name = sd_string(itemObj  + OFF_lootItemObjName)  -- ex: Grenade  ");
        sb.AppendLine("//     quality = *(u8*)(itemData + OFF_lootItemQuality)  -- eg: 0=Black,1=Yellow,2=Grey,3=Green,4=Blue,5=Purple,6=Orange,7=Vanity,8=GearSet,9=Exotic,10=Immersion,11=Prototype");
        sb.AppendLine("//");
        sb.AppendLine("//  recoil & spread:");
        sb.AppendLine("//     recoilAddr  = base + RVA_recoil   -- default 30.0f, write 0.0f to disable");
        sb.AppendLine("//     spreadAddr  = base + RVA_spread   -- default  4.0f, write 0.0f to disable");
        sb.AppendLine("//     (spread lives 4 bytes before recoil in memory)");
        sb.AppendLine("//");
        sb.AppendLine("//  magic bullet (bullets pass through walls/cover):");
        sb.AppendLine("//     mbAddr   = base + RVA_magicbullet   -- collision mask hi-byte in FIREROUND profile");
        sb.AppendLine("//     enable:  wu8(mbAddr, 0xFF)          -- expands collision layer mask to include all");
        sb.AppendLine("//     disable: wu8(mbAddr, 0x98)          -- restores original FIREROUND mask");
        sb.AppendLine("//");
        sb.AppendLine("//  silent aim (write direction):");
        sb.AppendLine("//     aimCtrl     = *(u64*)(localplayer + OFF_weaponAimCtrl)   -- AgentWeaponAimController*");
        sb.AppendLine("//     worldAimPos = aimCtrl + OFF_worldAimPos                  -- float3 {x, y, z}");
        sb.AppendLine("//     to snap aim:  write_float(aimCtrl + 0x3778, target.x)");
        sb.AppendLine("//                   write_float(aimCtrl + 0x377C, target.y)");
        sb.AppendLine("//                   write_float(aimCtrl + 0x3780, target.z)");
        sb.AppendLine("//     engine computes: aimDirection = normalize(worldAimPos - firePosition)");
        sb.AppendLine("//");
        sb.AppendLine("//  RPM final:");
        sb.AppendLine("//     weapPtr1 = *(u64*)(localplayer + OFF_weapPtr0)");
        sb.AppendLine("//     weapPtr2 = *(u64*)(weapPtr1    + OFF_weapPtr1)");
        sb.AppendLine("//     weapPtr3 = *(u64*)(weapPtr2    + OFF_weapPtr2)");
        sb.AppendLine("//     rpmFinal = read_float(weapPtr3 + OFF_rpmFinal)   -- read/write float");
        sb.AppendLine("//");
        sb.AppendLine("//  time of day:");
        sb.AppendLine("//     setnoon  = base + RVA_setnoon    -- byte: 1 = force noon  (12:00), 0 = normal");
        sb.AppendLine("//     setnight = base + RVA_setnight   -- byte: 1 = force night (22:00), 0 = normal");
        sb.AppendLine("//     NOTE: setting both simultaneously causes the engine to clear both (normal cycle)");
        sb.AppendLine();

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Writes a JSON file with all values for easy programmatic consumption.</summary>
    public void WriteJson(string path)
    {
        var doc = new
        {
            timestamp    = Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            module_base  = $"0x{ModuleBase:X}",
            rvas = new
            {
                rlclient_global = $"0x{RVA_rlclient_global:X}",
                viewmatrix_base = $"0x{RVA_viewmatrix_base:X}",
                recoil          = $"0x{RVA_recoil:X}",
                spread          = $"0x{RVA_spread:X}",
                setnoon         = $"0x{RVA_setnoon:X}",
                setnight        = $"0x{RVA_setnight:X}",
                magicbullet     = $"0x{RVA_magicbullet:X}",
            },
            extracted_offsets = new Dictionary<string, string>
            {
                ["OFF_pClient"] = $"0x{OFF_pClient:X}",
            },
            scanned_offsets = ScannedOffsets.OrderBy(x => x).ToArray(),
            struct_offsets = StructOffsets.ToDictionary(
                kv => kv.Key,
                kv => $"0x{kv.Value:X}")
        };

        var json = JsonSerializer.Serialize(doc,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

// ─── Dumper ───────────────────────────────────────────────────────────────────

internal sealed class OffsetDumper
{
    // ── Signatures ───────────────────────────────────────────────────────────
    //
    // SIG_CLIENT — the unique rlclient accessor function:
    //   mov  rax, [rip+rlclient_global]   ; 48 8B 05 ?? ?? ?? ??  (7 bytes)
    //   test rax, rax                      ; 48 85 C0
    //   jz   +8                            ; 74 08
    //   mov  rax, [rax+0x130]             ; 48 8B 80 30 01 00 00  (7 bytes)
    //   ret                                ; C3
    //
    // The OFF_pClient bytes (30 01 00 00 = 0x130) are kept FIXED in the pattern so the
    // scanner uniquely selects this function — the binary contains several functions with
    // the same opcode skeleton but different displacements; wildcarding those bytes causes
    // a false match on an earlier function with OFF_pClient=0x88.
    // ReadEmbeddedU32 at offset 15 still extracts the live value from the binary.
    // If OFF_pClient ever changes after a game update this pattern will stop matching and
    // needs to be updated.
    //
    private const string SIG_CLIENT =
        "48 8B 05 ? ? ? ? " +   // mov rax,[rip+d32]     — insnLen=7, disp@+3
        "48 85 C0 "              +   // test rax,rax
        "74 08 "                 +   // jz +8
        "48 8B 80 30 01 00 00 "  +   // mov rax,[rax+0x130] — OFF_pClient fixed to pick right fn
        "C3";

    // SIG_VIEW — function epilogue immediately after the viewmatrix getter call:
    //   ...
    //   lea  rcx, [rip+viewmatrix_base]    ; 48 8D 0D ?? ?? ?? ??  (7 bytes, at sig-7)
    //   call GetViewMatrix                 ; E8 ?? ?? ?? ??          (5 bytes = sig)
    //   add  rsp, 0x328                    ; 48 81 C4 28 03 00 00
    //   pop  rdi                           ; 5F
    //   pop  rbp                           ; 5D
    //   ret                                ; C3
    //
    private const string SIG_VIEW =
        "E8 ? ? ? ? "           +   // call (sig[0])
        "48 81 C4 28 03 00 00 " +   // add rsp,0x328
        "5F 5D C3";

    // SIG_TOD — time-of-day byte flags (setnoon / setnight) in the weather
    // manager constructor.  The function loads a global qword, LEAs the
    // "setnoon" string, calls a registration helper, then MOVZX-loads the
    // setnoon byte flag, LEAs "setnight", and tests AL.
    //
    //   48 8B 0D [d32]            ; mov  rcx, [rip+qword_global]
    //   48 8D 15 [d32]            ; lea  rdx, [rip+aSetnoon]   "setnoon"
    //   E8 [d32]                  ; call registration_helper
    //   0F B6 0D [d32]            ; movzx ecx, byte [rip+setnoon_flag]  ← target
    //   48 8D 15 [d32]            ; lea  rdx, [rip+aSetnight]  "setnight"
    //   84 C0                     ; test al, al
    //
    // The MOVZX resolves to RVA_setnoon.  RVA_setnight = setnoon + 1
    // (the two flags are consecutive bytes in .data).
    //
    private const string SIG_TOD =
        "48 8B 0D ? ? ? ? "  +   // mov rcx,[rip+d32]
        "48 8D 15 ? ? ? ? "  +   // lea rdx,[rip+d32]  (\"setnoon\")
        "E8 ? ? ? ? "         +   // call
        "0F B6 0D ? ? ? ? "  +   // movzx ecx,byte [rip+d32]  — setnoon flag
        "48 8D 15 ? ? ? ? "  +   // lea rdx,[rip+d32]  (\"setnight\")
        "84 C0";                  // test al,al

    // SIG_TOD layout: MOVZX ecx,byte [rip+d32] starts at byte 19 of the match.
    private const int TOD_MOVZX_DELTA    = 19;  // offset from match start to movzx instruction
    private const int TOD_MOVZX_DISP_OFF = 3;   // disp32 offset within the movzx (0F B6 0D [d32])
    private const int TOD_MOVZX_INSN_LEN = 7;   // movzx instruction length

    // SIG_RECOIL — global float access used for recoil/spread:
    //   movss xmm0, [rip+recoil]           ; F3 0F 10 05 ?? ?? ?? ??  (8 bytes, disp@+4)
    //   lea   rdx, [rsp+??]                ; 48 8D 54 24 ??
    //   mulss xmm0, xmm7                   ; F3 0F 59 C7
    //
    private const string SIG_RECOIL =
        "F3 0F 10 05 ? ? ? ? "  +   // movss xmm0,[rip+d32]  — insnLen=8, disp@+4
        "48 8D 54 24 ? "         +   // lea rdx,[rsp+??]
        "F3 0F 59 C7";

    // ── Instruction layouts for resolver ─────────────────────────────────────

    // SIG_CLIENT: first instruction (mov rax,[rip+d32])
    private const int CLIENT_RIP_DISP_OFF  = 3;  // disp32 offset in instruction
    private const int CLIENT_RIP_INSN_LEN  = 7;  // instruction length → RIP advances by 7

    // SIG_CLIENT: third instruction (mov rax,[rax+d32]) relative to sig start
    // Offset 12 = start of the 'mov rax,[rax+OFF_pClient]' instruction (48 8B 80)
    // Offset 15 = where the 4-byte disp32 for OFF_pClient lives
    private const int CLIENT_PCLIENT_EMB   = 15;

    // SIG_VIEW: LEA RCX,[rip+d32] is 7 bytes before the E8 (call)
    private const int VIEW_LEA_DELTA       = -7;  // sig - 7 = start of LEA
    private const int VIEW_LEA_DISP_OFF    = 3;   // disp32 inside the LEA
    private const int VIEW_LEA_INSN_LEN    = 7;
    // Expected opcode bytes for the LEA: 48 8D 0D
    private const byte VIEW_LEA_B0 = 0x48, VIEW_LEA_B1 = 0x8D, VIEW_LEA_B2 = 0x0D;

    // SIG_RECOIL: movss xmm0,[rip+d32]
    private const int RECOIL_DISP_OFF     = 4;
    private const int RECOIL_INSN_LEN     = 8;

    // SIG_MAGICBULLET — BALLISTIC_NOLOCALPLAYER_FIREROUND registration:
    //   mov  r9d, 0xFF4488AAh   ; 41 B9 AA 88 44 FF  (unique color constant)
    //   lea  r8,  [rip+d32]     ; 4C 8D 05 ?? ?? ?? ??  (7 bytes, disp@+3) → profile data
    //   lea  rdx, [rip+d32]     ; 48 8D 15 ?? ?? ?? ??  ("BALLISTIC_NOLOCALPLAYER_FIREROUND")
    //
    // Target byte = resolved_data + 9 (collision mask hi-byte, default 0x98)
    //
    private const string SIG_MAGICBULLET =
        "41 B9 AA 88 44 FF "  +   // mov r9d, 0xFF4488AA  (unique anchor)
        "4C 8D 05 ? ? ? ? "  +   // lea r8,[rip+d32]  → FIREROUND profile data
        "48 8D 15 ? ? ? ?";       // lea rdx,[rip+d32] → string

    // SIG_MAGICBULLET layout: LEA r8 starts at match+6
    private const int MB_LEA_DELTA    = 6;   // offset from match start to LEA r8
    private const int MB_LEA_DISP_OFF = 3;   // disp32 offset within LEA (4C 8D 05 [d32])
    private const int MB_LEA_INSN_LEN = 7;   // LEA instruction length

    // ─────────────────────────────────────────────────────────────────────────

    private readonly byte[] _data;
    private readonly ulong  _moduleBase;

    public OffsetDumper(byte[] moduleData, ulong moduleBase)
    {
        _data       = moduleData;
        _moduleBase = moduleBase;
    }

    /// <summary>Scans all signatures and returns a filled DumpResult, or null on failure.</summary>
    public DumpResult? Run()
    {
        var result = new DumpResult
        {
            Timestamp  = DateTime.UtcNow,
            ModuleBase = _moduleBase,
        };

        if (!ScanClient(result))   return null;
        if (!ScanView(result))     return null;
        if (!ScanRecoil(result))   return null;
        ScanTimeOfDay(result);      // non-fatal — dumper continues if not found
        ScanMagicBullet(result);    // non-fatal — dumper continues if not found

        ScanStructOffsets(result);
     
        //result.PrintSummary();
        return result;
    }

    // ── SIG_CLIENT ───────────────────────────────────────────────────────────

    private bool ScanClient(DumpResult r)
    {
        Console.Write("[*] Searching Sig: Client ... ");
        int off = PatternScanner.FindFirst(_data, SIG_CLIENT);
        if (off < 0)
        {
            Console.Error.WriteLine("NOT FOUND.");
            Console.Error.WriteLine("    Signature may need updating after a game patch.");
            return false;
        }
        Console.WriteLine($"");

        r.RVA_rlclient_global = SigResolver.ResolveRipRva(
            _data, off, CLIENT_RIP_DISP_OFF, CLIENT_RIP_INSN_LEN);

        r.OFF_pClient = SigResolver.ReadEmbeddedU32(_data, off, CLIENT_PCLIENT_EMB);
        r.StructOffsets["OFF_pClient"] = r.OFF_pClient;
        r.ScannedOffsets.Add("OFF_pClient");

        Console.WriteLine($"    rlclient_global RVA = 0x{r.RVA_rlclient_global:X}");
        Console.WriteLine($"    pClient         = 0x{r.OFF_pClient:X}");
        return true;
    }

    // ── SIG_VIEW ─────────────────────────────────────────────────────────────

    private bool ScanView(DumpResult r)
    {
        Console.Write("[*] Searching Sig: View ... ");
        int off = PatternScanner.FindFirst(_data, SIG_VIEW);
        if (off < 0)
        {
            Console.Error.WriteLine("NOT FOUND.");
            Console.Error.WriteLine("    Signature may need updating after a game patch.");
            return false;
        }

        int leaOff = off + VIEW_LEA_DELTA;
        if (leaOff < 0)
        {
            Console.Error.WriteLine("[!] SIG_VIEW LEA offset is negative — bad match.");
            return false;
        }

        // Sanity: verify the bytes are actually 'LEA RCX,[RIP+d32]' (48 8D 0D)
        if (!SigResolver.CheckBytes(_data, leaOff, VIEW_LEA_B0, VIEW_LEA_B1, VIEW_LEA_B2))
        {
            Console.Error.WriteLine(
                $"[!] Expected LEA RCX,[rip+d32] (48 8D 0D) at RVA 0x{leaOff:X} but got " +
                $"{_data[leaOff]:X2} {_data[leaOff+1]:X2} {_data[leaOff+2]:X2}.");
            Console.Error.WriteLine("    Trying second match ...");

            // Try subsequent matches
            var all = PatternScanner.FindAll(_data, SIG_VIEW);
            bool found = false;
            foreach (int candidate in all)
            {
                int cLea = candidate + VIEW_LEA_DELTA;
                if (cLea >= 0 && SigResolver.CheckBytes(
                        _data, cLea, VIEW_LEA_B0, VIEW_LEA_B1, VIEW_LEA_B2))
                {
                    off    = candidate;
                    leaOff = cLea;
                    found  = true;
                    Console.Error.WriteLine($"    Using match at RVA 0x{off:X}.");
                    break;
                }
            }
            if (!found)
            {
                Console.Error.WriteLine("[!] No valid SIG_VIEW match with LEA prefix found.");
                return false;
            }
        }
       Console.WriteLine($"");

        r.RVA_viewmatrix_base = SigResolver.ResolveRipRva(
            _data, leaOff, VIEW_LEA_DISP_OFF, VIEW_LEA_INSN_LEN);

        Console.WriteLine($"    viewmatrix_base RVA = 0x{r.RVA_viewmatrix_base:X}");
        return true;
    }

    // ── SIG_RECOIL ───────────────────────────────────────────────────────────

    private bool ScanRecoil(DumpResult r)
    {
        Console.Write("[*] Searching Sig: Recoil ... ");
        int off = PatternScanner.FindFirst(_data, SIG_RECOIL);
        if (off < 0)
        {
            Console.Error.WriteLine("NOT FOUND.");
            Console.Error.WriteLine("    Signature may need updating after a game patch.");
            return false;
        }
        Console.WriteLine($"");

        r.RVA_recoil = SigResolver.ResolveRipRva(
            _data, off, RECOIL_DISP_OFF, RECOIL_INSN_LEN);

        r.RVA_spread = r.RVA_recoil >= 4 ? r.RVA_recoil - 4 : 0;

        Console.WriteLine($"    recoil RVA = 0x{r.RVA_recoil:X}");
        Console.WriteLine($"    spread RVA = 0x{r.RVA_spread:X}");
        return true;
    }

    // ── SIG_TOD (time-of-day flags) ───────────────────────────────────────

    private void ScanTimeOfDay(DumpResult r)
    {
        Console.Write("[*] Searching Sig: TimeOfDay ... ");
        int off = PatternScanner.FindFirst(_data, SIG_TOD);
        if (off < 0)
        {
            Console.Error.WriteLine("NOT FOUND.");
            Console.Error.WriteLine("    setnoon/setnight flags will be 0.");
            return;
        }
        Console.WriteLine($"");

        int movzxOff = off + TOD_MOVZX_DELTA;
        r.RVA_setnoon  = SigResolver.ResolveRipRva(
            _data, movzxOff, TOD_MOVZX_DISP_OFF, TOD_MOVZX_INSN_LEN);
        r.RVA_setnight = r.RVA_setnoon + 1;

        Console.WriteLine($"    setnoon  RVA = 0x{r.RVA_setnoon:X}");
        Console.WriteLine($"    setnight RVA = 0x{r.RVA_setnight:X}");
    }

    // ── SIG_MAGICBULLET ──────────────────────────────────────────────────

    private void ScanMagicBullet(DumpResult r)
    {
        Console.Write("[*] Searching Sig: MagicBullet ... ");
        int off = PatternScanner.FindFirst(_data, SIG_MAGICBULLET);
        if (off < 0)
        {
            Console.Error.WriteLine("NOT FOUND.");
            Console.Error.WriteLine("    MagicBullet RVA will be 0.");
            return;
        }
        Console.WriteLine($"");

        // LEA r8,[rip+d32] starts at match+6, disp32 at +3 within the insn, insn is 7 bytes
        ulong dataRva = SigResolver.ResolveRipRva(_data, off + MB_LEA_DELTA, MB_LEA_DISP_OFF, MB_LEA_INSN_LEN);
        r.RVA_magicbullet = dataRva + 9;  // +9 = collision mask hi-byte (0x98)

        Console.WriteLine($"    magicbullet RVA = 0x{r.RVA_magicbullet:X}");
    }

// ── Struct-offset signature table ─────────────────────────────────────────
    //
    // Field layout: (name, byteOffsetFromMatchStart, byteSize)
    //   byteSize=4 → ReadEmbeddedU32  (disp32 little-endian, e.g. [r?+0x4F0])
    //   byteSize=1 → ReadEmbeddedU8   (disp8  zero-extended, e.g. [r?+0x10])
    //
    private record struct OffsetField(string Name, int Offset, int Size);

    private sealed class StructSig
    {
        public required string        Tag;
        public required string        Pattern;
        public required OffsetField[] Fields;
        /// <summary>
        /// Static fallback values used when the pattern fails to match.
        /// Also used as the sole source when Pattern is empty (static-only entries).
        /// </summary>
        public Dictionary<string, ulong>? Fallback { get; set; }
    }

    private static readonly StructSig[] _structSigs =
    [
        // ── pObject (offset from pclient) ────────────────────────────────────────
        // Anchors: LOCK INC [r12+8] (ref-count bump) + MOV [rbp+0x58] (frame store)
        // + TEST/JZ immediately following the pointer chain walk.
        // ALL struct disp bytes (pObject, stage0, stage1) are fully wildcarded.
        //
        // 48 8B ?? [??]                    ; mov rcx, [rax+OFF_pObject]  @+3 disp8
        // 48 8B ?? [?? ?? ?? ??]           ; mov rax, [rcx+stage0]       (wildcarded)
        // 4C 8B ?? [??]                    ; mov r12, [rax+stage1]       (wildcarded)
        // 4C 89 65 58                      ; mov [rbp+0x58], r12         (STABLE frame store)
        // 4D 85 E4 74 06                   ; test r12,r12 / jz +6        (STABLE)
        // F0 41 FF 44 24 08                ; lock inc [r12+8]             (STABLE ref-count bump)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_POBJECT",
            Pattern = "48 8B ?? ?? 48 8B ?? ?? ?? ?? ?? 4C 8B ?? ?? 4C 89 65 58 4D 85 E4 74 06 F0 41 FF 44 24 08",
            Fields  =
            [
                new("OFF_pObject", 3, 1),
            ],
            // // Fallback = new() { { ["OFF_pObject"] = 0x28 }
        },

        // ── Entity array + count ─────────────────────────────────────────────
        // Located in sub_7FF66D787670 — the entity update/tick function.
        // Old signature (INC EAX + backward JMP) matched a stale array at
        // pobject+0x4F0 (stride 16, no longer used for entities).
        // The active entity array lives at pobject+0x500 (stride 8).
        //
        // Anchors: XORPS xmm6,xmm6 (0F 57 F6) + MOV rdi,rcx (48 8B F9) +
        //          MOV ebp,r13d (41 8B ED) + CVTSI2SS prefix (F3 48 0F 2A).
        // ALL struct disp32 bytes are fully wildcarded.
        //
        // 0F 57 F6                  ; xorps xmm6, xmm6              (STABLE anchor)
        // ?? 89 ?? [?? ?? ?? ??]    ; mov [rcx+off1], r?d            (wildcarded clear)
        // 48 8B F9                  ; mov rdi, rcx                   (STABLE anchor)
        // ?? 89 ?? [?? ?? ?? ??]    ; mov [rcx+off2], r?d            (wildcarded clear)
        // 41 8B ED                  ; mov ebp, r13d                  (STABLE anchor)
        // 48 8B 05 [?? ?? ?? ??]   ; mov rax, [rip+globalPtr]       (wildcarded)
        // ?? ?? ?? ?? ??            ; mov esi, imm32                 (wildcarded)
        // ?? 8B ?? [?? ?? ?? ??]   ; mov r?, [rcx+entityArray]      @+38 (disp32)
        // ?? 8B ?? [?? ?? ?? ??]   ; mov r?, [rcx+entityCount]      @+45 (disp32)
        // F3 48 0F 2A ?? ??         ; cvtsi2ss xmm?, qword [rax+8]  (STABLE anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_ENTITY_ARRAY",
            Pattern = "0F 57 F6 ?? 89 ?? ?? ?? ?? ?? 48 8B F9 ?? 89 ?? ?? ?? ?? ?? 41 8B ED 48 8B 05 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? F3 48 0F 2A ?? ??",
            Fields  =
            [
                new("OFF_entityArray", 38, 4),
                new("OFF_entityCount", 45, 4),
            ],
        },

        // ── Entity world-space position (vec3 X/Y/Z) ─────────────────────────
        // Robust, update-proof signature for entityPos (local player/entity):
        // Anchors on unique, stable code bytes before the movss instruction.
        // The offset bytes are fully wildcarded, so the signature survives updates.
        // When the struct layout changes and the offset moves (e.g., from 0x80 to 0x84),
        // the signature will still match and extract the new offset automatically.
        //
        // Example code region:
        //   ...
        //   74 06 8B 45 ?? 89 43 10 4C 8B 75 F8
        //   F3 0F 10 87 ?? ?? ?? ??    ; movss xmm0,[rdi+entityPos] (offset wildcarded)
        //
        // Pattern: 74 06 8B 45 ?? 89 43 10 4C 8B 75 F8 F3 0F 10 87 ?? ?? ?? ??
        // Extraction offset: 16 (start of disp32 in movss)
        //
        // This ensures the dumper always finds the correct offset, even after updates.
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_ENTITY_POS",
            Pattern = "74 06 8B 45 ?? 89 43 10 4C 8B 75 F8 F3 0F 10 87 ?? ?? ?? ??",
            Fields  =
            [
                new("OFF_entityPos", 16, 4),
            ],
            // // Fallback = new() { { ["OFF_entityPos"] = 0x80 }
        },

        // ── Local-player pointer chain  stage0 → stage1 → stage2 ────────────
        // Anchors: two RIP-relative MOVSS loads of global float constants into
        // xmm8 and xmm9 (stable opcodes) and a short jump over an alternative
        // path (EB ??), then MOV rax,[rbp+0x30] (stack slot — stable).
        // ALL struct disp bytes (stage0, stage1, stage2) are fully wildcarded.
        //
        // F3 44 0F 10 05 [d32]    ; movss xmm8, [rip+??]   (STABLE opcode+reg)
        // F3 44 0F 10 0D [d32]    ; movss xmm9, [rip+??]   (STABLE opcode+reg)
        // EB ??                    ; jmp short               (STABLE)
        // 48 8B 45 30              ; mov rax,[rbp+0x30]      (STABLE stack slot)
        // 48 8B ?? [?? ?? ?? ??]  ; mov rcx,[rax+stage0]    @+27 wildcarded
        // 48 8B ?? [??]           ; mov rax,[rcx+stage1]    @+34 wildcarded
        // 48 85 C0 74 ??          ; test/jz                  (STABLE)
        // 48 8B ?? [??]           ; mov rax,[rax+stage2]    @+43 wildcarded
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_STAGE_CHAIN",
            Pattern = "F3 44 0F 10 05 ?? ?? ?? ?? F3 44 0F 10 0D ?? ?? ?? ?? EB ?? 48 8B 45 30 48 8B ?? ?? ?? ?? ?? 48 8B ?? ?? 48 85 C0 74 ?? 48 8B ?? ??",
            Fields  =
            [
                new("OFF_localStage0", 27, 4),
                new("OFF_localStage1", 34, 1),
                new("OFF_localStage2", 43, 1),
            ],
            // // Fallback = new() { { ["OFF_localStage0"] = 0x328, ["OFF_localStage1"] = 0x10, ["OFF_localStage2"] = 0x30 }
        },

        // ── ViewProjection matrix start (float[16] at viewmatrix_ptr+OFF_viewProj) ─
        // Renderer matrix-load path: 6 consecutive MOVAPS loads (rows -1 through 4)
        // then a MOVSD store to frame slot [rbp+0x20].
        // All 6 MOVAPS disp32 bytes are fully wildcarded; F2 0F 11 45 20
        // (movsd [rbp+0x20], xmm0) after them is a pure code anchor
        // (0x20 is a shadow/home slot, never a struct offset).
        //
        // 0F 28 ?? [?? ?? ?? ??] x6  ; movaps rows, all wildcarded  @+3 = viewProj
        // F2 0F 11 45 20             ; movsd [rbp+0x20], xmm0       (STABLE anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_VIEW_PROJ",
            Pattern = "0F 28 ?? ?? ?? ?? ?? 0F 28 ?? ?? ?? ?? ?? 0F 28 ?? ?? ?? ?? ?? 0F 28 ?? ?? ?? ?? ?? 0F 28 ?? ?? ?? ?? ?? F2 0F 11 45 20",
            Fields  =
            [
                new("OFF_viewProj", 3, 4),
            ],
            // // Fallback = new() { { ["OFF_viewProj"] = 0x450 }
        },

        // ── Health + healthMax ───────────────────────────────────────────────
        // Structural anchor: unique 'mov r12d, 2' followed by 5 consecutive
        // byte-struct field reads in the damage-calculation function.
        // health is the 4th read, healthMax the 5th.
        //
        // 41 BC 02 00 00 00   ; mov  r12d, 2              (anchor)
        // 44 8B ?? ??  44 8B ?? ??  8B ?? ??              ; 3 preceding reads
        // 8B ?? [??]          ; mov reg, [rax+OFF_health]    @+19 (disp8, wildcarded)
        // 8B ?? [??]          ; mov reg, [rax+OFF_healthMax] @+22 (disp8, wildcarded)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_HEALTH",
            Pattern = "41 BC 02 00 00 00 44 8B ?? ?? 44 8B ?? ?? 8B ?? ?? 8B ?? ?? 8B ?? ??",
            Fields  =
            [
                new("OFF_health",    19, 1),
                new("OFF_healthMax", 22, 1),
            ],
        },

        // ── isDowned flag ────────────────────────────────────────────────────
        // Structural anchor: virtual call at [rax+0x78] + cmp eax,1 + short-jnz,
        // unique to the entity-state iterator that checks the downed condition.
        //
        // FF 50 78            ; call  [rax+0x78]           (vcall anchor)
        // 83 F8 01            ; cmp   eax, 1
        // 75 ??               ; jnz   short
        // ?? 8B ?? [?? ?? ?? ??]  ; mov r?, [rbx+OFF_isDowned]  @+10 (disp32, wildcarded)
        // 85 ??               ; test  reg, reg
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_IS_DOWNED",
            Pattern = "FF 50 78 83 F8 01 75 ?? 8B ?? ?? ?? ?? ?? 85 ??",
            Fields  =
            [
                new("OFF_isDowned", 10, 4),
            ],
        },

        // ── Entity name (inline char[] at entity+OFF_name) ───────────────────
        // Inside the entity name-copy function (sub_7FF75E1BFA50).
        // Anchored on MOV [rdx+0x10],eax (89 42 10) immediately before the LEA.
        // 0x10 is a fixed field in the DESTINATION struct — not an entity offset —
        // so it is stable even when OFF_name changes.
        // ALL entity-side disp32 bytes (OFF_name and OFF_name+0x10) are wildcarded.
        //
        //
        // 89 42 10                 ; mov [rdx+0x10], eax          (STABLE anchor)
        // 49 8D ?? [?? ?? ?? ??]   ; lea rdx,[r8+OFF_name]   @+6 wildcarded
        // E8 ?? ?? ?? ??           ; call <string copy>           (wildcarded)
        // 0F B6 ?? [?? ?? ?? ??]   ; movzx r?,[r?+name+0x10]      wildcarded
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_NAME",
            Pattern = "89 42 10 49 8D ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B6 ?? ?? ?? ?? ??",
            Fields  =
            [
                new("OFF_name", 6, 4),
            ],
            // // Fallback = new() { { ["OFF_name"] = 0x430 }
        },

        // ── Type pointer at entity+0x228 ─────────────────────────────────────
        // Three consecutive narrowing reads in the entity-type classifier.
        // The middle MOVZX offset (0x222) is kept as structural anchor; the first
        // MOVZX disp32 and the final MOV disp32 (OFF_type) are wildcarded.
        //
        // 0F B7 ?? [?? ?? ?? ??]  ; movzx ?, word[r?+??]        (first, wildcarded)
        // (4-byte gap)
        // 0F B7 ?? 22 02 00 00    ; movzx ?, word[r?+0x222]     (anchor)
        // (4-byte gap)
        // 48 8B ?? [?? ?? ?? ??]  ; mov r?,  [r?+OFF_type]  @+25 (wildcarded)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_TYPE",
            Pattern = "0F B7 ?? ?? ?? ?? ?? ?? ?? ?? ?? 0F B7 ?? 22 02 00 00 ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ??",
            Fields  =
            [
                new("OFF_type", 25, 4),
            ],
        },

        // ── Agent armor + armorMax ───────────────────────────────────────────
        // NOTE: The agent-armor conditional-clamp function and the npc-armor
        // conditional-clamp function are STRUCTURALLY IDENTICAL — same CMOV/CMP
        // sequence, only the offset bytes differ.  No pure-code anchor can
        // distinguish them, so the scanned offset 0x254 is intentionally kept
        // in the pattern to uniquely select the agent-armor variant.
        // IDA: 4 matches — ALL encode 0x254/0x258 → FindFirst is always safe.
        // Fallback protects if the game updates and changes these offsets.
        new StructSig
        {
            Tag     = "SIG_AGENT_ARMOR",
            Pattern = "?? 8B ?? 54 02 00 00 ?? ?? ?? ?? ?? ?? ?? 8B ?? 58 02 00 00",
            Fields  =
            [
                new("OFF_agentarmor",    3, 4),
                new("OFF_agentarmorMax", 16, 4),
            ],
            // // Fallback = new() { { ["OFF_agentarmor"] = 0x254, ["OFF_agentarmorMax"] = 0x258 }
        },

        // ── pvpData pointer + pvpTeamId ──────────────────────────────────────
        // Structural anchor: LOCK INC [rbx+8] (interlocked reference-count bump)
        // immediately before the double null-check chain guarding pvpData access.
        //
        // F0 FF 43 08                    ; lock inc [rbx+8]           (anchor)
        // 48 85 ?? 74 ??                 ; test/jz
        // 48 8B ?? ??                    ; mov rax, [rbx+disp8]
        // 48 85 ?? 74 ??                 ; test/jz
        // 48 8B ?? [?? ?? ?? ??]         ; mov r?, [rax+OFF_pvpData]   @+21 (wildcarded)
        // 48 85 ?? 74 ??                 ; test/jz
        // 8B  ?? [??]                    ; mov r?, [r?+OFF_pvpTeamId]  @+32 (wildcarded)
        //
        // IDA: 1 unique match.  Both extracted offsets fully wildcarded.
        new StructSig
        {
            Tag     = "SIG_PVP_DATA",
            Pattern = "F0 FF 43 08 48 85 ?? 74 ?? 48 8B ?? ?? 48 85 ?? 74 ?? 48 8B ?? ?? ?? ?? ?? 48 85 ?? 74 ?? 8B ?? ??",
            Fields  =
            [
                new("OFF_pvpData",   21, 4),
                new("OFF_pvpTeamId", 32, 1),
            ],
        },

        // ── DZ rogue event manager component pointer (entity → rogueComp) ─────────
        // Found in sub_7FF75E4B8470 (DZ hostile sweep).  After an optional-flag
        // byte check, the function loads entity+rogueComp, null-tests it, then
        // calls sub_7FF75CED9660 (isConflict) and branches on the result.
        // Both disp32 bytes (the flag offset and rogueComp itself) are fully
        // wildcarded so the pattern survives struct-layout changes.
        //
        // 38 8A [?? ?? ?? ??]      ; cmp byte[rdx+??], cl   (flag offset, wildcarded)
        // 74 ??                    ; jz short
        // 48 8B 8A [?? ?? ?? ??]   ; mov rcx,[rdx+rogueComp]  @+11 ← EXTRACT
        // 48 85 C9                 ; test rcx,rcx              (STABLE)
        // 74 ??                    ; jz short
        // E8 ?? ?? ?? ??           ; call sub_7FF75CED9660     (wildcarded)
        // 84 C0                    ; test al,al                (STABLE)
        // 75 ??                    ; jnz short
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_ROGUE_COMP",
            Pattern = "38 8A ?? ?? ?? ?? 74 ?? 48 8B 8A ?? ?? ?? ?? 48 85 C9 74 ?? E8 ?? ?? ?? ?? 84 C0 75 ??",
            Fields  =
            [
                new("OFF_rogueComp", 11, 4),
            ],
            // Fallback = new() { { ["OFF_rogueComp"] = 0x730 },
        },

        // ── DZ rogue team index (rogueComp → rogueTeamIdx) ───────────────────
        // Found in sub_7FF66DCD7070 (rogue state update wrapper).
        // Calls sub_7FF66C6AAFB0 (isRogue) before/after sub_7FF66C73C650
        // (the actual rogue setter which writes rogueComp+0x6C).
        //
        // Prologue anchor: `56 57 41 56 48 83 EC 20` (push rsi/rdi/r14 + sub rsp,20h)
        // Key read: `8B ?? [??]` — mov eax,[rcx+rogueTeamIdx]  @+10 ← EXTRACT
        // Register save: `4D 8B F1 49 8B F0 8B EA 48 8B D9` — stable 10-byte
        //   register-assignment block (r14=r9, rsi=r8, ebp=edx, rbx=rcx).
        // Compare: `3B C2` — cmp eax,edx (current vs new team index).
        // Sentinel anchor: `83 F8 FF` (cmp eax,-1) — tests the SHD sentinel value
        //   (0xFFFFFFFF) that marks a clean/non-rogue agent.
        // Secondary anchor: `41 83 38 09` (cmp [r8],9) — tests rogue stage == 9
        //   (manhunt threshold).
        //
        // 56 57 41 56 48 83 EC 20  ; push rsi,rdi,r14 / sub rsp,20h (STABLE)
        // 8B ?? [??]                ; mov eax,[rcx+rogueTeamIdx] @+10 (EXTRACT, disp8)
        // 4D 8B F1                  ; mov r14,r9                     (STABLE)
        // 49 8B F0                  ; mov rsi,r8                     (STABLE)
        // 8B EA                     ; mov ebp,edx                    (STABLE)
        // 48 8B D9                  ; mov rbx,rcx                    (STABLE)
        // 3B C2                     ; cmp eax,edx                    (STABLE)
        // 74 ??                     ; jz short                       (wildcarded)
        // 83 F8 FF                  ; cmp eax,-1  (SHD sentinel)     (STABLE)
        // 75 ??                     ; jnz short                      (wildcarded)
        // 41 83 38 09               ; cmp dword ptr [r8],9           (STABLE)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_ROGUE_TEAM_IDX",
            Pattern = "56 57 41 56 48 83 EC 20 "
                    + "8B ?? ?? "
                    + "4D 8B F1 49 8B F0 8B EA 48 8B D9 "
                    + "3B C2 74 ?? 83 F8 FF 75 ?? 41 83 38 09",
            Fields  =
            [
                new("OFF_rogueTeamIdx", 10, 1),
            ],
            //// Fallback = new() { { ["OFF_rogueTeamIdx"] = 0x6C },
        },

        // ── NPC armor + npcArmorMax ──────────────────────────────────────────
        // Found in the npc-stat copy function — field loads into consecutive
        // stack frame slots.  The stores 89 45 30, 89 45 34, 89 45 38 are NOT
        // entity struct offsets (they are compiler-assigned stack slots), so
        // they are stable anchors even when the struct layout changes.
        //
        // 89 45 30                 ; mov [rbp+0x30], eax            (STABLE)
        // 41 8B ?? [?? ?? ?? ??]   ; mov eax,[r8+OFF_npcArmor]     @+6 wildcarded
        // 89 45 34                 ; mov [rbp+0x34], eax            (STABLE)
        // 41 8B ?? [?? ?? ?? ??]   ; mov eax,[r8+OFF_npcArmorMax]   @+16 wildcarded
        // 89 45 38                 ; mov [rbp+0x38], eax            (STABLE)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_NPC_ARMOR",
            Pattern = "89 45 30 41 8B ?? ?? ?? ?? ?? 89 45 34 41 8B ?? ?? ?? ?? ?? 89 45 38",
            Fields  =
            [
                new("OFF_npcArmor",     6, 4),
                new("OFF_npcArmorMax", 16, 4),
            ],
            // // Fallback = new() { { ["OFF_npcArmor"] = 0x1C4, ["OFF_npcArmorMax"] = 0x1C8 }
        },
        // ── HP pointer (entity → hpPtr) ──────────────────────────────────────
        // Found in the entity health-getter.  The function:
        //   1. Tests rbx (entity ptr) for null and jumps away if zero.
        //   2. Loads vtable from [rbx] then calls virtual slot 0x78 (isAlive —
        //      same vcall as SIG_IS_DOWNED).
        //   3. If result == 1, loads hpPtr from entity and calls the health getter.
        //   4. Stores the result float to [rdi] via MOVSS.
        // Every byte in this pattern is a pure code byte; zero struct offsets.
        //
        // 48 85 DB 74 ??          ; test rbx,rbx / jz              (STABLE)
        // 48 8B 03 48 8B CB       ; mov rax,[rbx]; mov rcx,rbx     (STABLE)
        // FF 50 78                ; call [rax+0x78]  vcall slot     (STABLE)
        // 83 F8 01 75 ??          ; cmp eax,1 / jnz                (STABLE)
        // 48 8B ?? [?? ?? ?? ??]  ; mov r?,[rbx+OFF_HpPtr]   @+22  wildcarded
        // E8 ?? ?? ?? ??          ; call health getter              (wildcarded)
        // F3 0F 11 07             ; movss [rdi],xmm0               (STABLE store)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_HP_PTR",
            Pattern = "48 85 DB 74 ?? 48 8B 03 48 8B CB FF 50 78 83 F8 01 75 ?? 48 8B ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? F3 0F 11 07",
            Fields  =
            [
                new("OFF_HpPtr", 22, 4),
            ],
            // // Fallback = new() { { ["OFF_HpPtr"] = 0x7C8 }
        },

        // ── Faction context pointer ───────────────────────────────────────────
        // Found inside the faction-dispatch function at the point where it looks
        // up the faction context on a freshly obtained service object.
        // Anchors: XOR EDX,EDX (33 D2) + CALL + MOV R8,RAX (4C 8B C0) are
        // pure code bytes with zero struct offsets.  The TEST+JZ-near that guards
        // the null check completes the signature.
        //
        // 33 D2                    ; xor edx,edx                (STABLE)
        // E8 ?? ?? ?? ??           ; call <service factory>     (wildcarded)
        // 4C 8B C0                 ; mov r8,rax                  (STABLE)
        // 48 85 C0 0F 84 ?? ?? ?? ?? ; test rax,rax / jz near   (STABLE)
        // 48 8B ?? [?? ?? ?? ??]   ; mov r?,[rax+OFF_factionCtx] @+22 wildcarded
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_FACTION_CTX",
            Pattern = "33 D2 E8 ?? ?? ?? ?? 4C 8B C0 48 85 C0 0F 84 ?? ?? ?? ?? 48 8B ?? ?? ?? ?? ??",
            Fields  =
            [
                new("OFF_factionCtx", 22, 4),
            ],
            // // Fallback = new() { { ["OFF_factionCtx"] = 0x3E0 }
        },

        // ── Agent armor overflow pointer ─────────────────────────────────────
        // The extracted disp32 lo-byte (0x48) is wildcarded so it survives an
        // independent change; the second load's anchor `50 02 00 00` (0x250)
        // keeps uniqueness.
        //
        //   ?? 8B ?? [?? 02 00 00]   ; mov r?, [r?+armorOvf]  @+3 (lo-byte wildcarded)
        //   48 85 ?? 74 ??           ; test/jz
        //   ?? 8B ?? 50 02 00 00     ; mov r?, [r?+0x250]          (anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_AGENT_ARMOR_OVERFLOW",
            Pattern = "?? 8B ?? ?? 02 00 00 48 85 ?? 74 ?? 48 8B ?? 50 02 00 00",
            Fields  =
            [
                new("OFF_agentarmorOverflow", 3, 4),
            ],
        },

        // ── BonesBase (inner struct offset 0x220) ────────────────────────────
        // In sub_7FF75CF0E7D0 (DropDetach).  Two structural anchors:
        //   (1) The `50 02 00 00` movss float load (0x250) immediately before.
        //   (2) The `0F 28 ??` MOVAPS immediately after the extracted load.
        // The extracted bonesBase disp32 bytes are fully wildcarded.
        //
        // F3 0F 10 ?? 50 02 00 00   ; movss xmm?, [r?+0x250]    (pre-anchor)
        // ?? 8B ?? [?? ?? ?? ??]    ; mov r?,  [r?+OFF_bonesBase]  @+11 (wildcarded)
        // 0F 28 ??                  ; movaps  xmm?, xmm?           (post-anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_BONES_BASE",
            Pattern = "F3 0F 10 ?? 50 02 00 00 ?? 8B ?? ?? ?? ?? ?? 0F 28 ??",
            Fields  =
            [
                new("OFF_bonesBase", 11, 4),
            ],
        },

        // ── BoneArray (offset from bonesBase, 0x170) ─────────────────────────
        // Found in bonesBase destructor sub_7FF75C3E1CE0 at 0x7FF75C3E1CFC.
        // The FIRST block's disp32 (OFF_boneArray) is wildcarded — ReadEmbedded
        // reads it from the actual module bytes regardless of what value it holds.
        // The four subsequent blocks keep their fixed descending offsets as structural
        // anchors; if boneArray itself changes, those anchors remain intact.
        //
        //   ?? 8B ?? [?? ?? ?? ??]  48 85 ?? 74 ?? E8 ??...  ; OFF_boneArray @+3
        //   ?? 8B ??  68 01 00 00   48 85 ?? 74 ?? E8 ??...  ; -0x08 (anchor)
        //   ?? 8B ??  60 01 00 00   48 85 ?? 74 ?? E8 ??...  ; -0x10 (anchor)
        //   ?? 8B ??  58 01 00 00   48 85 ?? 74 ?? E8 ??...  ; -0x18 (anchor)
        //   ?? 8B ??  50 01 00 00                            ; -0x20 (anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_BONE_ARRAY",
            Pattern = "?? 8B ?? ?? ?? ?? ?? 48 85 ?? 74 ?? E8 ?? ?? ?? ?? "
                    + "?? 8B ?? 68 01 00 00 48 85 ?? 74 ?? E8 ?? ?? ?? ?? "
                    + "?? 8B ?? 60 01 00 00 48 85 ?? 74 ?? E8 ?? ?? ?? ?? "
                    + "?? 8B ?? 58 01 00 00 48 85 ?? 74 ?? E8 ?? ?? ?? ?? "
                    + "?? 8B ?? 50 01 00 00",
            Fields  =
            [
                new("OFF_boneArray", 3, 4),
            ],
        },

        // ── Weapon pointer chain  (weapPtr0 / weapPtr1) ──────────────────────
        // Anchored on the clean Windows x64 function prologue of sub_7FF75D963DF0.
        // Prologue bytes (home RSP saves, PUSH RDI, SUB RSP,0x70) are extremely
        // stable and contain zero struct offsets.  The two pointer loads that
        // immediately follow are fully wildcarded.
        //
        // 48 89 5C 24 10 48 89 74 24 18 57 48 83 EC 70  ; function prologue (STABLE)
        // 48 8B ?? ??     ; mov rax,[rcx+??]             (wildcarded disp8)
        // 48 8B ??        ; mov rsi,rcx                  (reg-only mov, STABLE-ish)
        // 48 8B ?? [?? ?? ?? ??]  ; mov r?,[r?+weapPtr0]  @+25 wildcarded
        // 48 8B ?? [??]           ; mov r?,[r?+weapPtr1]  @+32 wildcarded disp8
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_WEAP_PTR_CHAIN",
            Pattern = "48 89 5C 24 10 48 89 74 24 18 57 48 83 EC 70 48 8B ?? ?? 48 8B ?? 48 8B ?? ?? ?? ?? ?? 48 8B ?? ??",
            Fields  =
            [
                new("OFF_weapPtr0", 25, 4),
                new("OFF_weapPtr1", 32, 1),
            ],
            // // Fallback = new() { { ["OFF_weapPtr0"] = 0x710, ["OFF_weapPtr1"] = 0x38 }
        },

        // ── Weapon pointer chain  (weapPtr2 / rpmFinal) ──────────────────────
        // Found at exactly 1 location: 0x7FF75E49B200 (sub_7FF75E49B200).
        //
        //   40 53 48 83 EC 20          ; push rbx / sub rsp,0x20  (prologue anchor)
        //   48 8B ?? [??]              ; mov r?, [rcx+weapPtr2]   @+9  disp8 (wildcarded)
        //   48 8B ?? E8 ?? ?? ?? ??    ; mov rcx,r? / call getter
        //   48 8B ?? 60 01 00 00       ; mov r?, [rax+0x160]      (context anchor)
        //   F3 0F 10 ?? [?? ?? 00 00]  ; movss xmm?, [r?+0x268]   (context, wildcarded hi-byte)
        //   F3 0F 10 ?? [?? ?? 00 00]  ; movss xmm?, [r?+OFF_rpmFinal]  @+37 (wildcarded)
        //
        // `0x160` kept fixed as uniqueness anchor (IDA: 1 match with wildcarded disp8).
        // Float disp32 bytes wildcarded with `?? ?? 00 00` (offsets in 0x0000–0xFFFF range).
        // Byte map: 0..8 [9] 10..17  18..24  25..32  33..36 [37..40]
        new StructSig
        {
            Tag     = "SIG_WEAP_PTR2_RPM",
            Pattern = "40 53 48 83 EC 20 48 8B ?? ?? 48 8B ?? E8 ?? ?? ?? ?? "
                    + "48 8B ?? 60 01 00 00 F3 0F 10 ?? ?? ?? 00 00 "
                    + "F3 0F 10 ?? ?? ?? 00 00",
            Fields  =
            [
                new("OFF_weapPtr2",  9, 1),
                new("OFF_rpmFinal", 37, 4),
            ],
        },

        // ── Loot manager + count (from pObject) ─────────────────────────────
        // IDA: sub_7FF75D9B4D00 — RClient handler that reads world loot count.
        // Chain: rlclient → +0x130 pclient → +0x28 pobject → +0x80 lootManager
        //        → +0x30 lootCount.
        //
        // 48 8B ?? [?? ?? ?? ??]  ; mov rdx,[rax+lootManager]  @+3 disp32
        // 48 85 ??                ; test rdx, rdx
        // 74 ??                   ; jz short
        // 8B ?? [??]              ; mov ebx,[rdx+lootCount]    @+14 disp8
        // 8B ?? 1C                ; mov eax,[rcx+1Ch]          (IOOutput access)
        // 25 FF FF 00 10          ; and eax, 1000FFFFh         (STABLE anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_LOOT_MGR",
            Pattern = "48 8B ?? ?? ?? ?? ?? 48 85 ?? 74 ?? 8B ?? ?? 8B ?? 1C 25 FF FF 00 10",
            Fields  =
            [
                new("OFF_lootManager",  3, 4),
                new("OFF_lootCount",   14, 1),
            ],
            // Fallback = new() { { ["OFF_lootManager"] = 0x80, ["OFF_lootCount"] = 0x30 },
        },

        // ── Loot array pointer ─────────────────────────────────────────────
        // Derived from OFF_lootCount in ScanStructOffsets: lootArray = lootCount - 8.
        // Standard Snowdrop container layout: ptr at count-8, count at count.
        // No standalone AOB needed — derivation auto-updates if lootCount shifts.
        // (entry removed from sig table; derived in ScanStructOffsets post-loop)

        // ── Loot source position (vec3 at loot entry + OFF_lootPos) ─────────
        // IDA: GetAvailableLootPosition evaluate (vtable[15] of unk_7FF7603DA658).
        // Double-pointer null check (tries +0x48 then +0x40 from interactable
        // loot manager), followed by three consecutive MOVSS float reads for
        // the vec3 X/Y/Z.
        //
        // 48 8B ?? ??            ; mov rax,[rcx+48h]    (first ptr)
        // 48 85 C0               ; test rax, rax
        // 75 ??                  ; jnz short → read pos
        // 48 8B ?? ??            ; mov rax,[rcx+40h]    (second ptr)
        // 48 85 C0               ; test rax, rax
        // 74 ??                  ; jz short → skip
        // F3 0F 10 ?? [??]       ; movss xmm6,[rax+lootPos]   @+22 disp8
        // F3 0F 10 ?? [??]       ; movss xmm7,[rax+lootPos+4]
        // F3 44 0F 10 ?? [??]    ; movss xmm8,[rax+lootPos+8]
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_LOOT_POS",
            Pattern = "48 8B ?? ?? 48 85 C0 75 ?? 48 8B ?? ?? 48 85 C0 74 ?? F3 0F 10 ?? ?? F3 0F 10 ?? ?? F3 44 0F 10 ?? ??",
            Fields  =
            [
                new("OFF_lootPos", 22, 1),
            ],
        },

        // ── Loot source item count (u32 at loot entry + OFF_lootItemsCount) ─
        // IDA: GetInteractableLootCount evaluate (vtable[15] of unk_7FF7602D0B28).
        // Same double-pointer null check as SIG_LOOT_POS, but reads a u32
        // instead of a vec3.  Followed by IOOutput write sequence with the
        // stable AND mask anchor (25 FF FF 00 10).
        //
        // 48 8B ?? ??            ; mov rax,[rcx+48h]
        // 48 85 C0 75 ??         ; test/jnz
        // 48 8B ?? ??            ; mov rax,[rcx+40h]
        // 48 85 C0 74 ??         ; test/jz
        // 8B ?? [??]             ; mov ebx,[rax+lootItemsCount] @+20 disp8
        // 48 8B ?? 20            ; mov rcx,[rdx+20h]           (IOOutput)
        // 48 85 ??               ; test rcx, rcx
        // 74 ??                  ; jz short
        // 8B ?? 1C               ; mov eax,[rcx+1Ch]
        // 25 FF FF 00 10         ; and eax, 1000FFFFh          (STABLE anchor)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_LOOT_ITEMS",
            Pattern = "48 8B ?? ?? 48 85 C0 75 ?? 48 8B ?? ?? 48 85 C0 74 ?? 8B ?? ?? 48 8B ?? 20 48 85 ?? 74 ?? 8B ?? 1C 25 FF FF 00 10",
            Fields  =
            [
                new("OFF_lootItemsCount", 20, 1),
            ],
        },

        // ── Loot item layout (scanned from RClientNode_GetInteractableLootAtIndex_Eval) ─
        // IDA: sub_7FF75D9C5890 @ 0x7FF75D9C5906
        //
        // E8 ?? ?? ?? ??                    ; call sub_7FF75C1501F0
        // 8B 08                              ; mov ecx,[rax]
        // 3B 4E ?? 7D ?? 85 C9 78 ??         ; bounds checks against [rsi+count]
        // 48 8B 46 ??                        ; mov rax,[rsi+OFF_lootItemsBase]   @+19 (disp8)
        // 48 69 C9 ?? ?? ?? ??               ; imul rcx, rcx, OFF_lootItemStride @+23 (imm32)
        // 48 8B 94 01 ?? ?? ?? ??            ; mov rdx,[rcx+rax+OFF_lootItemData] @+31 (disp32)
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_LOOT_ITEM_LAYOUT",
            Pattern = "E8 ?? ?? ?? ?? 8B 08 3B 4E ?? 7D ?? 85 C9 78 ?? 48 8B 46 ?? 48 69 C9 ?? ?? ?? ?? 48 8B 94 01 ?? ?? ?? ??",
            Fields  =
            [
                new("OFF_lootItemsBase",  19, 1),
                new("OFF_lootItemStride", 23, 4),
                new("OFF_lootItemData",   31, 4),
            ],
        },

        // ── Loot item hash + item obj pointer offsets ────────────────────
        // Derived from OFF_lootItemData in ScanStructOffsets:
        //   OFF_lootItemObj  = OFF_lootItemData - 0x10  (ptr at entry+0xB0)
        //   OFF_lootItemHash = OFF_lootItemData - 0x08  (u32 at entry+0xB8)
        // These are adjacent 8-byte fields in the loot entry struct; confirmed
        // via IDA: entry layout is [..., itemObj, itemHash, itemData, ...].
        // Derivation auto-updates if OFF_lootItemData shifts after a game update.
        // (entries removed from sig table; derived in ScanStructOffsets post-loop)

        // ── Loot item name inner offset (static fallback) ────────────────
        // OFF_lootItemObjName = offset within ItemObj to the sd_string display name.
        // This is an inner-object offset within ItemObj (not the entry struct itself)
        // and is part of the serialized item schema — stable across updates.
        // OFF_lootItemObj and OFF_lootItemHash are derived from scanned OFF_lootItemData
        // in ScanStructOffsets (see derivation block after the main scan loop).
        new StructSig
        {
            Tag     = "SIG_LOOT_ITEM_OBJ_NAME_STATIC",
            Pattern = "",
            Fields  = [ new("OFF_lootItemObjName", 0, 0) ],
            Fallback = new() { ["OFF_lootItemObjName"] = 0x98 },
        },

        // ── Loot item quality offset (static fallback) ─────────────────────
        // Raw u8 — read and use directly.
        // Enum: 0=Worn 1=Standard 2=Specialized 5=Superior 6=HighEnd 8=GearSet 9=Exotic
        new StructSig
        {
            Tag     = "SIG_LOOT_ITEM_QUALITY_STATIC",
            Pattern = "",
            Fields  = [ new("OFF_lootItemQuality", 0, 0) ],
            Fallback = new() { ["OFF_lootItemQuality"] = 0x1B0 },
        },


        // ── Weapon aim controller + world aim target ─────────────────────────
        // Found in sub_7FF75CE981B0 (Entity:AimVector Execute).
        // Loads entity→weaponAimCtrl ptr, saves XMM registers, does a vtable
        // call on the controller, then reads the float3 worldAimPos.
        // The vtable[22] offset (0xB0) is kept fixed as uniqueness anchor.
        // ALL struct-offset bytes (weaponAimCtrl and worldAimPos) are wildcarded.
        //
        // 48 8B ?? [?? ?? ?? ??]        ; mov rbx,[rax+weaponAimCtrl]  @+3 disp32 EXTRACT
        // 0F 29 ?? 24 ??                ; movaps save (STABLE shape)
        // 48 8B CB                       ; mov rcx,rbx                (STABLE)
        // 0F 29 ?? 24 ??                ; movaps save (STABLE shape)
        // 44 0F 29 ?? 24 ??             ; movaps save xmm8 (STABLE shape)
        // 48 8B 03                       ; mov rax,[rbx]              (STABLE vtable load)
        // FF 90 B0 00 00 00             ; call [rax+0xB0]             (ANCHOR vtable[22])
        // F3 0F 10 ?? [?? ?? ?? ??]     ; movss xmm?,[rbx+aimPos.x]  @+39 disp32 EXTRACT
        // F3 0F 10 ?? ?? ?? ?? ??        ; movss xmm?,[rbx+aimPos.y]
        // F3 44 0F 10 ?? ?? ?? ?? ??     ; movss xmm8,[rbx+aimPos.z]
        //
        // IDA: 1 unique match.
        new StructSig
        {
            Tag     = "SIG_WEAPON_AIM_POS",
            Pattern = "48 8B ?? ?? ?? ?? ?? 0F 29 ?? 24 ?? 48 8B CB "
                    + "0F 29 ?? 24 ?? 44 0F 29 ?? 24 ?? 48 8B 03 "
                    + "FF 90 B0 00 00 00 "
                    + "F3 0F 10 ?? ?? ?? ?? ?? F3 0F 10 ?? ?? ?? ?? ?? "
                    + "F3 44 0F 10 ?? ?? ?? ?? ??",
            Fields  =
            [
                new("OFF_weaponAimCtrl", 3, 4),
                new("OFF_worldAimPos",  39, 4),
            ],
            // Fallback = new() { { ["OFF_weaponAimCtrl"] = 0x840, ["OFF_worldAimPos"] = 0x3778 },
        },
    ];

    // ─────────────────────────────────────────────────────────────────────────

    private void ScanStructOffsets(DumpResult r)
    {
        Console.WriteLine("[*] Scanning struct offsets ...");
        // pClient was resolved in ScanClient — echo with consistent formatting.
        Console.WriteLine($"    Scanning: {"pClient",-28} 0x{r.OFF_pClient:X}");

        foreach (var sig in _structSigs)
        {
            // Empty pattern = static-only entry; skip scan and go straight to fallback.
            int off = !string.IsNullOrEmpty(sig.Pattern)
                ? PatternScanner.FindFirst(_data, sig.Pattern)
                : -1;
    
            if (off < 0)
            {
                foreach (var f in sig.Fields)
                {
                    if (sig.Fallback != null && sig.Fallback.TryGetValue(f.Name, out ulong fb))
                    {
                        // Don't overwrite a value that was already scanned by a previous sig.
                        if (!r.StructOffsets.ContainsKey(f.Name))
                        {
                            r.StructOffsets[f.Name] = fb;
                            Console.WriteLine($"    Scanning: {f.Name.Replace("OFF_", ""),-28} 0x{fb:X} (static)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    Scanning: {f.Name.Replace("OFF_", ""),-28} NOT FOUND");
                    }
                }
                continue;
            }
            foreach (var f in sig.Fields)
            {
                uint val = SigResolver.ReadEmbedded(_data, off, f.Offset, f.Size);
                r.StructOffsets[f.Name] = val;
                r.ScannedOffsets.Add(f.Name);
                Console.WriteLine($"    Scanning: {f.Name.Replace("OFF_", ""),-28} 0x{val:X}");
            }
        }

        // ── Derived offsets ──────────────────────────────────────────────
        // These offsets are computed from already-scanned values above.
        // They track adjacent fields in known structs, so they auto-update
        // when the base offset shifts after a game patch.

        // lootArray = lootCount - 8  (Snowdrop container: ptr lives 8 bytes before count)
        if (r.StructOffsets.TryGetValue("OFF_lootCount", out ulong lootCount) && lootCount >= 8)
        {
            ulong v = lootCount - 8;
            r.StructOffsets["OFF_lootArray"] = v;
            r.ScannedOffsets.Add("OFF_lootArray");
            Console.WriteLine($"    Derived:  {"lootArray",-28} 0x{v:X} (from lootCount - 8)");
        }
        else
        {
            r.StructOffsets["OFF_lootArray"] = 0x28;
            Console.WriteLine($"    Derived:  {"lootArray",-28} 0x28 (static fallback)");
        }

        // lootItemObj  = lootItemData - 0x10  (adjacent ptr field in entry struct)
        // lootItemHash = lootItemData - 0x08  (adjacent u32/u64 field in entry struct)
        if (r.StructOffsets.TryGetValue("OFF_lootItemData", out ulong itemData) && itemData >= 0x10)
        {
            ulong obj  = itemData - 0x10;
            ulong hash = itemData - 0x08;
            r.StructOffsets["OFF_lootItemObj"]  = obj;
            r.StructOffsets["OFF_lootItemHash"] = hash;
            r.ScannedOffsets.Add("OFF_lootItemObj");
            r.ScannedOffsets.Add("OFF_lootItemHash");
            Console.WriteLine($"    Derived:  {"lootItemObj",-28} 0x{obj:X} (from lootItemData - 0x10)");
            Console.WriteLine($"    Derived:  {"lootItemHash",-28} 0x{hash:X} (from lootItemData - 0x08)");
        }
        else
        {
            r.StructOffsets["OFF_lootItemObj"]  = 0xB0;
            r.StructOffsets["OFF_lootItemHash"] = 0xB8;
            Console.WriteLine($"    Derived:  {"lootItemObj",-28} 0xB0 (static fallback)");
            Console.WriteLine($"    Derived:  {"lootItemHash",-28} 0xB8 (static fallback)");
        }

    }


}
