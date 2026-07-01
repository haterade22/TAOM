#pragma once

// ============================================================================
// Native target signatures for NativeSkinFixes hooks
// ============================================================================
//
// Each entry resolves a function inside TaleWorlds.Native.dll by byte-pattern
// scan rather than hardcoded RVA. Patterns survive build-to-build relocation
// inside a given engine version and (usually) survive minor engine patches.
//
// Authoring patterns:
//   1. Open TaleWorlds.Native.dll from the installed game in IDA / Ghidra /
//      Binary Ninja.
//   2. Jump to the historical RVA in the comment for each entry (the v1.3.15
//      upstream offset; in newer versions it'll be elsewhere — find the same
//      function by xrefs, prologue shape, or signature in TAOM-src dumps).
//   3. Capture the first ~24-32 bytes of the function. Replace any byte that
//      is part of a relative offset, RIP-relative displacement, or absolute
//      address with '?'. Conservative rule: anything that is NOT an opcode
//      or register-encoded byte gets a '?'.
//   4. Test by checking the scan returns a single match against the module.
//      If more than one match: extend the pattern with more discriminating
//      bytes. If zero matches: relax a wildcard or capture from a slightly
//      different offset.
//   5. Verify the matched address by disassembling memory at that location
//      at runtime (attach a debugger to the running game) — it should be the
//      function prologue.
//
// AUTHORED FOR BANNERLORD v1.4.6 (2026-06-30) — ALL 7 targets
// -----------------------------------------------------------
// All 7 patterns below are authored + verified against the installed v1.4.6
// TaleWorlds.Native.dll (built 2026-06-11), via tools/native_sig_author.py.
// Method summary (full evidence in each entry + docs/features/native-skin-fixes.md):
//   * RTTI-anchored: the Face_mesh + rglCloth_simulator_component vtables were
//     resolved from RTTI type descriptors; every struct offset + vtable index
//     the hooks use was verified against them.
//   * cloth_factory: pinned STRUCTURALLY (its prologue changed v1.3.15->v1.4.6)
//     by its body replicating the HairCloth hook's exact register/list/scene
//     writes; its call graph then yielded AddToList/GpuInit/HasClothData/
//     NotifyPhysics; render_list_build by its +0xE0-from-submeshes fingerprint.
//   * add_skin_meshes: pinned by INTERIOR BYTE TRIANGULATION against the genuine
//     v1.3.15 DLL (its prologue changed AND it inlined heavily, so neither a
//     prologue diff nor a call-graph heuristic finds it) — 22 surviving
//     wildcarded windows converge on 0x61C7D0; confirmed by its 2 SkinMask
//     bit-0x01 tests.
// Each pattern is single-match at its expected v1.4.6 RVA. 5 of the 7 also have
// BYTE-IDENTICAL prologues in the genuine v1.3.15 DLL (independent cross-check);
// the 2 that changed (cloth_factory, add_skin_meshes) were pinned by the methods
// above. cloth_factory is the function the 2026-06-30 crashing build
// mis-resolved into memory corruption — a naive RVA/prologue port misses it.
//
// A "<PATTERN_TBD>" stub (should none remain) makes the scanner return 0 and the
// hook stays inert (game keeps running). Re-author per the workflow in
// docs/features/native-skin-fixes.md when porting to a new engine version.
//
// See docs/features/native-skin-fixes.md (Pattern authoring) for the full
// workflow.
// ============================================================================

namespace TAOM { namespace NativeHooks { namespace Signatures {

struct TargetSignature
{
    const char* name;             // Diagnostic label, shows up in logs
    const char* pattern;          // IDA-style byte pattern, or "<PATTERN_TBD>"
    const char* fallbackPattern;  // Optional secondary pattern; nullptr = none
    int         byteOffsetFromMatch;  // Usually 0. Non-zero when the pattern
                                      // anchors at a unique caller (e.g. inside
                                      // a wrapper) and we offset to the callee.
    long long   historicalRva;    // v1.3.15 reference RVA, for IDA navigation.
                                  // Not used at runtime — informational only.
};

// CoversHeadHook target: add_skin_meshes_to_agent_entity
//   Forces SkinGenParams->visibilityMask bit 0x01 ON so Face_mesh is created
//   even when covers_head="true". Without the Face_mesh the GPU morph pipeline
//   never initializes and hand-grip morphs freeze.
constexpr TargetSignature kAddSkinMeshes = {
    /* name              */ "add_skin_meshes_to_agent_entity",
    /* pattern           */ "48 8B C4 55 53 56 57 41 54 41 55 41 56 41 57 48 8D A8 08 FF FF FF 48 81 EC B8 01 00 00 48 C7 45 18 FE",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x617B50LL
    // v1.4.6 RVA = 0x61C7D0 (single-match). Its v1.3.15 prologue CHANGED and the
    // function GREW (0x617B50 sub rsp,0xC8 / ~226 instrs / 21 calls -> 0x61C7D0
    // sub rsp,0x1B8 / ~1023 instrs / 102 calls — heavily inlined), so neither a
    // prologue diff NOR a call-graph heuristic finds it (the inlining even added
    // a Face_mesh-ctor call the 1.3.15 version lacked). Pinned by INTERIOR BYTE
    // TRIANGULATION: 22 distinct wildcarded 40-byte windows from the genuine
    // v1.3.15 body all map (via the match-RVA-minus-window-offset invariant) to
    // one v1.4.6 function start, 0x61C7D0. Confirmed by its TWO SkinMask bit-0x01
    // tests (`test byte[rsi],1` + `test byte[r15],1`) — the exact gate this hook
    // forces on. AgentVisuals layout verified STABLE: both versions reference
    // +0x8B8 (ptr) + +0x8D0 (float) at identical offsets, so the hook's +0x830
    // Face_mesh read is in-bounds (struct extends past it); +0x830 is read-only
    // (fed to a tracking set), never written, so worst case is cosmetic.
};

// HairClothHook target: cloth_factory
//   Rescues orphan cloth components left at Face_mesh+0x1A0 and registers them
//   in the entity list (rendering) + sim list (simulation). Also re-enters the
//   factory for beard cloth at Face_mesh+0x108.
constexpr TargetSignature kClothFactory = {
    /* name              */ "cloth_factory",
    /* pattern           */ "40 53 56 57 48 83 EC 40 48 C7 44 24 20 FE FF FF FF 48 8B DA 48 8B F1 48 89 54 24 60 48 85 D2 74",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x359C10LL
    // v1.4.6 RVA = 0x35B0C0 (single-match). CORRECTED 2026-06-30 after the first
    // deploy hooked the WRONG function (0x35AF00) — an ADJACENT SIBLING that
    // shares the cloth-registration body (type dispatch, +0x1E8/+0x208 lists,
    // cloth-ctor call) but takes rdx as a BYTE FLAG (`movzx r14d,dl`), not the
    // mesh. Hooking it fed our HairCloth post-process rdx values like 0x18/0xD/
    // 0x1D (indices) instead of a Face_mesh pointer -> per-call AV (SEH-caught,
    // no CTD, but the feature did nothing). The REAL factory is 0x35B0C0: it does
    // `mov rbx,rdx; mov rax,[rdx]; call[rax+0x28]` then `mov rax,[rbx]; call
    // [rax+0xA0]` — i.e. it DEREFERENCES rdx as the mesh + dispatches on its type,
    // the exact signature the hook needs (rcx=factory, rdx=mesh). Pinned by
    // INTERIOR BYTE TRIANGULATION of the genuine v1.3.15 factory 0x359C10 (166
    // votes converge on 0x35B0C0 — vs the structural body-match that misfired),
    // and its prologue is BYTE-IDENTICAL to 1.3.15 (the prologue never changed;
    // the earlier "changed prologue" note compared the wrong sibling). All 12
    // factory-struct offsets the hook writes match 1.3.15 exactly (96% body
    // overlap). LESSON: a shared-body sibling defeats structural matching —
    // triangulate + verify the ARGUMENT SIGNATURE, not just the body.
};

// HairClothHook helper: AddToList (FUN_180_C4040)
//   Inserts a pointer into a vector at factory+0x1E8 (entity list) or
//   factory+0x208 (sim list). Used by the orphan-cloth rescue path.
constexpr TargetSignature kAddToList = {
    /* name              */ "AddToList",
    /* pattern           */ "40 56 57 48 83 EC 28 4C 8B 41 08 48 8B F2 48 8B F9 4C 3B 41 10 73 24 49 8D 40 08 48 89 41 08",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x0C4040LL
    // v1.4.6 RVA = 0x0C3E90 (single-match). Prologue BYTE-IDENTICAL v1.3.15<->v1.4.6.
    // Double-confirmed: called by cloth_factory right after lea[+0x1E8]/[+0x208],
    // AND by render_list_build to add sub-meshes to the +0xE0 list.
};

// HairClothHook helper: GpuInit (FUN_180292570)
//   Initializes cloth GPU resources. Optional — if not resolved, cloth still
//   registers but GPU buffers wait until next scene tick.
constexpr TargetSignature kGpuInit = {
    /* name              */ "GpuInit",
    /* pattern           */ "48 89 74 24 20 41 56 48 83 EC 20 48 8B 01 4C 8B F2 48 8B F1 FF 90 A0 00 00 00 83 E8 01 74 7C",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x292570LL
    // v1.4.6 RVA = 0x2936E0 (single-match). Prologue BYTE-IDENTICAL v1.3.15<->v1.4.6.
    // Confirmed: cloth_factory calls it after `mov rdx,[scene+0x20]` (gpuResource).
};

// HairClothHook helper: HasClothData (FUN_1802C3420)
//   Checks the vertex buffer flags for cloth-eligible data. Used to gate
//   beard cloth re-entry: only call the factory on beards that have cloth
//   data in their mesh, to avoid pointless work.
constexpr TargetSignature kHasClothData = {
    /* name              */ "HasClothData",
    /* pattern           */ "48 8B 51 38 48 8B 41 40 48 2B C2 48 C1 F8 04 85 C0 7E 4B 4C 63 C8 33 C9 48 8B 05 ? ? ? ?",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x2C3420LL
    // v1.4.6 RVA = 0x2C45A0 (single-match). Prologue BYTE-IDENTICAL v1.3.15<->v1.4.6
    // (`mov rdx,[rcx+0x38]; mov rax,[rcx+0x40]; sub; sar 4; test` = element-count
    // check on the vertex buffer). Confirmed: cloth_factory calls it on type-0 mesh.
};

// HairClothHook helper: NotifyPhysics (FUN_18034A570)
//   Notifies scene physics that a cloth was added. Original mod computed
//   this as `clothFactory - 0xF6A0` (inter-function relative offset), which
//   is fragile across engine versions. Pattern-scan independently instead.
constexpr TargetSignature kNotifyPhysics = {
    /* name              */ "NotifyPhysics",
    /* pattern           */ "66 FF 81 D8 02 00 00 48 8B 81 70 01 00 00 48 85 C0 74 20 0F 1F 40 00 66 0F 1F 84 00 00 00 00 00",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x34A570LL
    // v1.4.6 RVA = 0x34BA20 (single-match). Prologue BYTE-IDENTICAL v1.3.15<->v1.4.6
    // (`inc word[rcx+0x2D8]; mov rax,[rcx+0x170]; test`). Confirmed: cloth_factory
    // calls it after `mov rcx,[scene+0x170]` (physics). Independent pattern (the
    // upstream's `clothFactory-0xF6A0` inter-function offset is NOT used).
};

// FaceMeshObserveHook target: render_list_build
//   Rebuilds the Face_mesh render list at +0xE0 from sub-meshes at +0x100,
//   +0x108, +0x110, +0x118. We temporarily null specific slots so the rebuild
//   skips them, then restore the slot values for refcount purposes.
constexpr TargetSignature kRenderListBuild = {
    /* name              */ "render_list_build",
    /* pattern           */ "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 48 8B D9 48 81 C1 E0 00 00 00 E8 ? ? ? ?",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x61FE20LL
    // v1.4.6 RVA = 0x625670 (single-match). Prologue BYTE-IDENTICAL v1.3.15<->v1.4.6.
    // Confirmed: `add rcx,0xE0` (render list), reads sub-meshes [rbx+0x100..0x118],
    // rebuilds +0xE0 via AddToList(0xC3E90). Delta-exact with Face_mesh::ctor move
    // (+0x5850), an independent cross-check.
};

// Compile-time helper: detect a "<PATTERN_TBD>" stub at install time so the
// installer can log a clear diagnostic instead of attempting to scan for the
// literal string.
inline bool IsAuthored(const TargetSignature& sig)
{
    if (sig.pattern == nullptr || sig.pattern[0] == '\0') return false;
    // ASCII 'P', 'A', 'T' check is enough to distinguish "<PATTERN_TBD>" from
    // any legitimate hex pattern (hex pattern is digits + whitespace + '?').
    if (sig.pattern[0] == '<') return false;
    return true;
}

}}}  // namespace TAOM::NativeHooks::Signatures
