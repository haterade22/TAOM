---
name: taom-research
description: Investigate TaleWorlds behavior, signatures, lifecycle, or engine-related TAOM failures before design or review. Diagnose without implementing unless the user also requests a fix.
---

# Research the installed engine

Read the [policy](../../../.ai/policy.md),
[Codex operating guide](../../../docs/ai-includes/codex-operating-guide.md), and
[TaleWorlds research guide](../../../docs/ai-includes/taleworlds-research-guide.md).
Use [development machines](../../../docs/reference/development-machines.md) to
resolve the actual game and dump locations rather than assuming desktop paths.

State the engine question and the TAOM caller or failure it affects. Read the
relevant [engine process document](../../../docs/reference/engine/) first for the
conceptual path. Then verify the exact type, signature, getter, enum value or
raise site in the installed assembly. Trace base calls and native boundaries too.

Use available file/shell tools, `tools/taom-src.ps1`, `ilspycmd` or an actually
connected ILSpy MCP tool. Discover the tool and inspect its arguments before
using it; configuration text alone does not make a tool available. The source
helper writes a user-local cache and may need permission. Do not bypass a denial.

Distinguish shipping client, module, server and editor assemblies. A missing type
in one dump is not proof that it does not exist. Record assembly path, game
version/build, a fingerprint when needed, type/method and the relevant quotation.
Respect third-party no-decompile restrictions and source-sharing limits.

Return the evidence, its implication for TAOM, and remaining uncertainties. Do
not invent vanilla behavior, decompile the entire game without need, write a fix
during diagnosis, or treat missing external content as a confirmed repo defect.
