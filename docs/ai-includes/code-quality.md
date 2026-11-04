# Code Quality: the nullable ratchet

The one part of code quality that a gate enforces and an author must know. `NullableRatchetGateTests` and the `.editorconfig` files that graduate a folder cite the section below.

## How nullable is enforced

`Directory.Build.props` enables nullable for every
project. For `Main/` and `Dependencies/` the root `.editorconfig` sets the seven main nullable
ids (CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8625) to `none`; a folder that is
null-clean carries its own `.editorconfig` setting them to `error`, and the nearer file wins.
List the graduated folders with `git grep -l "CS8602.severity = error" -- '*.editorconfig'`.
Never add these ids back to a csproj `<NoWarn>`, nor the word `nullable`, which the compiler
expands to every nullable warning: the compiler's `/nowarn` beats every `.editorconfig`, which
would silently turn off every graduated folder (`NullableRatchetGateTests` fails if one does). On net472 `string.IsNullOrEmpty` does not narrow
(no `[NotNullWhen]` in the reference assemblies), so write `x is null || x.Length == 0` where the
compiler must see the check. This supersedes the "deliberately suppressed project-wide" note in
`docs/reviews/rca-banner-bearers-2026-07-16.md` for graduated folders.

To graduate a folder (one folder per change):

1. Copy `Main/Features/Siege/.editorconfig` into the folder with the seven ids at `warning`, and
   build with `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental`
   (without `-p:ModuleId=` the build deploys into the game install); every CS86xx warning must sit inside the folder.
2. Fix each one: `?` on a declaration that really holds null; an `is null` check where the engine
   or a config file can hand back null (with a test when it changes control flow); `= ""` or a
   collection initializer where every constructor path sets the value. Never `!` on a value read
   from a TaleWorlds type (the engine assemblies carry no nullable annotations, so `!` asserts
   something nobody checked), never `#pragma warning disable` or `#nullable disable`, never
   `required` (C# 10 in `Main`). `= null!` only on a field set outside the constructor before
   first use, with a comment naming who sets it.
3. Flip the file to `error`, rebuild with the same command (`0 Error(s)`), run the suite. Never leave a folder at
   `warning`: the shipped build has two warnings in total, and a standing warning tier becomes noise.

Hotfix escape: if a graduated folder blocks an urgent build, lower the severity in that folder's own
`.editorconfig` (one reviewable line), never through `<NoWarn>`.

---

## Related Guides

- [architecture.md](./architecture.md) - Overview, client seam and folders
- [patterns.md](./patterns.md) - Thread-local and cache patterns

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-teams.md](./agent-teams.md)
- [docs/ai-includes/architecture.md](./architecture.md)
- [docs/ai-includes/testing-guide.md](./testing-guide.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
