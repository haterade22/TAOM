Adversarial pre-review: TAOM.CoopCompat, a new separate Bannerlord module (#967 sub-batch C3), uncommitted in this worktree (branch feat/coop-compat on 410387c41). It holds the only compile-time references to BannerlordCoop. It does two things: a build and settings handshake when a client joins a co-op campaign (warn only, never disconnect), and a relay of TAOM's War of the Ring momentum state from the server to the clients. You are a reviewer: report findings, do not edit files.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/features/coop-compat.md
docs/adrs/014-separate-coop-compat-module.md
docs/features/coop-interop.md sections "Layer 2", "Load order", "What is NOT done"
docs/adrs/013-server-safe-core-client-ui-submodule.md (why a type naming a missing assembly kills the process at the engine's GetTypes scan)

Coop source to read for the bound APIs (read-only, outside this repo): E:\repos\coop-4593\source (branch task/4593-merge-development, Bannerlord v1.5.7 port). The module compiles against the installed E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Coop\bin\Win64_Shipping_Client DLLs. TaleWorlds signatures: pwsh tools/taom-src.ps1 path <Type>.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence from both codebases):
S1. Weak subscriptions. Coop's MessageBroker keeps WeakReference(delegate.Target). CoopTransport subscribes instance methods and CoopCompatSubModule holds the transport in a field. Is any subscription still collectable (a lambda, a closure, a method group on a short-lived object), so it silently stops firing after a GC?
S2. Role per call. CoopTransport reads the role per message through ContainerProvider.TryGetContainer and ModInformation.IsServer. For a client-hosted game (the host is a player and IsServer is true) and for a dedicated server, is the role right at the moment ClientCampaignReady and each message handler fire? Can the host peer send a hello to itself, or answer its own hello?
S3. Message flow. Is ClientCampaignReady published on the joining client's broker, after the campaign is loaded on that client? Does INetwork send to one peer and broadcast as the code assumes, with the messages registered for protobuf serialization the way Coop's own ICommand and IEvent messages are? Are the wire contract names stable across a net472 client and a .NET 6 or 8 server?
S4. Momentum snapshot size and reliability. A full snapshot measured 610 entries, 59,050 bytes (MomentumRelaySizeTests). Does Coop's transport (LiteNetLib under INetwork) deliver a message that large reliably in one send, or does it need fragmentation or a delivery method the code does not choose?
S5. Client writes. On a client, ApplyWarState replaces the war state and raises MomentumChanged. MomentumRelayService then sets its dirty flag. Can a client ever broadcast or answer as if it were the server? Can a stale or reordered snapshot overwrite a newer one (sequence handling across a server restart or a rejoin)?
S6. Type scan and assembly binding. The engine runs GetTypes() on TAOM.CoopCompat.dll (the only DLL SubModule.xml names) before any OnSubModuleLoad, when no Coop assembly is loaded. Every type that needs Coop to load (the transport, the three contracts that implement Common's ICommand and IEvent) is in TAOM.CoopCompat.Coop.dll, beside it in the same bin folder and not named in SubModule.xml (CoopCompat/Coop/TAOM.CoopCompat.Coop.csproj). CoopCompatSubModule starts the transport at the first campaign OnGameStart. TypeScanGateTests walks base types, interfaces, generic arguments and value-typed fields. Attack it: can any type of TAOM.CoopCompat.dll still need Coop at scan time (a nested type, a closure class, a value-typed field, an attribute argument)? Does .NET Framework 4.7.2 (client, Assembly.LoadFrom) and .NET 6 or 8 (dedicated server) resolve TAOM.CoopCompat.Coop.dll from the module bin folder when OnGameStart first touches it? Is the first campaign OnGameStart early enough on a joining client to see ClientCampaignReady, and on a dedicated server to answer the first hello?
S7. MomentumStateStore.CaptureWarState and ApplyWarState (Main/Features/WarOfTheRingMomentum). Is the "player.events" exclusion complete, so per-player state never crosses peers, and does ApplyWarState keep the client's own events intact? Does RestoreWarState, now shared with Deserialize, change save load behavior?

FILES:
Module: CoopCompat/CoopCompatSubModule.cs, CoopCompat/_Module/SubModule.xml, CoopCompat/TAOM.CoopCompat.csproj, CoopCompat/Coop/TAOM.CoopCompat.Coop.csproj, CoopCompat/Coop/Transport/CoopTransport.cs, CoopCompat/Coop/Transport/ICoopTransport.cs, CoopCompat/Coop/Messages/CoopCompatHello.cs, CoopCompat/Coop/Messages/CoopCompatHelloReply.cs, CoopCompat/Coop/Messages/CoopCompatMomentumSnapshot.cs, CoopCompat/Handshake/SettingsHandshakeService.cs, CoopCompat/Handshake/BuildIdentity.cs, CoopCompat/Handshake/ISettingsNotice.cs, CoopCompat/Handshake/InformationManagerSettingsNotice.cs, CoopCompat/Momentum/MomentumRelayService.cs, CoopCompat/Momentum/MomentumRelayCampaignBehavior.cs
TAOM core: Main/Features/WarOfTheRingMomentum/IMomentumStateStore.cs, Main/Features/WarOfTheRingMomentum/MomentumStateStore.cs, Main/Features/CoopInterop/TaomSettingsPages.cs, Main/SubModule.cs (the SettingsFingerprintLog.WriteAcross hunk only), Main/_Module/ModuleData/taom_module_strings.xml (two taom_coopcompat_* rows)
Tests: TAOM.CoopCompat.Tests/**, TAOM.Tests/Features/WarOfTheRingMomentum/MomentumStateStoreTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsModuleTests.cs, TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs, TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs
Build and tools: TAOM.sln, Directory.Build.props, tools/package_release.py (CoopCompat packaging and the Coop runtime refusal), tools/tests/test_package_release.py, tools/harvest_literal_loc_keys.py, tools/ingame_checks/967.toml (four new checks), tools/tests/test_ingame_verdict.py

REQUIRED SECTIONS:
1. COOP CODE: paste the Coop source you relied on for S1 to S6 (MessageBroker subscribe path, WeakDelegate, ClientCampaignReady publisher, INetwork send methods, the protobuf message registration) as code blocks with paths.
2. SUSPECT VERDICTS: S1 to S7, each CONFIRMED, DISPUTED or UNVERIFIED with evidence.
3. LIFECYCLE: trace one join end to end (client campaign ready, hello, server compare, reply, snapshot, apply), then a second client joining, a client leaving and rejoining, and a server save and reload. Name any state that survives where it should not.
4. PACKAGING: can a TAOM package ever carry a Coop or dedicated server binary past tools/package_release.py? Can the deploy target in the csproj copy one into the game install?
5. FINDINGS: severity CRITICAL, HIGH, MEDIUM, LOW, with file:line and a concrete failure scenario for each.

QUALITY GATES: every claim about Coop or TaleWorlds behavior quotes the line that produces it. A claim you could not check is UNVERIFIED, never assumed. Do not flag code that matches Coop's own usage of the same API as a bug without saying so.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to a file; do not write any file yourself.
