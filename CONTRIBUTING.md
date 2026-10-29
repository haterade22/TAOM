# Contributing to TAOM

Bug fixes, tests and gameplay reports are welcome. This page covers how to send a change and what
happens to it after you do.

## Where to send it

- **Branch:** open pull requests against `bannerlord-1.5.x`, which targets Bannerlord **v1.5.6**
  (the Steam beta branch). Bug fixes for the legacy Bannerlord 1.4.8 line can go to
  `bannerlord-1.4.5`; build and test those against 1.4.8 and say so in the PR.
- **One fix per pull request.** Small PRs get reviewed quickly; a bundle waits for its slowest part.
- **Bugs without a fix:** open an issue with the game version, the TAOM version, the steps to
  reproduce and both logs (`rgl_log_*.txt` and the crash report folder, if there is one).

## Setting up

The [README](README.md#quick-start-developers) has the full setup. In short: Bannerlord v1.5.6,
the .NET SDK (or Visual Studio 2022), and `.\setup-dev-env.ps1` to point the build at your install.

Build and run the tests without deploying into the game:

```powershell
dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
```

If you cannot build (no SDK, or a different game version), say so in the PR. We build and test it
on our side, but tell us exactly what you did and did not run.

## What a good PR has

1. **A failing test first.** Tests live in `TAOM.Tests` (MSTest and NSubstitute). Show the test
   failing without your fix and passing with it.
2. **The reproduction.** Game version, TAOM version, steps, and what you saw before and after.
3. **Engine evidence.** If the fix depends on how a TaleWorlds API behaves, name the type and method
   and the game version you checked it against.
4. **Project rules.** [AGENTS.md](AGENTS.md) has them; the ones that come up most:
   - TaleWorlds sealed types go behind an adapter; services never take them directly.
   - Harmony patches and entry points stay thin and call a service.
   - No `#region`, no `[Obsolete]`, no `#if` outside `IoC.cs`.
   - Commit subject: `fix(scope): vX.Y.Z - what changed`, where `vX.Y.Z` is the `<Version>` in
     `Main/_Module/SubModule.xml`. At most 72 characters.
   - New player-facing text uses `{=KEY}Default text` so it can be translated.

## What we will not merge

- Compiled binaries, archives or packaged assets (`.dll`, `.exe`, `.zip`, `.tpac` and similar).
  Send the source, or describe the asset change in an issue.
- Changes to the build, the AI tooling or the repo automation (`*.csproj`, `*.props`, `*.targets`,
  `build.ps1`, `.claude/`, `.ai/`, `.mcp.json`, `AGENTS.md`, `CLAUDE.md`, `.github/`) unless an
  issue agreed on them first.
- Changes to the unversioned modules (`TAOM_Map`, `LOTRLOME_Armory`): they are not in this repo.

## What happens after you open a PR

Every outside PR goes through the same three checks, in this order:

1. **Security review.** A maintainer reads the full diff on GitHub before anything is checked out or
   built. A PR that adds binaries, obfuscated or encoded content, network or process calls with no
   clear reason, or edits to the paths listed above stops here.
2. **Build and tests.** The full solution build and the whole test suite.
3. **Code review.** A review of correctness, engine compatibility and the project rules.

We may push small fixes to your branch or ask for changes. A maintainer merges; passing checks are
not approval on their own.

## Licensing

Code is MIT ([LICENSE](LICENSE)); game data, art and lore text are CC BY-NC-SA 4.0
([LICENSE-CONTENT.md](LICENSE-CONTENT.md)). By opening a pull request you agree that your
contribution is licensed under the same terms as the files it changes, and that you have the right
to submit it.
