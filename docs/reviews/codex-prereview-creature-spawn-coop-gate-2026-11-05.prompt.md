Adversarial pre-review of an uncommitted fix in this worktree (branch fix/creature-spawn-coop-gate, base origin/bannerlord-1.5.x ee7fe04f4). Issue #1084. Read-only: do not edit any file. Return the full report as your FINAL MESSAGE; do not write it to a file.

FEATURE: Creature Bandits (#692 spider broods, #694 troll bands). TrollBandSpawnBehavior and CreatureBroodSpawnBehavior spawn bandit parties on DailyTickEvent. On a BannerlordCoop dedicated server, clients ran the same ticks and threw a caught NullReferenceException in WarPartyComponent.OnInitialize. The fix injects ICoopSessionProvider and returns at the top of OnDailyTick unless IsAuthority (read every tick). CreatureBroodCampaignDiagBehavior (log lines only) is classified as deliberately not gated.

READ FIRST: docs/features/coop-interop.md sections "Layer 3 -- host authority", "Gated (host-only)", "Deliberately NOT gated", "Client-side object creation". Main/Features/CoopInterop/ICoopSessionProvider.cs, CoopSessionProvider.cs, CoopSessionPolicy.cs. docs/features/creature-bandits.md.

CHANGED FILES (git diff in this worktree):
Main/Features/CreatureBandits/TrollBandSpawnBehavior.cs
Main/Features/CreatureBandits/CreatureBroodSpawnBehavior.cs
Main/Features/CreatureBandits/CreatureBanditsModule.cs
TAOM.Tests/Features/CreatureBandits/CreatureSpawnCoopGateTests.cs (new)
TAOM.Tests/Features/CoopInterop/CoopGateDocCoverageTests.cs (baseline shrunk by 3)
docs/features/coop-interop.md, docs/features/creature-bandits.md

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. Is IsAuthority the right predicate, versus ShouldDeferToHost or IsSinglePlayerAuthority? Check what each returns on a dedicated server host, a listen host, a client, singleplayer, and singleplayer with the Coop module merely loaded. The server must still spawn.
2. Is ICoopSessionProvider registered in the container at the point CampaignBehaviorDecl factories resolve (game start), including in Custom Battle or any path that builds these behaviors? Could Resolve throw and take down module composition?
3. Other creature-band code that also creates or orders parties on a client: CreatureBandParties (ReturnStraysToPatrol, Spawn), console command taom.spawn_creature_band (CreatureBandCheats), the mission behavior, Patch93/Patch94. Is anything else in this feature world-mutating and ungated on a client? Report but do not require fixes outside scope; say which are real.
4. CreatureBroodSpawnBehavior.OnSessionLaunched stays ungated (diagnostic reset and a log). Confirm it mutates no shared state.
5. Test validity: CreatureSpawnCoopGateTests relies on Clan.All throwing with no Campaign.Current, caught and logged. Is that a sound probe, or could the client test pass for the wrong reason?

REQUIRED SECTIONS: per suspect verdict with evidence; ENGINE (quote Clan.All and WarPartyComponent.OnInitialize from the installed v1.5.6 TaleWorlds DLLs via the ilspy MCP or pwsh tools/taom-src.ps1 path <Type>); FINDINGS (severity CRITICAL/HIGH/MEDIUM/LOW, file:line, failure scenario); OBSERVATIONS.

QUALITY GATES: every claim cites a file and line you read; no invented API; missing evidence is UNVERIFIED.
