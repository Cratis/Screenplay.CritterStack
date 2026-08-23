<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Wolverine saga evidence prototype handover

## Status

This branch is a **paused prototype**, not part of the released source-to-Screenplay Preview baseline and not ready to merge.

It preserves the former local stash:

```text
stash commit: f8c4b96ba7bd9ae6e5888cd6bd6f703a6a57183b
message: prototype Wolverine saga evidence: 647 specs pass; requires independent review before ship
```

The last pre-pause run reported 647 passing specs. Treat that as historical evidence only; re-run every gate after changing or rebasing the prototype.

Tracking:

- Critter Stack #4 — Wolverine workflow/saga semantics and review findings;
- Critter Stack #44 — atomic Marten, Wolverine, and integration boundaries;
- Generation #17 and CLI #95 — atomic adapter execution and selection-only profiles.

## What the prototype explores

- current and legacy Wolverine saga discovery;
- saga, handler, message, and Handles/Cascades evidence;
- authored correlation-member evidence and runtime-correlation diagnostics;
- lifecycle-loss diagnostics for `MarkCompleted()`;
- saga-state return classification without treating state as a persisted event, response, or cascade;
- canonical VogenConcepts evidence for current package APIs.

Primary implementation and fixtures:

- `Source/DotNET/CritterStack/Wolverine/WolverineSagaFacts.cs`;
- `Source/DotNET/CritterStack/Wolverine/WolverineFacts.cs`;
- `Source/DotNET/CritterStack/Marten/MartenDocumentFacts.cs`;
- `Source/DotNET/CritterStack.Specs/given/a_wolverine_saga_application.cs`;
- `Source/DotNET/CritterStack.Specs/given/a_legacy_wolverine_saga_application.cs`;
- `Source/DotNET/CritterStack.Specs/for_CritterStackScreenplayGenerator/when_generating_wolverine_saga_evidence.cs`;
- `Source/DotNET/CritterStack.Specs/for_CritterStackScreenplayGenerator/when_generating_legacy_wolverine_saga_evidence.cs`;
- `Integration/Canonical/VogenConcepts/Sagas.cs`.

## Independent review findings that block merge

An independent read-only review found the following issues. Resolve them before treating the prototype as a candidate implementation.

1. **Saga state can be inferred as an HTTP query read model.** `WolverineFacts.AnalyzeQuery()` needs the same exact saga-state exclusion boundary as direct handler returns.
2. **Lifecycle role admission is incomplete.** Validate legal static/instance shapes for every role and require a start role to return valid saga state before emitting Saga, Handler, Message, or Handles evidence.
3. **Marten depends on Wolverine identity internals.** Remove `MartenDocumentFacts -> WolverineSagaFacts` coupling. Establish a neutral/shared method identity or wait for the atomic integration boundary in #44.
4. **Nested returns can reclassify saga state.** Exclude saga derivatives from nested `OutgoingMessages` and DCB event-payload extraction, not only top-level returns.
5. **Handler display names can collide.** Use signature-stable names for legal overloads; distinct subjects alone are insufficient.

Also re-check generated-source exclusion, exact symbol identity, correlation precedence, current/legacy API admission, and deterministic diagnostic ordering after every fix.

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

## Required verification before removing draft status

- Build Debug and Release with zero warnings and zero errors.
- Run the full specification suite with zero failures and confirm the expected test count.
- Add focused negative specs for every blocking finding above.
- Generate the canonical fixture twice and compare bytes.
- Confirm all pre-saga canonical evidence remains unchanged except reviewed additions.
- Run package validation and clean package-consumer checks.
- Verify current and legacy Wolverine package APIs from exact pinned dependencies.
- Run code, architecture, and security review after the implementation stabilizes.
- Update compatibility/status documentation to distinguish represented semantics from diagnostic-only evidence.

## Resume rule

Resume only for an explicit saga product requirement and after deciding whether #44 must land first. Keep the pull request in draft while any blocking finding remains. Do not merge merely because the historical 647-spec run can be reproduced.
