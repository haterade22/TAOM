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
// PATTERN PLACEHOLDER NOTE
// ------------------------
// Patterns labelled "<PATTERN_TBD>" below are intentional stubs. The scanner
// will return 0 on these and the hook installer logs a clear "pattern not
// yet authored for this Bannerlord version" message. The game keeps running
// — the hook is just inert. Replace each "<PATTERN_TBD>" with a real
// IDA-style byte pattern when porting to a new engine version.
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
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x617B50LL
};

// HairClothHook target: cloth_factory
//   Rescues orphan cloth components left at Face_mesh+0x1A0 and registers them
//   in the entity list (rendering) + sim list (simulation). Also re-enters the
//   factory for beard cloth at Face_mesh+0x108.
constexpr TargetSignature kClothFactory = {
    /* name              */ "cloth_factory",
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x359C10LL
};

// HairClothHook helper: AddToList (FUN_180_C4040)
//   Inserts a pointer into a vector at factory+0x1E8 (entity list) or
//   factory+0x208 (sim list). Used by the orphan-cloth rescue path.
constexpr TargetSignature kAddToList = {
    /* name              */ "AddToList",
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x0C4040LL
};

// HairClothHook helper: GpuInit (FUN_180292570)
//   Initializes cloth GPU resources. Optional — if not resolved, cloth still
//   registers but GPU buffers wait until next scene tick.
constexpr TargetSignature kGpuInit = {
    /* name              */ "GpuInit",
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x292570LL
};

// HairClothHook helper: HasClothData (FUN_1802C3420)
//   Checks the vertex buffer flags for cloth-eligible data. Used to gate
//   beard cloth re-entry: only call the factory on beards that have cloth
//   data in their mesh, to avoid pointless work.
constexpr TargetSignature kHasClothData = {
    /* name              */ "HasClothData",
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x2C3420LL
};

// HairClothHook helper: NotifyPhysics (FUN_18034A570)
//   Notifies scene physics that a cloth was added. Original mod computed
//   this as `clothFactory - 0xF6A0` (inter-function relative offset), which
//   is fragile across engine versions. Pattern-scan independently instead.
constexpr TargetSignature kNotifyPhysics = {
    /* name              */ "NotifyPhysics",
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x34A570LL
};

// FaceMeshObserveHook target: render_list_build
//   Rebuilds the Face_mesh render list at +0xE0 from sub-meshes at +0x100,
//   +0x108, +0x110, +0x118. We temporarily null specific slots so the rebuild
//   skips them, then restore the slot values for refcount purposes.
constexpr TargetSignature kRenderListBuild = {
    /* name              */ "render_list_build",
    /* pattern           */ "<PATTERN_TBD>",
    /* fallbackPattern   */ nullptr,
    /* byteOffsetFromMatch */ 0,
    /* historicalRva     */ 0x61FE20LL
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
