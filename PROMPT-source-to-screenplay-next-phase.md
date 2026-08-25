<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Historical prompt: atomic source-adapter phase

> Superseded: this prompt captures the `0.19.0` / Generation `0.8.0` checkpoint and must not be used as current continuation. The saga candidate it references shipped in `v0.21.0`; consult [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md) and the owning issue trackers before starting new work.

The original fresh-session prompt follows unchanged as historical design context.

---

The Cratis source-to-Screenplay Preview foundation is complete. Do not reopen its finalization or resume broad Marten/Wolverine coverage.

Read completely before acting:

1. `AGENTS.md` and `.agents/PROJECT.md` in every repository you change.
2. `.ai/rules/framework.md`, C#/spec/commit/PR rules, and the ship-changes skill.
3. `SOURCE_TO_SCREENPLAY_GOOD_ENOUGH_HANDOVER.md`.
4. The latest comments on Generation #17-#21, Critter Stack #44, CLI #95, Arc #2600, and Screenplay #128.

## Released baseline

- Screenplay language `4.2.1`.
- Generation `0.8.0`, four lockstep packages with typed fail-closed outcomes.
- Critter Stack `0.19.0`.
- Arc adapter `22.1.0`.
- CLI `2.15.2`, commit `ff0ae6fb59a5154f89ce6f3830505ecc87e0c97b`.
- Generation #5 and CLI #87 are closed.

The public CLI package, native assets, Homebrew formula, package closure, and deterministic installed-tool fixtures were verified. Do not repeat those gates unless the baseline changes.

## Product decision required

Before writing code, confirm that the requested work explicitly starts **atomic adapter composition**. If the request is merely to add more Marten/Wolverine/Vogen cases, stop and ask for the product need or canonical loss that justifies them.

## Target architecture

```text
compile-time allowlisted atomic adapter roster
  primary: Marten, Wolverine, Arc-neutral bridge, ...
  integration: exact Wolverine-Marten coupled APIs only
  cross-cutting: Vogen, FluentValidation, StronglyTypedId, ...

selection-only profile
  -> independently probe/admit each adapter
  -> run each adapter once over one immutable authored-source snapshot
  -> validate/freeze neutral contributions
  -> derive/resolve/lower/print/compile once
```

Profiles select adapters. They do not own Vogen, validation, conflict precedence, or output composition.

## Vogen boundary

`Cratis.Screenplay.Generation.DotNet.Vogen` is already technically independent from JasperFx, Marten, Wolverine, Critter Stack, Arc, and the target Vogen runtime/source generator. It emits neutral concept, representation, and named-validation facts.

The remaining coupling is orchestration only: CLI `2.15.2` invokes complete facades, and the Critter compatibility facade composes Vogen. The atomic phase must let the allowlisted Vogen adapter run independently and at most once with Arc, Marten, Wolverine, or another primary adapter.

Do not move Vogen semantics into a framework profile or create facade packages for every library combination.

## Recommended first vertical increment

Deliver the smallest compatibility-preserving seam across Generation #17, Critter Stack #44, and CLI #95:

1. expose stable distinct Marten, Wolverine, Wolverine-Marten integration, and Vogen adapter identities;
2. retain existing complete generators as compatibility wrappers;
3. add a compile-time allowlisted CLI roster and selection-only profile model;
4. execute each admitted adapter once over the selected application scope;
5. feed all contributions through the existing deterministic Generation resolver/lowerer/printer/compiler pipeline;
6. prove reversed adapter order is byte-identical and duplicate IDs/execution fail before analysis;
7. prove the compatibility wrapper remains byte-identical to the released baseline.

Do not combine this first increment with new source semantics.

## Non-negotiable invariants

- Adapters emit immutable neutral facts/evidence/diagnostics, never Screenplay AST or text.
- One resolver/lowerer/printer/compiler owns composition and output.
- Authored source originates semantics; generated source only corroborates.
- Adapter/project enumeration order never chooses a conflict.
- Persisted events, messages, cascades, publishes, schedules, responses, side effects, saga state, documents, and projection fan-out remain distinct.
- Concept representation, validation, optionality, identity, and event-source identity remain independent.
- Unknown, malformed, or unsupported inputs fail closed with typed diagnostics.
- Profiles and package provenance cannot load arbitrary workspace plugins.
- Source identity and display paths remain separate per Generation #21.

## Historical saga prototype

The former paused saga candidate addressed its review findings and shipped in `v0.21.0`. Do not apply the old stash or treat this historical prompt as a source of current saga work.

## Verification and stop condition

Use compatibility fixtures before adding behavior. Run repository quality gates, package consumers, deterministic generation in reversed adapter order, public API compatibility, and CLI selection/admission specs.

Stop the increment when the atomic roster can reproduce the released complete-facade baseline without semantic additions. Report remaining target-language loss and deferred product decisions; do not continue into framework breadth automatically.
