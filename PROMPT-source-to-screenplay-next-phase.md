<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Fresh-session prompt: conclude the source-to-Screenplay Preview foundation

Copy everything below this line into a fresh Pi session started in `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack`.

---

You are concluding—not indefinitely expanding—the Cratis source-to-Screenplay Preview foundation.

Read completely before acting:

1. `AGENTS.md` and `.agents/PROJECT.md` in every repository you change.
2. `.ai/rules/framework.md`, C#/spec/commit/PR rules, and the ship-changes skill.
3. `SOURCE_TO_SCREENPLAY_GOOD_ENOUGH_HANDOVER.md`.
4. The latest comments on Critter roadmap #29, Generation #5/#17, CLI #87/#95, Critter #3/#4/#44, Arc #2600, and Screenplay #128.

## Goal

Reach the two remaining good-enough checks, report the stable Preview baseline, and stop:

1. release/verify CLI with Generation 0.8.0 + Critter Stack 0.19.0;
2. close Generation #5 after both current Cratis consumers are verified.

Do not resume broad Marten/Wolverine completeness or the saga prototype during this finalization.

## Current package baseline

- Screenplay language `4.2.1`.
- Generation `0.8.0`, four lockstep packages.
- Critter Stack `0.19.0`, directly depending on all four Generation 0.8 packages.
- Arc `22.0.0` legacy complete generator.
- CLI `2.15.1` released; local update branch targets Generation 0.8 + Critter 0.19.

## Exact resume sequence

### 1. Finish the active CLI branch

```text
worktree: /tmp/cli-generation080-critter019
branch: chore/take-generation080-critter019
```

Inspect status/diff first. The branch should:

- pin `Cratis.CritterStack.Screenplay` 0.19.0;
- pin Contracts, Generation, DotNet, and DotNet.Vogen 0.8.0;
- keep Arc 22.0.0;
- update provider/package-sentinel/docs expectations;
- expect the new Generation 0.8 `GEN0004` diagnostics for recognized unlowerable Aggregate roles;
- contain no target `Vogen` runtime/source-generator package.

Run:

- all CLI specs;
- warning-free Release build;
- sentinel package metadata specs;
- exact dependency graph assertions;
- isolated tool install if feasible;
- LSP/lens/security/diff review.

Commit logically, open one `patch` PR tied to CLI #87, monitor green CI, merge, and clean the branch/worktree.

### 2. Verify the resulting CLI release

Verify exact commit/tag/release/publish provenance, signed NuGet package, four native assets, Homebrew, and isolated install.

Run installed CLI against:

- a clean package-based Arc fixture;
- the six non-Vogen Critter/Marten fixtures;
- VogenConcepts at Critter 0.19;
- explicit Marten and Arc+Vogen characterization paths.

Generate twice, compare bytes, validate, and record provider/package/assembly/capability provenance. Direct project targeting uses project-relative paths; do not compare its Vogen hash with the canonical runner's repository-relative hash as if that were semantic drift.

### 3. Close the foundation

- Comment full release evidence on CLI #87/#95, Generation #5, and Critter #29.
- Close Generation #5 when the current Critter and CLI consumers both prove Generation 0.8.
- Update `SOURCE_TO_SCREENPLAY_GOOD_ENOUGH_HANDOVER.md` checkboxes and current CLI release.
- Remove completed disposable worktrees and stale `.pi` directories without deleting an active harness directory.
- Report remaining manual unlisting issues #13/#37 and stop.

## Vogen architecture—preserve this

`Cratis.Screenplay.Generation.DotNet.Vogen` is independent from Critter Stack/JasperFx and has no Vogen runtime dependency. It emits neutral facts.

Current execution coupling exists only because CLI invokes complete facades. The next architectural phase, if explicitly approved, is:

```text
compile-time allowlisted atomic adapter roster
  + selection-only profiles
  + independent cross-cutting Vogen/validation adapters
  + central deterministic derivation/resolution
```

Do not add another facade combination. Follow Generation #17-#21, Critter #44, CLI #95, and Arc #2600.

## Paused saga prototype

Do not apply automatically:

```text
repo stash: stash@{0}
stash commit: f8c4b96ba7bd9ae6e5888cd6bd6f703a6a57183b
original branch/worktree: feat/wolverine-saga-evidence / /tmp/Critter-wolverine-sagas
```

It had 647 passing specs before pause but lacks independent final review. In particular, review any MartenDocumentFacts dependency on WolverineSagaFacts against atomic adapter issue #44.

## Non-negotiable invariants

- Adapters emit neutral immutable facts/evidence/diagnostics, never AST/text.
- One resolver/lowerer/printer/compiler owns output.
- Authored source originates semantics; generated source only corroborates.
- Adapter/project order never chooses a conflict.
- Persisted events, messages, cascades, publishes, schedules, responses, side effects, saga state, documents, and projection fan-out remain distinct.
- Concept representation, validation, optionality, and identity roles remain independent.
- Unknown or unsupported inputs fail closed with typed diagnostics.
- No runtime host/database/broker startup.
- No arbitrary workspace plugin loading.
- No more framework breadth before the finalization goal is complete.

## Definition of done

The final answer must name:

- exact Generation, Critter Stack, CLI, Arc, and Screenplay versions;
- specs/build/package/canonical/installed-tool evidence;
- what Vogen independence means today;
- the current facade limitation and atomic-roster next phase;
- what remains intentionally deferred;
- what was not verified.

Then stop. Do not autonomously start the next phase.
