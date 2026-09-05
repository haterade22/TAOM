# Adversarial review — TAOM host-authority gating for BannerlordCoop

Independent adversarial reviewer. Assume the gating is wrong and prove it. Repo root is the working
directory; all changes are UNCOMMITTED (`git diff`, `git ls-files --others --exclude-standard`).
Engine is **Bannerlord v1.4.7** — verify every TaleWorlds signature against the installed DLLs at
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`, never from
memory. Decompiled cache: `C:/Users/mikew/.taom-src/v1.4.7/`.

## Context

TAOM is a Bannerlord total conversion. BannerlordCoop (launcher id `Coop`) is host-authoritative:
one peer simulates the campaign, others follow. **Coop does not stop a client ticking the campaign** —
its `PartyTickPatch` blocks per-entity tickers, but the *global* `DailyTickEvent` / `HourlyTickEvent`
fire on both peers. So TAOM must gate its own campaign behaviours.

The gate is `ICoopSessionProvider.IsAuthority`, defined by `CoopSessionPolicy` as
`!sessionActive || isServer` — it **fails OPEN to singleplayer** by design, because a false negative
would silently disable TAOM features for a solo player.

Read `docs/features/coop-interop.md` (the contract) and
`docs/research/bannerlordcoop-internals.md` (how Coop works) before starting.

## The bug class you are hunting

A gate on a campaign behaviour is wrong in **two** directions, and both have already occurred here:

- **Too broad** — it gates state that is legitimately *per-player*, so a client silently loses
  something it earned. Already found once: `SiegeDefenseBehavior` gated its whole hourly tick,
  including the reward path keyed on `Hero.MainHero`, so a co-op client who defended a siege
  received nothing. Now split into `OnHourlyTickShared` (authority) + `OnHourlyTickLocalPlayer`
  (every peer).
- **Too narrow / missing** — shared campaign state a client can still mutate and thereby diverge
  the campaign, or create an `MBObjectBase` on a client (which throws — see below).

Your job is to find the remaining instances of both.

## Gated behaviours to audit — one verdict each

| Behaviour | Event(s) |
|---|---|
| `CultureConversionBehavior` | Daily + `OnGameLoaded` |
| `RaceAgeBehavior` | Daily |
| `WarOfTheRingBehavior` | Daily |
| `WarOfTheRingMomentumBehavior` | Daily |
| `MessengerCampaignBehavior` | Hourly |
| `SiegeDefenseBehavior` | Hourly (recently split — attack the split) |
| `CastleRecruitmentBehavior` | `OnGameLoaded` / `OnNewGameCreated` |

Deliberately NOT gated: `CareerQuestCampaignBehavior` (daily) — claimed safe because it is keyed
entirely on `Hero.MainHero`, legitimately different per peer. **Verify that claim.** Its only object
creation is `new CareerQuest` on player acceptance.

For **each** behaviour answer, with `file:line` and pasted code:

1. Exactly which state does the handler read and write? Classify each as SHARED (replicated or
   save-backed campaign state) or PER-PLAYER (`Hero.MainHero`, `MobileParty.MainParty`, local UI).
2. Is the gate placed so that SHARED writes are authority-only AND PER-PLAYER effects still reach
   every peer? If it gates a per-player effect, that is a **too-broad** finding — say precisely what
   the client loses.
3. Does any ungated path from the same behaviour still write shared state? Check *all* its event
   registrations, not just the gated one — `RegisterEvents` is the enumeration point.
4. Does any ungated path construct an `MBObjectBase` subclass (`Hero`, `MobileParty`, `Settlement`,
   quests, `CharacterObject`)? On a client this throws: Coop's `MBObjectBasePatches` prefixes the
   `StringId` **setter** and returns false, leaving `StringId` null, so
   `MBObjectManager.cs:215` does `TryGetValue(null, …)` → `ArgumentNullException`. Verify that chain
   against the installed DLLs and Coop's own assemblies before relying on it.

## Also check

5. **`CoopSessionProvider`** — reflection binding into Coop. It must never throw into a caller, must
   fail open, and must not cache a value that legitimately changes when a session starts or stops.
   Two documented traps to confirm are actually handled: `ContainerProvider.Alive` is permanently
   `false` (inline static initialiser evaluated while the backing field is null), and
   `ModInformation.IsServer` defaults false and is *sticky* — so `IsClient` read alone reports
   "client" in plain singleplayer whenever the Coop module is merely enabled.
6. **`CoopSessionPolicy`** — is `!sessionActive || isServer` correct for every combination, including
   host-who-has-hosted-before and a session that ended?
7. **Assembly redirects.** `Dependencies/SubModule.cs`'s `AssemblyResolve` matches on simple name and
   discards the requested version — safe only while TAOM's copy is newest. Five names were removed
   from `RedirectedSimpleNames` because Coop ships higher versions (`Serilog`,
   `System.Runtime.CompilerServices.Unsafe`, `System.Memory`, `System.Buffers`,
   `System.Numerics.Vectors`). Verify the removals are complete and that nothing TAOM needs still
   depends on the older versions at runtime.

## Rules

- Verify before asserting. Cite `file:line`, paste the code you are judging.
- CONFIRMED (you read it and it is wrong) vs SUSPECTED. Severity P1/P2/P3 with the player-visible
  symptom.
- **Solo play must be byte-identical to before.** Any solo regression is automatically P1 — check it
  explicitly; it is the project's hard constraint.
- No taste refactors. Defects only, with the minimum fix.
- Say so plainly if an area is clean rather than padding.

Output: findings by severity, then a one-paragraph verdict on whether the authority layer is
internally consistent, given that no live two-peer session has ever been run.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
