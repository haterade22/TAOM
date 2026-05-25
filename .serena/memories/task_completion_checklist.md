# Task Completion Checklist

After every change session, before finishing:

1. **Tests pass**: Run `dotnet test TAOM.Tests` (or `./build.ps1 -RunTests`)
2. **Build succeeds**: Run `./build.ps1`
3. **Update CHANGELOG.md**: Summarize all changes (grouped by date, then category)
4. **Update CLAUDE.md**: If new files, paths, patterns, or rules were added
5. **Update ADRs**: If architectural decisions were made (`docs/adrs/`)
6. **Update migration tracking**: If migration tasks completed (`docs/migration/TRACKING.md`)
7. **Stub version sync**: If any `<PackageReference>` for `Lib.Harmony`, `Bannerlord.UIExtenderEx`, or `Bannerlord.MCM` in `Dependencies/TAOM.Dependencies.csproj` was bumped, bump the matching stub `<Version>` in `Stubs/Bannerlord.{Harmony,UIExtenderEx,MBOptionScreen}/_Module/SubModule.xml` (note: MCM's stub is `Bannerlord.MBOptionScreen`). Third-party mods may pin via `<DependedModuleMetadata version="..."/>` — silent drift breaks BLSE-enforced version checks. See `docs/migration/dr3-maintenance.md` "Stub modules" section.