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
- `main` / `origin/main` is released as [`v0.21.0`](https://github.com/Cratis/Screenplay.CritterStack/releases/tag/v0.21.0) at `f3f1909`; the current working tree contains an uncommitted, review-only compatibility increment.
- Initial adapter PR: <https://github.com/Cratis/Screenplay.CritterStack/pull/2> (merged).
- Trusted NuGet publication is operational; correct releases restore normally from nuget.org and are mirrored to `~/.nuget/cratis-local` after exact-tag verification.
- Current project dependencies use the four Screenplay.Generation packages at `0.9.0`; package compatibility remains guarded independently from implementation details.
- Historical versions requiring manual unlisting are tracked in [issue #37](https://github.com/Cratis/Screenplay.CritterStack/issues/37); OIDC publishing credentials cannot delete or unlist.
- Research/handover commit imported from the original unpublished Screenplay branch.
- Source projects were moved here before their first source commit.
- No `.pi`, credentials, `bin`, or `obj` artifacts were transferred.

## Implemented locally

- `Cratis.CritterStack.Screenplay` adapter implementing the shared .NET adapter interface.
- Marten projection registration discovery for snapshots and explicit single-stream projections.
- Generic and instance-based projection registration discovery, including inherited JasperFx `Add(...)` APIs and configured-evidence provenance.
- Explicit `MARTEN0004` diagnostics for async/live projection lifecycles that Screenplay cannot currently represent.
- Exact current and legacy authored projection name/version evidence, async daemon mode evidence, and first-class `ISubscription` / `SubscriptionBase` registration evidence for `Events.Subscribe` and `AddSubscriptionWithServices`, including direct filters, start policy, archived-event policy, and name/version declarations. Current contracts retain these as `MARTEN0007`-`MARTEN0010` diagnostics and neutral custom-projection artifacts rather than inventing slice semantics.
- Exact authored Marten 9.29 event aliases, suffix/schema-version helpers, global naming style, `[MartenEvent(Alias=...)]`, typed/raw upcasts, and recognized CLR/SystemTextJson/JsonNet class-upcaster registrations are retained as `MARTEN0011`/`MARTEN0012` diagnostics. They never rename or originate Event artifacts or infer behavioral relationships, runtime execution, ordering, precedence, or reachability.
- Exact authored logical tenancy declarations are retained as `MARTEN0013` diagnostics: current/legacy `Single` and `Conjoined` event styles, exact per-document fluent calls and attributes, and exact global policy calls. Evidence remains occurrence-based and never creates tenancy facts, artifacts, relationships, tenant-specific duplicates, policy-expanded documents, or runtime/database topology claims.
- Markerless event discovery from `Apply`, `Create`, and `ShouldDelete` conventions.
- Direct Marten document discovery from registration, Store, Insert, Update, Delete, Load, and Query usage, with Store/Update/Delete/Read relationships and `MARTEN0003` rather than invented read models.
- Marten document identity discovery honors direct `Schema.For<T>().Identity(...)` configuration, exact identity attributes, and `Id` conventions; unresolved configured expressions produce `MARTEN0005` and suppress fallback guesses.
- Exact `IQuerySession` and `IBatchedQuery` compiled-query executions link document reads and public plan parameters only to proven Wolverine HTTP query entry points; generated handlers, ordinary query plans, unrelated same-named methods, and unused plans remain excluded. Nested local-function and lambda calls are linked only when their endpoint invocation is proven; unresolved flow reports `MARTEN0006`.
- Read-model/reducer/builds/consumes facts.
- Literal `IEventSlice<T>.PublishMessage(new TMessage(...))` calls inside snapshot, single-stream, multi-stream, and event projections emit Message artifacts and `Publishes` relationships from the projection. Observed `EnableSideEffectsOnInlineProjections = true` yields exact evidence; absent authored configuration retains conventional evidence, non-literal payloads report `MARTEN0015`, and published messages are never conflated with events.
- Authored `IDocumentSessionListener` / `IChangeListener` implementations and `Listeners.Add(...)` registrations report `MARTEN0016` once per listener type without interpreting commit-hook bodies.
- Exact `UseBinarySerializer<T>`, `AppendMode`, and `StreamIdentity` declarations join the existing `MARTEN0011` configuration-loss family. `RegisterValueType<T>` and `RegisterValueType(typeof(T))` nominate neutral Concept artifacts with configured evidence; lowering still requires an independent representation fact, supplied today by adapters such as Vogen and otherwise reserved for shared Generation-level representation discovery.
- Wolverine HTTP and message-handler discovery with route/response/version/validation loss diagnostics.
- Context-aware handling of aggregate returns, direct stream operations, HTTP query returns, document deletes, and command/read-model relationships.
- Exact target-aware `JasperFx.Events.IEventStream<T>` handling preserves every parameter binding, receiver-specific `AppendOne`/`AppendMany` events, per-binding Reads/Appends identities, and stable `WOLVERINE0012`/`WOLVERINE0013` loss diagnostics without inventing read models or first-stream ownership.
- Bounded Wolverine DCB interpretation admits one authored current/legacy model parameter only when a public authored tag-query companion exists; preserves direct `Or<TTag>`, `Or<TEvent,TTag>`, and `AndEventsOfType<T...>` conditions as neutral Aggregate/Reads evidence; emits only proven events/production; and reports `WOLVERINE0014`/`WOLVERINE0015` without inventing read models, reducers, projections, or stream targets.
- Bounded authored Wolverine saga discovery admits public concrete closed exact `Wolverine.Saga` derivatives active under source-resolved discovery/include/ignore policy. Roles are grouped per message to mirror actual `SagaChain` admission: isolated static `Starts`/`StartsAsync` and `NotFoundAsync` are rejected, direct primitive returns are excluded, and instance/fallback creation requires a public parameterless constructor unless an exact returned saga supplies a static start-only chain. Existing-only chains remain valid. It emits unplaced neutral Saga state, collision-safe Handler identities, authored-only Message properties, and role-discriminated Handles evidence; follows Wolverine correlation precedence across every handler parameter with explicit-name replacement, inherited public fields/properties, and located fail-closed ambiguity; excludes saga state at every final HTTP query, direct bus, nested message, persistence, DCB, and explicit event-stream append admission boundary while preserving ordinary siblings; preserves ordinary cascades, timeout reports, direct bus and document operations; and reports `WOLVERINE0016`/`WOLVERINE0017`/`WOLVERINE0018` without inventing lifecycle persistence or transport topology. `WOLVERINE0016` is report-only realization/provenance: Wolverine-managed lifecycle is intentionally not lowered because authored source does not safely establish a portable domain workflow. Screenplay uses ordinary Event Modeling building blocks; this is not a language-gap request, requires no Saga syntax, and keeps generated `.play` bytes unchanged.
- Slot-level Wolverine return classification keeps HTTP responses, persisted events, persistence wrappers, cascades, `OutgoingMessages`, side effects, saga state, and direct `IEventStream<T>` consequences distinct; returned saga state is excluded from event, message, command, aggregate, read-model, production, and cascade facts while other tuple slots continue through existing consequence handling.
- Exact returned `IStorageAction<T>` and `UnitOfWork<T>` slots retain their entity type as persistence state; static `Storage.Delete<T>` and `Storage.Update<T>` calls refine the relationship kind while unresolved unit-of-work flow remains `Stores`, mixed tuple cascades remain independent, and storage entities are never classified as events.
- Neutral Marten and Wolverine handler subjects share full .NET documentation method identities. Ordinary and saga overloads remain distinct, preserve readable artifact names, and converge cross-adapter identity without changing generated Screenplay bytes.
- Current and legacy Wolverine handler/return metadata, explicit and ignored handlers, and inactive open-generic/abstract handler types.
- Exact Wolverine `T[]` batch-handler admission resolves the authored element message type, retains batched delivery evidence, and marks the `Handles` relationship as a collection without requiring authored batching configuration.
- Exact Wolverine `[Entity]`, `[FirstOrDefault]`, and `[Queryable]` parameter bindings emit typed `Reads` relationships with binding discriminators, nullable/`Required = false` optionality, queryable collection semantics, and request-message exclusion; aggregate workflow parameters are not lowered twice.
- Exact authored `HandlerDiscovery.DisableConventionalDiscovery()` and `IncludeType<T>()` / `IncludeType(typeof(T))` configuration, same-compilation assembly inclusion, generated-source exclusion, and stable `WOLVERINE0007` diagnostics when custom predicates or external assembly scans cannot be resolved without guessing.
- Presence-only convention-alteration diagnostics retain `WOLVERINE0019` for authored handler policies, chain-modifying attributes, Wolverine extensions, and message-discovery customization, plus `MARTEN0014` for authored Marten configuration and document/projection policies. Policy bodies remain uninterpreted and do not change otherwise discovered facts.
- Exact source-bound FluentValidation/DataAnnotations message and HTTP policy activation, validator/annotation applicability, compound `Validate`/`ValidateAsync` middleware, ASP.NET authorization attributes, anonymous overrides, and global Wolverine endpoint authorization, with stable diagnostics instead of invented relationships when current contracts cannot represent the behavior.
- Compound `Load*`, `Before*`, `After*`, `PostProcess*`, `Finally*`, and after-commit stages retain `OutgoingMessages` and safe cascade consequences on the owning entry point with `stage:<method>` discriminators. `HandlerContinuation`/`RequirementResult` remain control flow, loaded data is not fabricated as messaging, DCB companions stay under DCB discovery, and `WOLVERINE0020` reports remaining data-loading and continuation loss per stage.
- Public static `Configure(HandlerChain)` members report `WOLVERINE0021` on each owning entry point without interpreting retry/discard policy bodies.
- Exact direct bus send, publish, request/reply, scheduling, delivery-option scheduling, and topic-broadcast consequences with stable discriminators and no event fabrication.
- Pure direct-bus handlers represented as reactions with message triggers and explicit lowering loss.
- Evidence-strength-based placements so exact Wolverine behavior overrides Marten heuristics.
- Dedicated synthetic Marten specs.
- Complete `CritterStackScreenplayGenerator` compilation-in/source-out façade matching the Arc package architecture, with default independent Vogen + Critter Stack composition, external adapter-list injection, and preserved contribution provenance.
- Adapter version derived from assembly informational version.
- Synthetic current Wolverine/Marten fixture covering start stream, aggregate events, response wrappers, `Events`, `OutgoingMessages`, external events, direct document deletion, and queries.
- Pattern-discovery research maps Marten/Wolverine source evidence into State Change, State View, Automation, and Translation without conflating responses, cascades, publishes, side effects, or persisted events.
- `COMPATIBILITY.md` records exact canonical package combinations, current source baselines, and explicit support tiers; CLI owns NuGet provenance and provider admission.
- `CRITTER_STACK_SOURCE_ADAPTER_CASE_STUDY.md` documents how maintainers extend the Marten/Wolverine source adapter; `WRITING_CRITTER_STACK_RENDERER.md` is an unapproved design proposal for a possible separate Stage-based Screenplay-to-Critter Stack renderer, not a defined implementation path or commitment.
- `VOGEN_CONCEPT_DISCOVERY_RESEARCH.md` establishes the reusable neutral-concept/Vogen architecture. Screenplay.Generation 0.9.0 supplies neutral concept validation and the separate Vogen adapter; the Critter Stack facade composes it without adding Vogen semantics to the Marten/Wolverine adapter.

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
- 579 combined Critter Stack specs pass in Debug on .NET 10; the post-rebase Release build is warning-free on .NET 8, .NET 9, and .NET 10.
- Real canonical IncidentService generation succeeds and captures commands, outgoing/delayed messages, external `Archived`, query/read model/reducer, document deletion, and compound validation with explicit WOLVERINE0001-0005 loss diagnostics.
- `UpdatedAggregate` is correctly excluded from events.
- Real Marten 6/Wolverine 1 CritterStackHelpDesk generation now produces a compiling document after project/module-name sanitization.
- Pinned MartenWithProjectAspire generation verifies projection grouping, EventProjection operations, exact projection metadata, and daemon mode while unresolved semantics remain explicit diagnostics.
- A seven-fixture canonical run includes the pinned Cratis-owned Vogen 8.0.7 fixture; the six pre-existing non-Vogen outputs remain byte-identical.
- Package validation uses the public 0.1.0 baseline with sentinel version 9999.0.0; the isolated consumer uses Generation 0.7.0 plus the separate Vogen adapter without a Vogen source-generator/runtime dependency in the production facade.
- Exact-tag distribution verification at this historical checkpoint covered release assets, signatures, dependencies, local-feed installation, and clean consumers; current release status is recorded above.

## Current cross-repository status

- This repository is released as `v0.21.0` and currently references Screenplay.Generation `0.9.0` packages.
- Cratis CLI remains the primary production consumer and owns workspace loading, target-framework selection, provider admission, package provenance, and distribution. Consult the CLI repository for its current release rather than duplicating a version snapshot here.
- The present T1–T7 compatibility increment is uncommitted and review-only. It has focused source specs plus public-sample compatibility evidence; no new synthetic canonical fixture was added.
- Historical delivery details below remain dated evidence rather than current continuation instructions.

## Current working-tree verification

- Full Debug suite: **858 specs passed**, zero failures.
- Release solution build: `net8.0`, `net9.0`, and `net10.0`, zero warnings and zero errors.
- Package validation against released `0.21.0` and the clean isolated `9999.0.0` candidate-package consumer pass.
- All retained canonical expectations pass twice with byte-identical output, including the pinned public Critter Stack samples and public HelpDesk application.
- Additional pinned public sample checks prove signature-stable overloaded handlers, projection publication, compound stages, and per-chain configuration without adding a new canonical fixture.
- The working tree remains unstaged and uncommitted for human review.

## Historical cross-repository delivery evidence

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

1. Keep the current compatibility increment uncommitted until human review explicitly authorizes shipping.
2. Use only pinned public Critter Stack samples or the public HelpDesk application for additional package-level compatibility evidence; focused authored-source specs remain appropriate for bounded semantic cases.
3. Keep `WOLVERINE0022` deferred until a solution-wide authored implementation manifest can join interface calls to implementations across project boundaries without guessing.
4. Add Screenplay language capabilities only from measured loss under `Cratis/Screenplay#128`.
5. Keep historical package unlisting work external/manual.

## Known implementation gaps

- NuGet OIDC publication cannot delete or unlist the historically mispublished versions; manual owner action remains tracked separately.
- Pinned canonical verification is complete through PR #11 and closed issue #5. It verifies current BankAccountES, a license-attributed current IncidentService fixture, and legacy CritterStackHelpDesk at immutable upstream commits.
- PR #14 extends canonical verification to CqrsMinimalApi and Reports and implements the first bounded part of Marten completeness issue #3.
- Instance-based Marten projection registrations and direct async/live lifecycle constants are recognized; lifecycle remains a diagnostic-only loss until Screenplay can represent it. Authored event aliases and upcast registrations are also diagnostic-only: generated source, unresolved constants, arbitrary `IEventUpcaster` implementations, upcaster bodies/constructors/`EventTypeName` overrides, mixed inline upcaster collections, `AddEventType*`, alias-less `[MartenEvent]`, legacy `EventGraph.EventMappingFor<T>().EventTypeName`, and runtime precedence/reachability are deliberately not inferred. Exact projection name/version, daemon mode, and first-class subscription registration/options are source-reviewed and diagnostic-only because the current Generation contracts have no subscription or daemon metadata artifact. Conditional/computed settings, arbitrary `SubscribeFromTime` expressions, non-constructor configuration helpers, manual daemon agent/shard operations, and arbitrary `ProcessEventsAsync` consequences remain unresolved rather than guessed. Authored `EventProjection.Create` returns and event-bound `IDocumentOperations.Store`/`Insert`/`Update`/`Delete`/`DeleteWhere` calls emit exact event, projection, document, and relationship facts without inventing read models; arbitrary body/value flow remains diagnostic-only. Exact directly-authored `MultiStreamProjection<T,TId>` identity/member and fan-out declarations retain neutral evidence, while custom groupers/slicers, computed or conditional selectors, and tenancy-dependent grouping fail closed with `MARTEN0001`. Logical tenancy is diagnostic-only: computed/invalid/stale values fail closed, and effective precedence, callback behavior, sessions/`ForTenant`, tenant ids, database mappings, partition/shard topology, daemon-per-tenant behavior, and projection consequences remain deliberately unanalyzed. Computed lifecycle values, manual daemon shard operations, runtime subscription configuration, and other document operation families remain unanalyzed.
- Compiled queries are linked from exact Marten execution calls to proven Wolverine HTTP query entry points, but arbitrary expression reconstruction and non-Wolverine application entry-point classification remain out of scope.
- Current/legacy return slots, direct-stream cascades, direct bus delivery, pure bus automations, return-only/`OutgoingMessages` automations, exact source-bound handler discovery activation, bounded DCB source evidence, and bounded authored saga state/role/correlation/lifecycle realization/provenance evidence are classified. Runtime/custom predicate discovery, external assembly and handler-module scanning, saga persistence-provider/final-state semantics, richer transport topology, arbitrary DCB query/data flow, runtime descriptors, `FromConditions`, cross-boundary routing, subscriptions/forwarding, and arbitrary non-literal projection side-effect value flow remain.
- Persistence indirection through authored non-framework abstractions remains an open residual. `WOLVERINE0022` is reserved until a solution-wide authored implementation manifest can join interface members to every visible implementation across project boundaries; a per-project one-hop heuristic is intentionally not shipped because public samples provide no positive recall evidence.
- Route-only identities and HTTP response metadata are facts but current Screenplay syntax cannot represent them fully.
- Validation/authorization discovery is deliberately source-bound: package presence alone has no effect; exact built-in policy activation and applied behavior are retained as `WOLVERINE0005` and `WOLVERINE0008`-`WOLVERINE0011` diagnostics because current Generation contracts have no faithful neutral representation for that behavior. Runtime/custom validator registration, conditional policy activation, custom `IHttpPolicy` behavior, and broader ASP.NET route-group/fallback-policy data flow remain unresolved rather than guessed.
- Ambiguous multiple deployable hosts now have CLI specification coverage and fail with `CLI0009`; richer API/worker cross-project contract relationships still need acceptance coverage.
- MSBuildWorkspace can omit framework reference packs for net7/net9 projects. Canonical and CLI generation repair these references, and remaining authored-source errors now fail with `CLI0008` so error symbols cannot silently become artifacts.
- The production facade requires the full Generation and separate Vogen adapter packages; `CritterStackScreenplayAdapter` and the Marten/Wolverine readers remain independent of Vogen metadata and source-generator/runtime packages.

## Safety

- Do not reference Marten or Wolverine runtime packages; match metadata names through Roslyn.
- Do not start an application host or connect to PostgreSQL by default.
- Do not map broker messages/local cascades to persisted Screenplay events.
- Do not commit external sample repositories wholesale; use license-compatible pinned fixtures and attribution.
- Do not commit local repository roots, secrets, connection strings, `.pi`, `bin`, or `obj`.
