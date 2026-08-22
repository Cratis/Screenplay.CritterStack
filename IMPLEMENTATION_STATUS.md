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
- `main` is synchronized with `origin/main` through the `v0.6.0` Wolverine pure-automation release.
- Initial adapter PR: <https://github.com/Cratis/Screenplay.CritterStack/pull/2> (merged).
- Latest release/tag: [`v0.6.0`](https://github.com/Cratis/Screenplay.CritterStack/releases/tag/v0.6.0). Its release/restore/build/pack jobs succeeded; NuGet login remains blocked by trusted-publishing setup in run `32568782112`.
- The verified `Cratis.CritterStack.Screenplay.0.1.0.nupkg` remains attached to the `v0.1.0` GitHub release for temporary bootstrap restores.
- NuGet publishing blocker: [issue #1](https://github.com/Cratis/Screenplay.CritterStack/issues/1); nuget.org returns 404 until a trusted-publishing policy is created and the publish job is rerun.
- Research/handover commit imported from the original unpublished Screenplay branch.
- Source projects were moved here before their first source commit.
- No `.pi`, credentials, `bin`, or `obj` artifacts were transferred.

## Implemented locally

- `Cratis.CritterStack.Screenplay` adapter implementing the shared .NET adapter interface.
- Marten projection registration discovery for snapshots and explicit single-stream projections.
- Generic and instance-based projection registration discovery, including inherited JasperFx `Add(...)` APIs and configured-evidence provenance.
- Explicit `MARTEN0004` diagnostics for async/live projection lifecycles that Screenplay cannot currently represent.
- Markerless event discovery from `Apply`, `Create`, and `ShouldDelete` conventions.
- Direct Marten document discovery from registration, Store, Insert, Update, Delete, Load, and Query usage, with Store/Update/Delete/Read relationships and `MARTEN0003` rather than invented read models.
- Read-model/reducer/builds/consumes facts.
- Wolverine HTTP and message-handler discovery with route/response/version/validation loss diagnostics.
- Context-aware handling of aggregate returns, direct stream operations, HTTP query returns, document deletes, and command/read-model relationships.
- Slot-level Wolverine return classification keeps HTTP responses, persisted events, persistence wrappers, cascades, `OutgoingMessages`, side effects, and direct `IEventStream<T>` consequences distinct.
- Current and legacy Wolverine handler/return metadata, explicit and ignored handlers, and inactive open-generic/abstract handler types.
- Exact direct bus send, publish, request/reply, scheduling, delivery-option scheduling, and topic-broadcast consequences with stable discriminators and no event fabrication.
- Pure direct-bus handlers represented as reactions with message triggers and explicit lowering loss.
- Evidence-strength-based placements so exact Wolverine behavior overrides Marten heuristics.
- Dedicated synthetic Marten specs.
- Complete `CritterStackScreenplayGenerator` compilation-in/source-out façade matching the Arc package architecture.
- Adapter version derived from assembly informational version.
- Synthetic current Wolverine/Marten fixture covering start stream, aggregate events, response wrappers, `Events`, `OutgoingMessages`, external events, direct document deletion, and queries.
- Pattern-discovery research maps Marten/Wolverine source evidence into State Change, State View, Automation, and Translation without conflating responses, cascades, publishes, side effects, or persisted events.
- `COMPATIBILITY.md` records exact canonical package combinations, current source baselines, and explicit support tiers; CLI `v2.12.0` implements the planned NuGet provenance seam.
- `VOGEN_CONCEPT_DISCOVERY_RESEARCH.md` establishes the reusable neutral-concept/Vogen architecture. Screenplay.Generation v0.2.0/v0.3.0 now provide neutral concept representation, attributes, subject-aware references, deterministic resolution, and lowering; validation remains in #6 before Vogen interpretation #7 and Critter Stack composition #25.

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
- 92 adapter/generator specs pass.
- Real canonical IncidentService generation succeeds and captures commands, outgoing/delayed messages, external `Archived`, query/read model/reducer, and document deletion with explicit WOLVERINE0001-0005 loss diagnostics.
- `UpdatedAggregate` is correctly excluded from events.
- Real Marten 6/Wolverine 1 CritterStackHelpDesk generation now produces a compiling document after project/module-name sanitization.
- Pinned MartenWithProjectAspire generation verifies instance-registered async single-stream, multi-stream, and event projections with explicit `MARTEN0001`, `MARTEN0002`, and `MARTEN0004` loss diagnostics.
- A fresh six-fixture canonical run passed. The adapter Release build had zero warnings/errors; pinned external Reports, MartenWithProjectAspire, and legacy HelpDesk builds retained their documented upstream vulnerability/EOL/nullability warnings without suppression.
- Local package pack succeeded with sentinel version 9999.0.0; nuspec dependency direction remains Generation 0.1.0 + Roslyn only.
- Verified v0.5.0 and v0.6.0 packages are attached to their GitHub releases and installed in `~/.nuget/cratis-local`; scratch consumers restore and execute without repository workflow changes.

## Current cross-repository status

- [`Cratis/Screenplay.Generation` PR #1](https://github.com/Cratis/Screenplay.Generation/pull/1) is merged and tagged [`v0.1.0`](https://github.com/Cratis/Screenplay.Generation/releases/tag/v0.1.0).
- All three Generation nupkgs are attached to the release, but nuget.org returns 404 until [Generation issue #2](https://github.com/Cratis/Screenplay.Generation/issues/2) is resolved.
- [`Cratis/Arc` PR #2594](https://github.com/Cratis/Arc/pull/2594) is merged; `Cratis.Arc.Screenplay` 22.0.0 is published against Screenplay 4.2.1. Arc issue #2558 is closed.
- [`Cratis/cli` PR #84](https://github.com/Cratis/cli/pull/84) is merged in CLI `v2.11.0`. Its discoverable, allowlisted provider registry selects bundled source providers from semantic evidence, lets Critter Stack supersede its Marten foundation, and rejects unrelated provider ambiguity. `CLI0008` rejects authored-source compilation errors; `CLI0009` rejects ambiguous multi-host solutions; `CLI0010`/`CLI0011` reject no-match/multiple-provider auto detection.
- PR #84 passed all four GitHub checks. Fresh local verification after merging current CLI main into the branch passed 568 CLI specs and a Release build with zero warnings and zero errors. Real auto-provider generation was also rechecked against BankAccountES and IncidentService.
- CLI documentation PR #86 is merged. It labels Marten/Critter Stack generation as preview, explains discovery and trust boundaries, and documents `CLI0008`–`CLI0011`.
- CLI `v2.11.0` published successfully to NuGet, GitHub release assets, and Homebrew. The Homebrew installation was upgraded from 2.10.1 to 2.11.0 and tested from `/tmp`: Arc, BankAccountES, and IncidentService generation plus validation all exited successfully. The generated files were 74, 160, and 133 lines respectively. Generation reported explicit known losses—Arc 2 warnings/2 information, BankAccountES 1 warning/9 information, IncidentService 4 warnings/6 information—and IncidentService validation retained 7 known undeclared-type warnings while Arc and BankAccountES validated without compiler diagnostics.
- [`Cratis/cli` PR #88](https://github.com/Cratis/cli/pull/88) is merged and released as `v2.12.0`. It adds selected-target `project.assets.json` package provenance, assembly identities, API capability fingerprints, and independent support-tier/recognition/semantic-conformance/lowering-fidelity reporting. `CLI0012`/`CLI0013` fail closed outside admitted framework generations, and `CLI0014` reports unsupported provider options. Verification passed 623 specs, a zero-warning/error Release build, all four PR checks, sentinel packing, and the full NuGet/native/GitHub/Homebrew release workflow.
- CLI `v2.12.0` still declares `Cratis.CritterStack.Screenplay` 0.1.0 from the temporary release-asset bootstrap. Exact application package sets therefore remain `SourceReviewed` in the shipped CLI. Local development can override to current 0.6.0 from `~/.nuget/cratis-local`, where all six canonical applications generate as `Canonical`.
- Local CLI worktree `/Volumes/sourcecode/repos/cratis/cli-critter` and its feature branch were removed after merge. The durable continuation root remains `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack`.

## Exact continuation order

1. In nuget.org, manually create one owner-scoped trusted-publishing policy for GitHub owner `Cratis`, repository `Screenplay.Generation`, workflow filename `publish.yml`, and no environment. One policy covers all packages owned by the selected NuGet owner; do not create one per package ID. NuGet currently has no policy-management API/CLI—NuGet/NuGetGallery#10690 tracks that request.
2. Rerun failed Generation publish run `32435010554`; verify all three `0.1.0` package IDs resolve from nuget.org, then close Generation issue #2.
3. Manually create one owner-scoped policy for GitHub owner `Cratis`, repository `Screenplay.CritterStack`, workflow filename `publish.yml`, and no environment.
4. Rerun every failed Critter Stack release, including `32437573817` (0.1.0), `32540542422` (0.3.0), `32565807453` (0.4.0), `32567975867` (0.5.0), and `32568782112` (current 0.6.0); verify the package versions resolve from nuget.org, then close Critter Stack issue #1.
5. Remove temporary GitHub-release-asset bootstrap restore steps from Critter Stack and CLI workflows; use ordinary nuget.org restore and rerun all checks.
6. Enable package validation (Generation #3 and Critter Stack #7).
7. Update CLI from the temporary adapter 0.1.0 bootstrap to the published current package so exact canonical package sets can be promoted from `SourceReviewed` to `Canonical`.
8. Complete neutral concept validation in Screenplay.Generation #6, then implement authored-source Vogen interpretation (#7) and Critter Stack composition/canonical coverage (#25).
9. Continue remaining Marten completeness (#3)—compiled queries, identity configuration, EventProjection document operations, multi-stream grouping/fan-out, daemon/subscription configuration, aliases/upcasts, and tenancy—or Wolverine completeness (#4), followed by measured Screenplay language gaps (`Cratis/Screenplay#128`).

## Known implementation gaps

- NuGet trusted-publishing policies are not configured for the new SDK or adapter package IDs; this blocks normal nuget.org restore and removal of the temporary release-asset bootstrap.
- Pinned canonical verification is complete through PR #11 and closed issue #5. It verifies current BankAccountES, a license-attributed current IncidentService fixture, and legacy CritterStackHelpDesk at immutable upstream commits.
- PR #14 extends canonical verification to CqrsMinimalApi and Reports and implements the first bounded part of Marten completeness issue #3.
- Instance-based Marten projection registrations and direct async/live lifecycle constants are recognized; lifecycle remains a diagnostic-only loss until Screenplay can represent it. Projection daemon/subscription settings and computed lifecycle values are not yet analyzed.
- CLI `v2.12.0` carries resolved package/assembly/capability provenance and explicit compatibility dimensions. Its committed package remains 0.1.0 until normal publication; local development can override to current 0.6.0 from `~/.nuget/cratis-local` and obtains `Canonical` support.
- Current/legacy return slots, direct-stream cascades, direct bus delivery, and pure bus automations are classified. Return-only/`OutgoingMessages` automations, configured discovery policies, richer transport topology, sagas, DCB, subscriptions/forwarding, and projection side effects remain.
- Route-only identities and HTTP response metadata are facts but current Screenplay syntax cannot represent them fully.
- Validation/authorization discovery is not implemented yet.
- Ambiguous multiple deployable hosts now have CLI specification coverage and fail with `CLI0009`; richer API/worker cross-project contract relationships still need acceptance coverage.
- MSBuildWorkspace can omit framework reference packs for net7/net9 projects. Canonical and CLI generation repair these references, and remaining authored-source errors now fail with `CLI0008` so error symbols cannot silently become artifacts.
- The current production project reference to the full Generation package should remain only if required by the complete generator façade; low-level analysis code must depend only on Contracts and DotNet.

## Safety

- Do not reference Marten or Wolverine runtime packages; match metadata names through Roslyn.
- Do not start an application host or connect to PostgreSQL by default.
- Do not map broker messages/local cascades to persisted Screenplay events.
- Do not commit external sample repositories wholesale; use license-compatible pinned fixtures and attribution.
- Do not commit local repository roots, secrets, connection strings, `.pi`, `bin`, or `obj`.
