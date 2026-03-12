# Task Completion Checklist

After every change session, before finishing:

1. **Tests pass**: Run `dotnet test TAOM.Tests` (or `./build.ps1 -RunTests`)
2. **Build succeeds**: Run `./build.ps1`
3. **Update CHANGELOG.md**: Summarize all changes (grouped by date, then category)
4. **Update CLAUDE.md**: If new files, paths, patterns, or rules were added
5. **Update ADRs**: If architectural decisions were made (`docs/adrs/`)
6. **Update migration tracking**: If migration tasks completed (`docs/migration/TRACKING.md`)