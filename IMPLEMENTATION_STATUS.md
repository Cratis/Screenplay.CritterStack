<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Critter Stack Screenplay adapter implementation status

## Selected architecture

This repository owns Marten and Wolverine source interpretation and publishes a complete generator package:

```text
Cratis.CritterStack.Screenplay
  -> accepts Roslyn compilations
  -> discovers Marten facts
  -> discovers Wolverine facts
  -> applies Marten+Wolverine contextual semantics
  -> uses Cratis.Screenplay.Generation internally
  -> returns verified Screenplay source, semantic graph, and diagnostics
```

This intentionally mirrors the existing Arc architecture:

- Arc publishes `Cratis.Arc.Screenplay` from the Arc repository.
- Its generator accepts compilations, builds Screenplay AST/text, verifies it, and is consumed by Cratis CLI.
- Cratis CLI owns `MSBuildWorkspace` and passes compilations to complete generator packages.

Repository boundaries:

- `Cratis/Screenplay`: language/compiler/AST/printer/editor only.
- `Cratis/Screenplay.Generation`: shared facts, resolver/lowerer/verifier, and Roslyn adapter SDK.
- `Cratis/Screenplay.CritterStack`: this Marten/Wolverine adapter and full generator façade.
- `Cratis/Arc`: existing Arc adapter.
- `Cratis/cli`: workspace loading, host/provider selection, and output.

## Repository state

- GitHub repository: <https://github.com/Cratis/Screenplay.CritterStack>
- Local repository: `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack`
- Branch: `feat/critter-stack-adapter`
- Research/handover commit imported from the original unpublished Screenplay branch.
- Source projects were moved here before their first source commit.
- No `.pi`, credentials, `bin`, or `obj` artifacts were transferred.

## Implemented locally

- `Cratis.CritterStack.Screenplay` adapter implementing the shared .NET adapter interface.
- Marten projection registration discovery for snapshots and explicit single-stream projections.
- Markerless event discovery from `Apply`, `Create`, and `ShouldDelete` conventions.
- Read-model/reducer/builds/consumes facts.
- Initial Wolverine HTTP and message-handler discovery.
- Context-aware handling of aggregate returns, direct stream operations, HTTP query returns, document deletes, and command/read-model relationships.
- Evidence-strength-based placements so exact Wolverine behavior overrides Marten heuristics.
- Dedicated synthetic Marten specs.

## Verified before repository split

- Project built with zero warnings/errors.
- 18 synthetic Critter Stack specs passed.
- Real `~/CritterStackSamples/BankAccountES` built cleanly.
- A temporary MSBuildWorkspace host loaded real BankAccountES and generated a compiling `.play` document with 87 semantic facts and zero diagnostics.
- The generated model included:
  - Account/Client/AccountTransactions read models and reducers;
  - deposit, withdrawal, update, open, and enrollment state changes;
  - query endpoints;
  - correct distinction between aggregate event returns and HTTP results.
- Canonical Wolverine IncidentService built cleanly.

## Immediate dependency sequence

1. Finish, review, merge, and release `Cratis/Screenplay.Generation`.
2. Replace temporary local source references here with released package versions.
3. Add a complete `CritterStackScreenplayGenerator` façade matching Arc's public experience.
4. Rebuild and run synthetic specs independently.
5. Add pinned BankAccountES and IncidentService acceptance fixtures.
6. Complete current IncidentService semantics: response wrappers, route-only identity, `Events`, `OutgoingMessages`, delayed dispatch, external `Archived`, and direct delete evidence.
7. Add legacy Marten 6/Wolverine 1 fixtures from CritterStackHelpDesk.
8. Pack, inspect dependencies, scratch-consume, and release.
9. Integrate the published package into Cratis CLI.

## Known implementation gaps

- The package currently exposes only the low-level adapter, not the complete compilation-in/source-out façade.
- Adapter version is hard-coded instead of derived from package informational version.
- Wolverine source has only synthetic/real smoke evidence, not committed canonical specs yet.
- `Events` collection expressions and `OutgoingMessages` need fuller body-value analysis.
- Route-only identities and HTTP response metadata are facts but current Screenplay syntax cannot represent them fully.
- Validation/authorization discovery is not implemented yet.
- Multiple deployable hosts and cross-project contract identity still need acceptance coverage.
- The current production project reference to the full Generation package should remain only if required by the complete generator façade; low-level analysis code must depend only on Contracts and DotNet.

## Safety

- Do not reference Marten or Wolverine runtime packages; match metadata names through Roslyn.
- Do not start an application host or connect to PostgreSQL by default.
- Do not map broker messages/local cascades to persisted Screenplay events.
- Do not commit external sample repositories wholesale; use license-compatible pinned fixtures and attribution.
- Do not commit local repository roots, secrets, connection strings, `.pi`, `bin`, or `obj`.
