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
- `main` is clean and synchronized with `origin/main`.
- Initial adapter PR: <https://github.com/Cratis/Screenplay.CritterStack/pull/2> (merged).
- Release/tag: [`v0.1.0`](https://github.com/Cratis/Screenplay.CritterStack/releases/tag/v0.1.0).
- The verified `Cratis.CritterStack.Screenplay.0.1.0.nupkg` is attached to the GitHub release.
- NuGet publishing blocker: [issue #1](https://github.com/Cratis/Screenplay.CritterStack/issues/1); nuget.org returns 404 until a trusted-publishing policy is created and the publish job is rerun.
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

## Current cross-repository status

- [`Cratis/Screenplay.Generation` PR #1](https://github.com/Cratis/Screenplay.Generation/pull/1) is merged and tagged [`v0.1.0`](https://github.com/Cratis/Screenplay.Generation/releases/tag/v0.1.0).
- All three Generation nupkgs are attached to the release, but nuget.org returns 404 until [Generation issue #2](https://github.com/Cratis/Screenplay.Generation/issues/2) is resolved.
- [`Cratis/Arc` PR #2594](https://github.com/Cratis/Arc/pull/2594) is merged; `Cratis.Arc.Screenplay` 22.0.0 is published against Screenplay 4.2.1. Arc issue #2558 is closed.
- [`Cratis/cli` PR #84](https://github.com/Cratis/cli/pull/84) is an intentional draft. All checks pass. It is locally and in CI verified against Arc, BankAccountES, and canonical IncidentService source, but must not merge until the new Generation and Critter Stack packages are available from nuget.org.
- Local working trees for Screenplay, Screenplay.Generation, Screenplay.CritterStack, and the CLI feature branch are clean except Screenplay's ignored/untracked local `.pi/` state.

## Exact continuation order

1. In nuget.org, manually create one owner-scoped trusted-publishing policy for GitHub owner `Cratis`, repository `Screenplay.Generation`, workflow filename `publish.yml`, and no environment. One policy covers all packages owned by the selected NuGet owner; do not create one per package ID. NuGet currently has no policy-management API/CLI—NuGet/NuGetGallery#10690 tracks that request.
2. Rerun failed Generation publish run `32435010554`; verify all three `0.1.0` package IDs resolve from nuget.org, then close Generation issue #2.
3. Manually create one owner-scoped policy for GitHub owner `Cratis`, repository `Screenplay.CritterStack`, workflow filename `publish.yml`, and no environment.
4. Rerun failed Critter Stack publish run `32437573817`; verify `Cratis.CritterStack.Screenplay` 0.1.0 resolves from nuget.org, then close Critter Stack issue #1.
5. Remove temporary GitHub-release-asset bootstrap restore steps from Critter Stack and CLI workflows; use ordinary nuget.org restore and rerun all checks.
6. Mark CLI PR #84 ready, confirm all checks green, merge it, and monitor the CLI release.
7. Continue product work through tracked issues: Marten completeness (#3), Wolverine completeness (#4), package validation (#7), and Screenplay language gaps (`Cratis/Screenplay#128`).

## Known implementation gaps

- NuGet trusted-publishing policies are not configured for the new SDK or adapter package IDs; this is the only blocker to merging the already-green CLI integration.
- Pinned canonical verification is complete through PR #11 and closed issue #5. It verifies current BankAccountES, a license-attributed current IncidentService fixture, and legacy CritterStackHelpDesk at immutable upstream commits.
- `Events` and delayed `OutgoingMessages` collection expressions are recognized; direct bus calls, richer delivery options, and transport topology need fuller body-value analysis.
- Route-only identities and HTTP response metadata are facts but current Screenplay syntax cannot represent them fully.
- Validation/authorization discovery is not implemented yet.
- Multiple deployable hosts and API/worker cross-project contract relationships still need acceptance coverage.
- MSBuildWorkspace can omit framework reference packs for net7/net9 projects. Canonical verification now repairs these references, and CLI PR #84 carries the equivalent loader fix so error symbols cannot silently become artifacts.
- The current production project reference to the full Generation package should remain only if required by the complete generator façade; low-level analysis code must depend only on Contracts and DotNet.

## Safety

- Do not reference Marten or Wolverine runtime packages; match metadata names through Roslyn.
- Do not start an application host or connect to PostgreSQL by default.
- Do not map broker messages/local cascades to persisted Screenplay events.
- Do not commit external sample repositories wholesale; use license-compatible pinned fixtures and attribution.
- Do not commit local repository roots, secrets, connection strings, `.pi`, `bin`, or `obj`.
