# Codex Adversarial Review — RevoltTuning (2026-04-20)

Target: working tree diff
Verdict: needs-attention

No-ship. The new revolt tuning path silently accepts semantically bad config and caches it for the whole process, so one bad JSON edit can produce wrong gameplay with a misleading success log, and later retunes do not reliably apply on the next campaign/load as the docs claim.

## Findings

### [HIGH] Provider accepts any parseable values as success

**Location:** `Main/Features/RevoltTuning/RevoltTuningConfigProvider.cs:34-39`

`LoadConfig()` treats any successfully deserialized object as valid and immediately logs `Loaded revolt_tuning_config.json`. There is no validation that thresholds are sane or that the two culture effects stay negative. Because this feature is explicitly user-editable JSON, a parseable but wrong file like `{"rebellionStartLoyaltyThreshold":100}` or a sign-flipped `1.0` penalty will be accepted, cached, and applied with no warning. The current tests only cover missing/malformed/partial JSON, so this failure mode is untested. This is an inference from the provider code and test matrix; Codex could not verify the Bannerlord-side comparisons from the installed DLL in this sandbox.

**Recommendation:** Validate deserialized values before logging success. Reject or clamp out-of-range thresholds/effects, log an error, and fall back to defaults. Add tests for empty `{}`, extra fields, wrong-type values, and semantically invalid but parseable values.

### [MEDIUM] Singleton config cache survives across game starts

**Location:** `Main/Features/RevoltTuning/RevoltTuningIoC.cs:7-9`

The provider is registered as `Reuse.Singleton`, `IoC.Configure()` runs once in `OnSubModuleLoad()`, and `IoC.Dispose()` is only called in `OnSubModuleUnloaded()`. `TaomSettlementLoyaltyModel` then captures `GetConfig()` in its constructor. In practice that means once the config is first loaded, editing `revolt_tuning_config.json` and starting another campaign/loading another save in the same Bannerlord process will keep using the stale cached instance. That directly conflicts with the feature doc's promise that JSON edits take effect on the next game load.

**Recommendation:** Either scope the provider to a game session, add an explicit reload path and stop snapshotting the config in the model constructor, or change the docs to require a full application restart instead of a game/load restart.

## Next Steps (from Codex)

- Add semantic validation to `RevoltTuningConfigProvider` and extend the tests beyond syntax-error cases.
- Fix the cache lifetime/reload behavior or narrow the docs so they match the actual lifecycle.
