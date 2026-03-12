# Suggested Commands

## Build & Test
- `./build.ps1` — Build the mod
- `./build.ps1 -RunTests` — Build + run tests
- `dotnet test TAOM.Tests` — Run tests only

## TaleWorlds Decompilation
- `ilspycmd "%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.ClassName"` — Decompile a specific class

## Git
- `git status` / `git log` / `git diff` — Standard git commands

## System (Windows)
- `ls` / `dir` — List directory
- `cat` / `Get-Content` — Read file
- `grep` / `Select-String` — Search content
- `find` / `Get-ChildItem -Recurse` — Find files

## Key DLLs for Decompilation
- `TaleWorlds.CampaignSystem.dll` — Campaign logic
- `TaleWorlds.CampaignSystem.ViewModelCollection.dll` — UI ViewModels
- `TaleWorlds.Core.dll` — Core types
- `TaleWorlds.MountAndBlade.dll` — Battle/mission logic