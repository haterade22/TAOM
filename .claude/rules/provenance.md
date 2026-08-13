---
paths:
  - "Main/**/*.cs"
  - "Dependencies/**/*.cs"
  - "docs/features/**/*.md"
  - "docs/reference/**/*.md"
  - "**/_Module/**/*.xml"
  - "**/THIRD-PARTY-LICENSES.txt"
---

# Provenance: name the source, state its license

When TAOM code or content derives from, interoperates with, or was compared against a third-party
mod, that source gets a row in [`docs/reference/provenance-register.md`](../../docs/reference/provenance-register.md)
naming it, its license, and its derivation type. **A bare unattributed mention is a violation. So is
an unnamed euphemism.** "The donor mod", "the upstream pack", "the reference module", "the original
developer" all name nobody, which means nobody can check what terms applied.

## This reverses an earlier rule. Do not restore it.

`docs/changelog-archive/CHANGELOG-2026-H1.md:2258-2270` records a standing rule that "TAOM
documentation must not NAME other mods", and an 8-agent pass that de-named two creature mods into
"the donor mod" / "the upstream beasts pack". **That rule is superseded.** It existed in exactly one
archived CHANGELOG line, had no checker, covered two of a dozen sources, and left the repo
documenting that something had been taken while making the terms unverifiable. If you find that line
first, it is history, not policy.

De-naming also did not work in practice: `docs/INDEX.md:47` said "the upstream beasts pack" while
linking to a file named `adod-beasts-architecture-and-taom-port.md`. The identifiers always survive.

## What good attribution looks like

Two examples in this repo, both safe, both worth copying:

- **Clean-room, for copyleft sources.** [`docs/scene-scripts/ATTRIBUTION.md`](../../docs/scene-scripts/ATTRIBUTION.md):
  Alliance is GPLv3, so its source was read once to produce a committed spec, the implementation was
  written from the spec, a cross-check pass confirmed no structural collision, and every file carries
  a header naming the mod, its license, and its spec.
- **Design reference, stated as such.** [`docs/features/crash-report.md:7`](../../docs/features/crash-report.md):
  BetterExceptionWindow is AGPL, so TAOM authored equivalents from scratch and used it only for what
  to patch and what to display.

## Rules

| Rule | Detail |
|---|---|
| **Name it** | Use the source's published name in comments, docs, and commit messages. Link the register row. |
| **A shipped notice states terms only when they are established** | `UNKNOWN` is a valid value in the register and never appears in a `THIRD-PARTY-LICENSES.txt`. A shipped notice may still describe a derivation factually (what TAOM's code is, what it was written from) without asserting terms nobody has confirmed, and it must not carry the register's internal status vocabulary or point at repo-internal tracking docs. Publishing "we do not know" is not a notice; it is a worklist. |
| **Never assert ownership you do not have** | A blanket "all other binaries here are original TAOM work" in a notice file is an affirmative claim about every artifact it sweeps up. Enumerate, or scope the sentence to what you can actually stand behind. This is the same defect as an unattributed taking, pointing the other way. |
| **Pick the narrowest true derivation** | The vocabulary is closed: `clean-room`, `behavioural-port`, `verbatim-port`, `data-port`, `redistributed`, `interop-only`, `comparison-only`. **If you read the source while implementing, it is not `clean-room`**, no matter how much you changed. Claiming clean-room falsely is worse than claiming nothing, because it is the one claim a rights holder would actually test. |
| **Redistributing a binary means reproducing its notice** | Anything landing under `*/_Module/bin/**` that is not TAOM-built needs a `redistributed` row and an entry in that module's `THIRD-PARTY-LICENSES.txt`. MinHook shipped for months without one. |
| **Do not carry another party's identity into TAOM's public surface** | Their initials in a type name, their SaveSystem base id, their namespace. These outlive any comment and are visible to every other mod in the process. **One carve-out, and it must be argued in writing:** a short identifier that has become community vocabulary can be kept when the reasoning is recorded, as `docs/scene-scripts/ATTRIBUTION.md` does for Alliance's `CS_` scene-script prefix (short names are not copyrightable, and map authors search for them). A prefix that is just the donor's initials on a type nobody outside TAOM searches for does not qualify. |
| **Module ids are facts, not expression** | Listing another mod's id in `ModulesToLoadAfterThis`, in `coop-modules.txt`, or in a compatibility check is interop, not derivation. It still gets an `interop-only` row so the checker can tell the difference. |
| **Honour a stated no-decompile policy** | BannerlordTogether ships one. TAOM reads its Harmony id from Harmony's public runtime registry and constrains `HarmonyCensus` to carry no IL and no method bodies. Keep that shape for any source with similar terms. |

## When you are about to write the comment

Ask which of these you are doing, and write that word:

- Reproducing their code or constants → `verbatim-port`, and the license question is now blocking.
- Reproducing their behaviour after reading the source → `behavioural-port`.
- Working from a committed spec you wrote once and did not re-read the source against → `clean-room`.
- Coexisting with their ids → `interop-only`.
- Comparing designs, deriving nothing → `comparison-only`, and say what TAOM does differently.

## Enforcement

**Today this rule is enforced by reading it.** There is no checker yet, so the register is only as
current as the last person who updated it. Update it in the same commit as the code it describes.

The planned mechanism, not yet built: `tools/check_provenance.py` parsing the register as its
allowlist and scanning for module ids, mod distribution links, euphemism phrases,
attribution-header coverage, and unregistered shipped binaries, with a
`tools/provenance-baseline.txt` ratchet for the pre-existing backlog. It would run in CI (the
unconditional `validate-xml` job in `.github/workflows/build.yml`, which is the only path every
committer hits) plus a `PreToolUse` hook for fast feedback. **Do not cite it as though it exists**
until it does; that is the same gap this rule was written to close.

**When it does exist, a zero from it will not be self-validating.** It would detect the shapes in
which a source is usually named, not intent. Read the register when you touch a ported feature.

## Adopting something new

Run `/adopt-external` first. Its security and license pass is the front door; the register is where
its answer gets written down. Its output is not complete until the row exists.
