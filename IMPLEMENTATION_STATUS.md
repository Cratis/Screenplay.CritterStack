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
- Initial adapter PR: <https://github.com/Cratis/Screenplay.CritterStack/pull/2> (merged).
- Release/tag: `v0.1.0`.
- NuGet publishing blocker: issue #1; the verified nupkg is attached to the GitHub release until the trusted-publishing policy exists.
- Research/handover commit imported from the original unpublished Screenplay branch.
- Source projects were moved here before their first source commit.
- No `.pi`, credentials, `bin`, or `obj` artifacts were transferred.

## Implemented locally

- `Cratis.CritterStack.Screenplay` adapter implementing the shared .NET adapter interface.
- Marten projection registration discovery for snapshots and explicit single-stream projections.
- Markerless event discovery from `Apply`, `Create`, and `ShouldDelete` conventions.
- Read-model/reducer/builds/consumes facts.
- Wolverine HTTP and message-handler discovery with route/response/version/validation loss diagnostics.
- Context-aware handling of aggregate returns, direct stream operations, HTTP query returns, document deletes, and command/read-model relationships.
- Evidence-strength-based placements so exact Wolverine behavior overrides Marten heuristics.
- Dedicated synthetic Marten specs.
- Complete `CritterStackScreenplayGenerator` compilation-in/source-out façade matching the Arc package architecture.
- Adapter version derived from assembly informational version.
- Synthetic current Wolverine/Marten fixture covering start stream, aggregate events, response wrappers, `Events`, `OutgoingMessages`, external events, direct document deletion, and queries.

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
- The independent repository now builds Debug/Release with zero warnings/errors.
- 36 adapter/generator specs pass.
- Real canonical IncidentService generation succeeds and captures commands, outgoing/delayed messages, external `Archived`, query/read model/reducer, and document deletion with explicit WOLVERINE0001-0005 loss diagnostics.
- `UpdatedAggregate` is correctly excluded from events.
- Real Marten 6/Wolverine 1 CritterStackHelpDesk generation now produces a compiling document after project/module-name sanitization.
- Local package pack succeeded; nuspec dependency direction is Generation 0.1.0 + Roslyn only.

## Immediate dependency sequence

1. Screenplay.Generation PR #1 is merged and tagged `v0.1.0`; build/pack passed, but NuGet push is blocked by missing trusted-publishing policies tracked in Screenplay.Generation issue #2.
2. Once the policies exist, rerun publish and verify all three 0.1.0 packages on nuget.org.
3. After policy setup, rerun Critter Stack publish run 32437573817 and verify package 0.1.0 on nuget.org; remove temporary release-asset bootstrap steps.
4. Cratis/Arc#2594 is merged and `Cratis.Arc.Screenplay` 22.0.0 is published against Screenplay 4.2.1.
5. Cratis/cli#84 is implemented and locally verified against real Arc, BankAccountES, and IncidentService source; it remains draft until the new packages are available from nuget.org.
6. Add pinned canonical fixture projects (issue #5), complete Marten analysis (#3), and complete Wolverine analysis (#4).
7. Design language additions from measured losses in Cratis/Screenplay#128.

## Known implementation gaps

- NuGet trusted-publishing policies are not configured for the new SDK or adapter package IDs.
- Wolverine source has committed synthetic canonical coverage and real smoke evidence, but not yet frozen full-project fixtures.
- `Events` and delayed `OutgoingMessages` collection expressions are recognized; direct bus calls, richer delivery options, and transport topology need fuller body-value analysis.
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
