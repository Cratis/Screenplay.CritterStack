<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Source-to-Screenplay good-enough handover

## Conclusion

The source-to-Screenplay **Preview foundation is good enough and finalization is complete**. Do not continue filling every Marten/Wolverine edge case as an open-ended program.

The delivered foundation now proves the important architecture:

```text
trusted host source/workspace
  -> independently identified neutral facts/evidence/diagnostics
  -> deterministic conflict-visible resolution
  -> one Generation lowerer
  -> one Screenplay printer/compiler/round-trip check
  -> CLI output plus separate provenance/diagnostics
```

Future work should prioritize **atomic adapter composition**, reusable evidence mechanics, and target-language capabilities. Framework-specific breadth continues only when a product need or canonical loss justifies it.

## Current released baseline

| Component | Release | Commit | State |
| --- | --- | --- | --- |
| Screenplay language | `4.2.1` | product release | Compiler/AST/printer target |
| Screenplay.Generation | `v0.8.0` | `4ec7ef0031e0f1c74d1033e0a78234f40bbd65ff` | Typed fail-closed outcomes; four lockstep packages |
| Critter Stack adapter | `v0.19.0` | `3585e9abba1d78bb1eb093e068d5beef6e609a1a` | Marten/Wolverine/Vogen composition through bounded DCB |
| Arc adapter | `22.1.0` | published package | Mature legacy complete generator |
| CLI | `v2.15.2` | `ff0ae6fb59a5154f89ce6f3830505ecc87e0c97b` | Generation 0.8 + Critter 0.19 public distribution verified |

Verified public package hashes and detailed evidence live on:

- `Cratis/Screenplay.Generation#5` and `#17`;
- `Cratis/Screenplay.CritterStack#3`, `#4`, and roadmap `#29`;
- `Cratis/cli#87` and `#95`.

## What is complete

### Neutral Generation

- Public `AdapterContribution` facts, evidence, source ranges, diagnostics, subjects, and fact identities.
- Deterministic duplicate collapse, conflict variants, placement resolution, lowering, canonical printing, compiler verification, and round-trip stability.
- Concepts with independent representation, attributes, named validation, exact subject references, and use-site optionality.
- Typed diagnostic outcomes: `Unknown`, `Conflict`, and `Unsupported`.
- `Unknown = -1` public fact discriminators; malformed/future values fail closed before resolution.
- Package compatibility baseline `0.7.1`, current `0.8.0` packages, legacy `0.1`/`0.5` binary smoke, and current-source consumer.

### Vogen

Vogen support is **not semantically owned by Critter Stack**:

- package: `Cratis.Screenplay.Generation.DotNet.Vogen`;
- depends only on Generation.DotNet, not Marten, Wolverine, JasperFx, Arc, or the Vogen runtime/source-generator package;
- emits neutral Concept, representation, and named validation facts;
- generated Vogen members corroborate only; authored partial declarations originate semantics;
- never infers identity from `Guid`, `Id`, generated members, normalization, or named instances.

The analyzed application owns its optional `Vogen` package. The installed CLI bundles the Cratis Vogen **source adapter**, not the target runtime.

The remaining coupling is orchestration: the current Critter compatibility facade composes Vogen by default, and CLI selects one complete provider. This is tracked and must be evolved before adding another adapter family.

### Critter Stack behavior

Canonical and synthetic evidence covers:

- Marten snapshots, projections, compiled queries, documents, identities, EventProjection operations, multi-stream identity/fan-out, projection/daemon/subscription metadata;
- Marten event aliases/upcasts (`MARTEN0011/0012`) and logical tenancy (`MARTEN0013`) as diagnostic-only evidence;
- Wolverine handler discovery, return classification, bus consequences, automations, validation/authorization evidence, current/legacy event streams, receiver-targeted appends, and bounded DCB (`WOLVERINE0012-0015`);
- strict separation among persisted events, messages, cascades, publishes, schedules, HTTP responses, side effects, saga state, document operations, and projection fan-out;
- seven build-first canonical fixtures with deterministic source.

`v0.19.0` Vogen canonical hash:

```text
688ab242f2f40c2a5334f61194af644bdb24b6d189083fc1a0c6878cf10cc745
```

Six non-Vogen outputs remained byte-identical through the DCB release.

### CLI

- Explicit project/workspace/provider/output workflow.
- Explicit multi-target selection (`--framework`, `CLI0015`, `CLI0016`).
- Conservative host/provider ambiguity handling.
- Package, TFM, assembly, and API-capability provenance.
- Independent support tier, recognition, semantic conformance, and lowering fidelity.
- Signed NuGet tool, four native assets, Homebrew, deterministic machine output, and installed-tool fixture verification.
- Forty-one characterization facts freeze current provider/facade limitations before atomic migration.

## Vogen and future cross-cutting adapters

The target architecture is:

```text
CLI built-in allowlisted adapter roster
  primary: Marten, Wolverine, Arc-neutral bridge, EventStoreDB, ...
  integration: Wolverine-Marten, ...
  cross-cutting: Vogen, StronglyTypedId, FluentValidation, ...

selection-only profile
  -> independently probe/admit each adapter
  -> run each adapter once over one immutable authored-source snapshot
  -> validate/freeze contributions
  -> central neutral derivation
  -> resolve/lower/print/compile once
```

Profiles never own Vogen/validation semantics and never determine conflict precedence.

Tracked architecture work:

- Generation `#17`: atomic adapters and profiles;
- Generation `#18`: authored-source and bounded Roslyn helpers;
- Generation `#19`: granular type-use facts and central derivation lineage;
- Generation `#20`: library-neutral validation contracts;
- Generation `#21`: cross-host source-root/path policy;
- Critter Stack `#44`: atomic Marten/Wolverine/integration identities;
- CLI `#95`: selection-only profile roster;
- Arc `#2600`: neutral contribution bridge with legacy parity;
- Screenplay `#128`: measured target-language gaps.

## Active finalization work

### CLI finalization complete

CLI `v2.15.2` shipped through PR `Cratis/cli#99` and was verified from public distributions:

- 735 specs passed and Release built with zero warnings and zero errors;
- public NuGet tool installed and executed as `2.15.2`;
- NuGet SHA-256 `1d830f4ee81190dcccd8adcd8ba89338a0db9718455c5aee491ed9705d0fa76f`, with a valid NuGet.org repository signature;
- embedded closure: Arc `22.1.0`, Critter Stack `0.19.0`, and all four Generation packages `0.8.0`, with no target `Vogen` runtime/source-generator assembly;
- four native assets and Homebrew published; macOS ARM64 SHA-256 `a47feb7a485cd1245feac1f434fdadd5e53d63de307a01fd584897864e99d481` matched the formula and executed as `2.15.2`;
- seven Critter/Marten/Vogen fixtures generated twice byte-identically and every output validated with the installed public tool;
- installed-tool VogenConcepts hash: `b8aa7f9339408a29f3d6b5c763b3033d413dc2483ade5b5140689e84125b57eb`.

Generation `#5` and CLI `#87` are closed. Atomic adapter composition remains separately tracked in Generation `#17`, Critter Stack `#44`, and CLI `#95`.

### Saga prototype — deliberately paused

A substantial saga prototype exists but is **not part of the good-enough baseline**.

```text
worktree: /tmp/Critter-wolverine-sagas
branch: feat/wolverine-saga-evidence
stash: stash@{0}
commit: f8c4b96ba7bd9ae6e5888cd6bd6f703a6a57183b
message: prototype Wolverine saga evidence: 647 specs pass; requires independent review before ship
```

Do not apply and ship it automatically. An independent review confirmed it must remain paused: saga state needs exclusion from HTTP-query and nested payload inference, lifecycle role admission needs stricter static/instance and creation-return checks, handler display names need signature stability, and Marten-to-Wolverine identity internals need a neutral boundary aligned with `#44`. The findings are recorded on Critter Stack `#4`.

## Definition of good enough

- [x] Generation 0.8 released, signed, assets attached, package compatibility and consumers verified.
- [x] Critter Stack 0.19 released with Generation 0.8, deterministic canonical evidence, signed assets, and clean consumer.
- [x] CLI 2.15.1 distribution verified with project-relative source-root behavior documented.
- [x] CLI updated to Generation 0.8 + Critter 0.19, released, and installed-tool verified.
- [x] Generation issue #5 closed after both current Cratis consumers were verified on 0.8.
- [x] Atomic adapter migration and reusable learnings recorded in owning repositories.
- [x] Historical package unlisting remains explicitly external/manual (`Generation#13`, `Critter#37`).
- [x] No requirement to complete every remaining Marten/Wolverine item before ending the Preview-foundation program.

The stop condition is met. The Preview-foundation program ends here. The next phase begins only with an explicit product decision, preferably atomic composition rather than more framework breadth.

## Verification caveats

- macOS `/tmp` resolves to `/private/tmp`; this can break repository `.editorconfig`, SourceLink local-origin checks, and Vogen analyzer dependency loading. Use a non-symlinked home scratch path or exact Linux CI for Release verification.
- Restore separately for Debug and Release multi-TFM builds.
- Temporary `NUGET_PACKAGES` caches can leave `project.assets.json` pointing to deleted package folders. Re-restore external fixtures with the default cache before canonical generation.
- Canonical runner uses repository-relative source paths; direct CLI project targeting uses project-relative paths. Semantics match but hashes differ. Generation `#21` owns the path contract.
- OIDC publish credentials can publish but cannot unlist/delete.

## Do not do

- Do not add another combined stack facade for every library combination.
- Do not move Vogen semantics into Critter, Marten, Wolverine, Arc, or CLI.
- Do not let adapters emit Screenplay AST/text.
- Do not concatenate `.play` documents.
- Do not execute workspace-discovered plugins.
- Do not add syntax without measured neutral facts and loss evidence.
- Do not keep expanding #3/#4 merely because more upstream APIs exist.
