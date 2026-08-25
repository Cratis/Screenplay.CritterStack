<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Wolverine saga evidence implementation handover

## Status

This document is historical release evidence for the bounded saga work that subsequently merged and shipped in `v0.21.0`. The candidate/branch language below describes the pre-merge checkpoint and is not a current continuation instruction.

It preserves the former local stash:

```text
stash commit: f8c4b96ba7bd9ae6e5888cd6bd6f703a6a57183b
message: prototype Wolverine saga evidence: 647 specs pass; requires independent review before ship
```

The former stash reported 647 passing specs. The resumed branch adds focused lifecycle, correlation, method-identity, and graph-compatibility coverage; final verification for this working diff is recorded below.

Tracking:

- Critter Stack #50 — bounded authored saga completion;
- Critter Stack #44 — atomic Marten, Wolverine, and integration boundaries;
- Generation #17 and CLI #95 — atomic adapter execution and selection-only profiles.

## What the bounded implementation establishes

- current and legacy Wolverine saga discovery;
- saga, handler, message, and Handles/Cascades evidence;
- authored correlation-member evidence and runtime-correlation diagnostics;
- report-only `WOLVERINE0016` realization/provenance for Wolverine-managed lifecycle and exact `MarkCompleted()` occurrences;
- saga-state return classification without treating state as a persisted event, response, or cascade;
- canonical VogenConcepts evidence for current package APIs.

Primary implementation and fixtures:

- `Source/DotNET/CritterStack/Wolverine/WolverineSagaFacts.cs`;
- `Source/DotNET/CritterStack/Wolverine/WolverineSagaTypes.cs`;
- `Source/DotNET/CritterStack/Wolverine/WolverineSymbolAuthority.cs`;
- `Source/DotNET/CritterStack/Wolverine/WolverineFacts.cs`;
- `Source/DotNET/CritterStack/Marten/MartenDocumentFacts.cs`;
- `Source/DotNET/CritterStack.Specs/given/a_wolverine_saga_application.cs`;
- `Source/DotNET/CritterStack.Specs/given/a_legacy_wolverine_saga_application.cs`;
- `Source/DotNET/CritterStack.Specs/for_CritterStackScreenplayGenerator/when_generating_wolverine_saga_evidence.cs`;
- `Source/DotNET/CritterStack.Specs/for_CritterStackScreenplayGenerator/when_generating_legacy_wolverine_saga_evidence.cs`;
- `Integration/Canonical/VogenConcepts/Sagas.cs`.

## Independent review findings

The resumed implementation addresses the original blockers and the final correctness review:

1. **Final admission boundaries:** one exact saga-state predicate prevents Saga derivatives from originating Query, ReadModel, Returns, direct-bus Message, explicit event-stream Event, persistence-return, `OutgoingMessages`, or DCB message/event evidence. Mixed payload specs prove ordinary sibling messages and events remain.
2. **Lifecycle admission:** roles are grouped by message to match Wolverine's actual `SagaChain` admission. Instance roles, static `Start`/`StartAsync`, and static `NotFound` establish a chain; static `Starts`/`StartsAsync` and `NotFoundAsync` require another exact chain-establishing method for the same message. Direct primitive returns are rejected as Wolverine discovery rejects them. Existing-only chains remain valid without construction. Instance/fallback creation requires an accessible public parameterless constructor unless an exact returned saga supplies creation for a static start-only chain, and a `NotFound`-only chain also requires one because Wolverine reaches `CreateNewSagaFrame` when there are no existing calls. Rejected authored roles report `WOLVERINE0018`.
3. **Correlation precedence:** an explicit `[SagaIdentityFrom]` name replaces the `<SagaTypeName>Id` tier. Every parameter in every admitted same-message chain is inspected as Wolverine does; conflicting names fail closed with authored, located, deterministic diagnostics. A missing explicit member continues with the suffix-stripped name, `SagaId`, and case-insensitive `Id`. Inherited public authored fields/properties participate, and ambiguous matches fail closed.
4. **Neutral method identity:** `DotNetMethodIdentity` replaces the `MartenDocumentFacts -> WolverineSagaFacts` dependency and uses collision-safe .NET documentation IDs while retaining readable names for same-named namespace types, multiple parameters, modifiers, generic arity, arrays, nested types, and generics.
5. **Graph compatibility:** non-saga Marten document-operation overloads remain separate, generated overloads do not interfere, and Marten/Wolverine facts converge on the same identity for one saga handler.
6. **Authored message shape and ignore policy:** every Message artifact originating from saga analysis uses authored-only properties, including incoming roles, returned cascades, direct bus messages, and `OutgoingMessages`; generated partial properties are absent while authored siblings and relationships remain. A source-generator-only `WolverineIgnore` attribute cannot suppress an authored saga; only authored attribute occurrences or trusted metadata are authoritative.
7. **Nested and direct exclusion:** saga derivatives are rejected at direct bus send/publish/schedule/request, outgoing-message, persistence-return, explicit append, and DCB event-payload boundaries while ordinary sibling payloads remain.
8. **Generated output invariant:** the saga fixture pins generated Screenplay bytes with a repeat equality assertion and an explicit SHA-256 hash, proving neutral saga facts and method-subject migration do not change `.play` syntax.
9. **Canonical saga symbol authority:** `WolverineSagaTypes.IsSagaState()` now requires the canonical `Wolverine.Saga` symbol itself to be metadata or authoritative authored source. The authority predicate is centralized and shared with event-stream/DCB recognition. An adversarial generated-framework fixture proves that a generated-source-only canonical symbol emits no Saga evidence while authored derivatives remain eligible for ordinary Message and Event classification.

Generated-source exclusion, authored/generated boundaries, exact correlation, current/legacy API admission, direct/nested saga exclusion, ordinary sibling payloads, non-saga behavior, generated Screenplay bytes, and deterministic diagnostic ordering are covered by focused specs.

## Architectural boundary

Do not solve the findings by adding more direct Marten/Wolverine adapter dependencies. The released foundation deliberately keeps persisted events, messages, cascades, responses, side effects, documents, and saga state distinct.

Prefer completing the atomic boundary in #44 first:

```text
Marten adapter
Wolverine adapter
exact Wolverine-Marten integration adapter
    -> neutral contributions
    -> one Generation resolver/lowerer/printer/compiler pass
```

The integration adapter may join exact cross-framework evidence. Atomic adapters must not consume one another.

`WOLVERINE0016` is report-only realization/provenance. Wolverine-managed lifecycle is intentionally not lowered because authored source does not safely establish a portable domain workflow. Screenplay uses ordinary Event Modeling building blocks. This is **not** a language-gap request and does not require or propose Saga syntax. The neutral Saga/Handler/Handles facts and pre-release internal handler-subject migration remain reviewable in the semantic graph, while canonical generated `.play` bytes intentionally remain unchanged.

## Completed local evidence

Fresh local evidence for this working tree:

- Full Debug suite: **698 specs passed**, zero failures and zero warnings.
- Focused saga lifecycle, correlation, method-identity, graph-compatibility, generated-boundary, event-stream/DCB, and non-saga regressions: **218 specs passed**, zero failures and zero warnings.
- Release restore and build: `net8.0`, `net9.0`, and `net10.0` completed with **0 warnings / 0 errors**.
- Sentinel package validation and the clean package consumer passed in the supplied local certification run. They were not rerun for the final internal symbol-authority guard because it changes no public package surface.
- All seven canonical fixtures — BankAccountES, CqrsMinimalApi, Reports, MartenWithProjectAspire, IncidentService, VogenConcepts, and Helpdesk — generated twice with matching hashes and retained expectations in the supplied local certification run. The six non-saga outputs remained unchanged. After the authority guard, VogenConcepts was rerun once against its retained expectation and passed with source SHA-256 `688ab242f2f40c2a5334f61194af644bdb24b6d189083fc1a0c6878cf10cc745`.
- Current and legacy Wolverine API shapes remain covered by pinned and synthetic contexts.
- Compatibility/status documentation records `WOLVERINE0016` as report-only realization/provenance, not a language-gap request.
- Independent diff and security review completed. Certification reported one remaining blocker; the canonical-symbol authority guard and generated-only adversarial fixture in this working tree address that finding.

## Final status

- [x] Full Debug specs.
- [x] Release build for all target frameworks.
- [x] Focused regression specs.
- [x] Seven canonical two-pass checks and retained expectations.
- [x] Six non-saga canonical outputs unchanged.
- [x] Current and legacy Wolverine API coverage.
- [x] Compatibility/status documentation.
- [x] Sentinel package validation and clean package consumer.
- [x] Independent review and certification finding addressed locally.
- [x] Final GitHub CI completed for the shipped change.
- [x] The bounded saga work merged and shipped in `v0.21.0`.

## Historical continuation note

The bounded saga candidate is shipped in `v0.21.0`; do not resume the old stash/branch workflow. Remaining atomic adapter or CLI roster work is tracked independently in its owning repositories.
