<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Critter Stack source generation: implementation plan and handover

## Mission

Add source-derived Screenplay generation for:

1. Marten applications;
2. Marten + Wolverine applications;
3. current and legacy Critter Stack conventions;
4. future framework/language adapters without coupling them to Arc, Roslyn, or `.play` text emission.

The implementation must preserve existing Arc generation behavior and public APIs, generate deterministic valid `.play`, avoid application/database startup by default, and report every recognized-but-unrepresentable semantic loss.

Read the companion architecture first:

- [`CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md`](CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md)

## Current status

- Deep research of Screenplay, Arc, CLI, JasperFx, Marten, Wolverine, CritterStackSamples, canonical IncidentService, and CritterStackHelpDesk is complete.
- The existing Arc architecture is confirmed: `Cratis.Arc.Screenplay` is a NuGet package owned by Arc; it accepts Roslyn compilations, analyzes Arc/Chronicle source, creates the Screenplay AST/text, verifies it, and is consumed by Cratis CLI, which owns `MSBuildWorkspace`.
- The final repository topology is decided and two public repositories have been created:
  - <https://github.com/Cratis/Screenplay.Generation>
  - <https://github.com/Cratis/Screenplay.CritterStack>
- Screenplay itself has been restored to language/compiler/editor-only state. No generation or Critter Stack source remains there.
- Shared generation contracts, resolver/lowerer, specs, and Roslyn SDK live in `/Volumes/sourcecode/repos/cratis/Screenplay.Generation`; PR #1 is merged and `main` is clean.
- Screenplay.Generation `v0.1.0` exists with all three verified nupkgs attached to the GitHub release. NuGet push is blocked only by missing trusted-publishing policies tracked in Screenplay.Generation issue #2.
- Critter Stack analysis, specs, research, this handover, and the fresh-session prompt live on `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack/main`; initial PR #2 is merged and `v0.1.0` exists with the verified nupkg attached to the release.
- Implemented locally so far:
  - typed facts/evidence/diagnostics;
  - deterministic resolution and placement precedence;
  - lowering for events, read models, reducers, commands, and queries;
  - Roslyn catalogs/source/type utilities;
  - Marten snapshot/single-stream discovery;
  - Wolverine HTTP/handler/aggregate classification, response-wrapper exclusion, direct document deletion, and delayed-message consequence diagnostics;
  - complete compilation-in/source-out Critter Stack generator façade;
  - 33 adapter/generator specs;
  - real BankAccountES smoke generation with compiling `.play` and no diagnostics;
  - real canonical IncidentService generation with 18 artifacts, one explicit delayed-delivery warning, and no fabricated `UpdatedAggregate` event.
- Arc PR #2594 is merged and `Cratis.Arc.Screenplay` 22.0.0 is published against Screenplay 4.2.1/reaction syntax. 1,262 Screenplay specs, all real Arc generation fixtures, and CI pass.
- CLI PR #84 adds `auto|arc|marten|critter-stack` provider routing. It is an intentional draft with all checks green. Local and CI validation covers 500 CLI specs, 164 Chronicle integration specs, a zero-warning Release build, real Arc generation, real BankAccountES generation, and real IncidentService generation.
- Remaining release-order blocker: configure nuget.org trusted-publishing policies, rerun the two failed package publish jobs, remove temporary release-asset restore bootstrap steps, then mark CLI PR #84 ready and merge.

Update this section whenever a stage lands.

## Non-negotiable decisions

### Implement now

- `Cratis.Screenplay` remains the language compiler/AST/printer repository and package.
- `Cratis.Screenplay.Generation` owns neutral contracts, resolution/lowering, verification, and the shared Roslyn SDK in its own repository/release line.
- `Cratis.CritterStack.Screenplay` owns Marten/Wolverine interpretation in its own repository/release line.
- Low-level adapters emit typed facts/evidence, never `ApplicationSyntax`.
- Each ecosystem package exposes a complete compilation-in/source-out generator façade, matching the existing `Cratis.Arc.Screenplay` package experience.
- One central resolver/lowerer/emitter verifies every generated document.
- Use Roslyn semantic symbols and fully qualified metadata names.
- Keep official .NET adapters in-process over one CLI-owned workspace for the MVP.
- Keep the existing Arc generator path operational and behaviorally unchanged.
- Add provider-aware CLI orchestration without merging multiple deployable hosts implicitly.
- Generate useful partial models with explicit diagnostics when current Screenplay cannot represent a construct.

### Architectural seam, not implementation yet

- out-of-process adapter hosts;
- plugin discovery;
- JSON-RPC or another public wire protocol;
- runtime application bootstrapping;
- runtime Marten/Wolverine descriptor enrichment;
- Arc public API migration to the new fact model;
- non-.NET adapter implementation.

The contracts must be serialization-friendly so these can be introduced without redesigning the semantics.

### Forbidden shortcuts

- Do not concatenate `.play` documents.
- Do not let adapters build AST nodes directly.
- Do not use simple names as merge keys.
- Do not use adapter execution order or last-wins conflict handling.
- Do not classify all Wolverine returns as events.
- Do not classify broker publication or local cascade as Screenplay `produces`.
- Do not infer mappings that the aggregate/projection did not implement.
- Do not load every executable in a solution into one application silently.
- Do not start application hosts or contact PostgreSQL by default.
- Do not move/type-forward the broad Arc public model surface during the MVP.
- Do not expand the Screenplay grammar before measuring actual loss from the first working generator.

## Target repository and package graph

```text
Cratis/Screenplay
  Cratis.Screenplay
    -> language/compiler/AST/printer/editor only

Cratis/Screenplay.Generation
  Cratis.Screenplay.Generation.Contracts
    -> no dependency on Roslyn, MSBuild, Arc, Marten, or Wolverine
  Cratis.Screenplay.Generation
    -> Contracts + Cratis.Screenplay
  Cratis.Screenplay.Generation.DotNet
    -> Contracts + Microsoft.CodeAnalysis

Cratis/Screenplay.CritterStack
  Cratis.CritterStack.Screenplay
    -> Contracts + Generation + Generation.DotNet
    -> metadata-name matching only; no Marten/Wolverine runtime package dependency
    -> exposes a complete generator façade like Cratis.Arc.Screenplay

Cratis/Arc
  Cratis.Arc.Screenplay
    -> existing complete Arc generator, unchanged during MVP

Cratis/cli
  -> owns MSBuildWorkspace, project/host selection, output, and generator package selection
```

The Critter Stack package uses the shared SDK internally but remains independently consumable. The CLI passes loaded compilations to the complete Critter Stack generator, exactly as it does for Arc today.

## Semantic pipeline

Keep four explicit layers:

```text
Adapter facts
  -> deterministic resolution/conflict handling
Resolved application graph
  -> Screenplay capability and loss analysis
Lowerable Screenplay model
  -> ApplicationSyntax
  -> ScreenplayPrinter
  -> ScreenplayCompiler
```

### Fact model

Start with the minimum typed facts needed by BankAccountES and IncidentService, but design additive records.

Required concepts:

- `AdapterIdentity`
- `SubjectId`
- `FactId`
- `SourceLocation`/`SourceRange`
- `EvidenceStrength`: `Exact`, `Configured`, `Conventional`, `Heuristic`
- provider/framework version
- explanation and related facts
- stable structured diagnostics

Initial fact categories:

- project/application host;
- type declaration and property shape;
- type role: command/message/event/document/aggregate/projected model/response/saga;
- handler and endpoint;
- handler input and parameter source;
- event append and stream start;
- document store/update/delete;
- aggregate read/write and identity/version source;
- projection/reducer registration and consumed event;
- query return/read;
- response, cascade, publish, side effect, and delayed delivery;
- module/feature/slice placement evidence;
- validation and authorization;
- explicit loss/ambiguity.

Use assembly/project identity plus fully qualified metadata name for .NET type subjects. Keep display names separate.

### Resolution

Resolution must be:

- deterministic;
- idempotent;
- independent of adapter/project enumeration order;
- exact-identity first;
- explicit-equivalence aware;
- conflict preserving;
- diagnostic producing when conflicts remain.

Marten+Wolverine augmentation occurs after both base discoveries so return values can be interpreted with aggregate/HTTP context.

## Implementation stages

## Stage 0: Align package baselines and freeze Arc behavior

### Goal

Prevent another syntax-tree binary mismatch before adding cross-repository packages.

### Work

1. Determine the current releasable Screenplay package version and API baseline.
2. Build Arc against the selected Screenplay 4.x package/source without changing Arc public contracts.
3. Update the CLI to consume matching Screenplay and Arc Screenplay packages.
4. Add/strengthen binary and real-package smoke coverage where missing.
5. Capture golden Arc `.play` output and diagnostics before refactoring shared internals.

### Compatibility constraints

- Positional syntax records cannot gain constructor parameters in a minor release.
- New AST capability uses additive `init` properties.
- `Cratis.Arc.Screenplay` public model records remain in the Arc assembly.
- Preserve the injected analyzer/emitter `ScreenplayGenerator` constructor.
- Preserve `ScreenplayGenerationResult.Model` type and legacy diagnostics.

### Gate

- Screenplay Debug and Release builds: zero errors; Release zero warnings.
- Screenplay specs pass.
- Arc Screenplay specs and real end-to-end fixtures pass.
- CLI Screenplay specs pass.
- Existing Arc generated bytes and diagnostics are equivalent.
- A consumer compiled against the previous Arc Screenplay public API runs without `MissingMethodException`.

## Stage 1: Neutral generation core

### Goal

Create the framework-neutral fact/resolution/lowering pipeline without changing Arc.

### Work

1. Add `Generation.Contracts` project and immutable documented records.
2. Add `Generation` project.
3. Implement deterministic fact collection and conflict reporting.
4. Define a resolved graph and lowerable model separate from raw facts.
5. Port/copy only generic naming, type conversion, AST construction, printer, and verification behavior needed by the new path.
6. Add canonical serialization or snapshot formatting for facts and diagnostics.
7. Add exhaustive specs for order independence, idempotence, conflicts, provenance, naming, printing, and verification.

### Gate

- Contracts has no framework/compiler dependency.
- Generation has no Roslyn/MSBuild/framework dependency.
- Randomly shuffled fact input produces byte-identical output.
- Duplicate facts are idempotent.
- Contradictory facts produce stable located diagnostics.
- Every emitted document compiles and reprints identically.

## Stage 2: Shared .NET source analysis

### Goal

Provide reusable Roslyn mechanics without making the generation core C#-specific.

### Work

1. Add `Generation.DotNet` project.
2. Implement compilation ordering and project metadata context.
3. Implement declared-type cataloging across nested types.
4. Implement semantic-model routing across project compilations.
5. Implement authored/generated-source classification.
6. Implement source-root and repository-relative file paths.
7. Implement type shape, nullability, collections, enums, records, and primitive mapping evidence.
8. Add metadata-name/symbol helpers and bounded value-flow helpers for object creation, local variables, arrays/collections, and simple conditionals.
9. Add tests using in-memory C# compilations and referenced projects.

### Gate

- No MSBuildWorkspace dependency in this package.
- Generated sources never duplicate authored artifacts.
- Multi-project symbols and file paths resolve deterministically.
- Unsupported generic/type shapes produce diagnostics rather than crashes.

## Stage 3: Marten foundation using BankAccountES

### Goal

Generate useful Screenplay facts for Marten event sourcing and documents before Wolverine contextual interpretation.

### Discovery readers

- Marten configuration and projection registration.
- Document roles and identity.
- Event evidence.
- Stream start and append operations.
- Self-aggregating and explicit aggregate projections.
- `SingleStreamProjection`, `MultiStreamProjection`, and `EventProjection` classification.
- Projection lifecycle as evidence/loss metadata.
- Query/document operations.
- Generated evolver/manifest corroboration only.

### BankAccountES gate

- Correct events: account/client opening and deposit/withdrawal facts.
- Correct snapshots/read models and reducer event lists.
- Correct stream identities.
- No command inferred from storage calls outside an entry point.
- No projection mapping invented from matching property names.
- Direct document operations represented as facts and lowered with explicit loss warnings where needed.
- No application or PostgreSQL startup.
- Generated `.play` compiles and expected diagnostics are snapshot-tested.

## Stage 4: Wolverine + Marten augmentation using IncidentService

### Goal

Implement exact handler, HTTP, and aggregate-context consequence classification.

### Wolverine readers

- handler type/method discovery and ignores;
- lifecycle middleware methods;
- HTTP endpoint attributes and startup mappings;
- route/body/query/header/service/context parameter sources;
- ASP.NET authorization;
- FluentValidation/DataAnnotations enablement and rules;
- ordinary return/cascade/side-effect decomposition;
- saga classification;
- explicit publish/send/schedule calls.

### Integration augmentation

Normalize:

- current `[DeciderFunction]`, `[WriteModel]`, `[ReadModel]`, `EventsToAppend`;
- legacy `[AggregateHandler]`, `[WriteAggregate]`, `[ReadAggregate]`, `[Aggregate]`, `Events`;
- `IEventStream<T>` direct append behavior;
- `IStartStream` and `IMartenOp`;
- `CreationResponse`, `EmptyResponse`, `UpdatedAggregate`;
- `OutgoingMessages`;
- expected/exclusive version sources;
- route-only aggregate identities;
- delayed one-shot delivery.

### IncidentService gate

- `LogIncident` starts the stream and produces only `IncidentLogged`; the response is not an event.
- `CategoriseIncident` appends `IncidentCategorised`, returns 204, and uses route identity/version correctly.
- `CloseIncident` appends `IncidentClosed`, returns updated aggregate metadata, and dispatches delayed `ArchiveIncident` as a message—not an event or recurring schedule.
- `ArchiveIncident` appends `Archived` and records document deletion loss exactly once.
- `GetIncident` is a query.
- `Incident` reducer lists only implemented `Apply`/`ShouldDelete` evidence.
- Internal/inactive sample methods and comments are excluded.
- Output compiles and is deterministic.

## Stage 5: CLI orchestration

### Goal

Expose the new generation without destabilizing current Arc generation.

### Work

1. Add `--provider auto|arc|marten|critter-stack`.
2. Keep Arc on `ArcScreenplayGeneration` initially.
3. Add Marten/Critter Stack adapter invocation after one shared compilation load.
4. Replace Arc-only solution filtering.
5. Direct `.csproj`: use it as host and include transitive project references.
6. Solution with one detected host: use its closure.
7. Multiple hosts: fail with a diagnostic listing candidates and require a project target.
8. Report target-framework choice; do not silently hide divergent TFMs.
9. Map severities explicitly.
10. Preserve stdout/file/error behavior and deterministic diagnostic ordering.
11. Add real installed-global-tool smoke tests.

### Gate

Run installed CLI from outside each repository against:

- an Arc application;
- BankAccountES;
- canonical IncidentService;
- a Marten-only target.

Verify clean stdout, expected stderr diagnostics, output bytes, and exit codes.

## Stage 6: Compatibility breadth

### Marten-only

Use `MartenWithProjectAspire`:

- async daemon;
- single/multi-stream/EventProjection;
- no invented commands;
- projection lifecycle retained as loss/provenance;
- project-role handling for Aspire host/domain/worker.

### Legacy Critter Stack

Use CritterStackHelpDesk:

- Marten 6/Wolverine 1 metadata names and aliases;
- API/worker/contracts project relationships;
- explicit treatment of demo/test projects;
- multiple handlers/entry points for one message;
- event forwarding;
- stream type distinct from projected decision state;
- checked-in generated source exclusion;
- Rabbit exchange versus worker queue;
- negative assertions for absent auth/tenancy/saga/scheduling/upcasts.

### Additional samples

- CqrsMinimalApi: document CRUD.
- OutboxDemo: saga/outbox.
- BookingMonolith: multiple entity loads.
- Reports: `IMartenOp` and custom identity.
- Fleet service: transport, delay, projection side effects.
- ProjectManagement: manual Minimal API versus Wolverine HTTP.

## Stage 7: Measure language loss and evolve Screenplay

Do not begin until Stage 5 produces working documents and a measured diagnostic inventory.

Candidate language additions, in likely value order:

1. direct document state operations;
2. publish/cascade distinct from event append;
3. command-level outgoing and delayed messages;
4. HTTP exposure metadata;
5. projection lifecycle/version;
6. stream identity and expected/exclusive version;
7. multi-stream grouping;
8. saga/workflow metadata;
9. event aliases/upcasts;
10. tenancy/subscription/daemon metadata.

Every AST extension must use binary-compatible additive properties unless a deliberate major release is chosen.

## Stage 8: Future language adapter protocol

Implement only when a non-.NET adapter exists or dependency isolation demonstrably requires it.

Protocol requirements:

- request/fact/result contracts derived from the proven in-process model;
- independently versioned protocol, IR schema, adapter, and target-language versions;
- framed protocol-only stdout;
- bounded stderr;
- timeout/cancellation/process cleanup;
- malformed/oversized response handling;
- explicit adapter trust/allowlist;
- no workspace-provided adapter auto-execution;
- cross-platform distribution tests;
- compatibility tests across protocol versions.

## Diagnostics strategy

Keep categories separate:

- `CLIxxxx`: target/workspace/output concerns;
- existing `SPxxxx`: Arc generator compatibility;
- `GENxxxx`: shared merge/lowering/verification;
- `DOTNETxxxx`: C#/compilation/source concerns;
- `MARTENxxxx`: Marten ambiguity/loss;
- `WOLVERINExxxx`: Wolverine ambiguity/loss;
- `CRITTERxxxx`: integration-context ambiguity/loss.

Before committing exact prefixes, verify repository conventions and avoid collisions. Never reuse retired codes.

Every diagnostic needs:

- stable code;
- severity;
- source location where available;
- affected fact/artifact;
- what was omitted or approximated;
- actionable next step or override when possible.

## Canonical test matrix

| Fixture                 | Purpose                                                               |
| ----------------------- | --------------------------------------------------------------------- |
| Existing Arc fixtures   | byte-for-byte and diagnostic compatibility                            |
| BankAccountES           | event-sourced commands, snapshots, validation                         |
| IncidentService         | current aggregate/HTTP wrappers, route identity, delay, direct delete |
| MartenWithProjectAspire | Marten-only async/multi-stream/EventProjection                        |
| CritterStackHelpDesk    | legacy APIs and multi-project API/worker/contracts                    |
| CqrsMinimalApi          | direct documents                                                      |
| OutboxDemo              | saga/outbox                                                           |
| BookingMonolith         | multiple entity loading                                               |
| Reports                 | `IMartenOp`, generated/custom identity                                |
| Fleet service           | broker topology, delayed messages, projection side effects            |
| ProjectManagement       | manual Minimal API negative distinction                               |

For every fixture assert:

- generated document compiles;
- print/compile/print stability;
- deterministic bytes;
- expected artifacts and relationships;
- expected loss diagnostics;
- no runtime/database startup;
- no generated-source duplication;
- no fabricated absent behavior.

## Security and trust checklist

- Inspect diffs for credentials, connection strings, user data, tokens, generated secrets, and private endpoints before every commit.
- Never include local `.pi/`, build outputs, package caches, or generated application artifacts.
- Treat MSBuild project evaluation as code execution.
- Do not execute repository-provided adapter binaries automatically.
- Do not enable runtime enrichment without explicit consent.
- Keep environment variables and credentials out of diagnostics/provenance.
- Normalize and constrain output paths.
- Use metadata-name analysis rather than loading framework assemblies where possible.
- Review every dependency/manifest change explicitly.

## Cross-repository release strategy

Ship coherent, dependency-ordered PRs:

1. Screenplay generation packages and specs.
2. Arc package alignment/compatibility changes, only if required.
3. CLI provider orchestration after packages are published or source-linked safely.
4. Screenplay grammar changes as separate later PRs based on measured losses.

Use semantic labels accurately:

- generation packages: `minor`;
- compatible CLI provider addition: `minor`;
- internal compatibility/test-only work: `no-release` where appropriate;
- syntax breaking changes: `major` only by deliberate decision.

Do not merge a downstream PR until its upstream package is available to CI, unless the repository uses a verified temporary source/project-reference strategy that will not ship.

## Definition of done

The first complete delivery is done when:

- existing Arc generation remains green and compatible;
- `cratis screenplay generate --provider marten` works for a Marten-only app;
- `--provider critter-stack` works for BankAccountES and IncidentService;
- auto detection is conservative and deterministic;
- every output compiles and reprints identically;
- canonical loss diagnostics are stable;
- no application/database startup occurs;
- package/global-tool distribution works on CI platforms;
- docs explain capabilities, limitations, provider selection, and trust;
- PRs are reviewed by CI, merged, released where appropriate, and branches/issues cleaned up.

## Open decisions to resolve from implementation evidence

These are intentionally deferred:

- exact shape/names of all fact union records;
- whether ordinary Marten documents lower to `readmodel` or remain diagnostics-only before grammar support;
- how much simple data flow to support in v1;
- exact module/feature heuristics beyond project/host and source paths;
- exact provider diagnostic prefixes;
- first grammar additions after loss measurement;
- timing and transport for a non-.NET adapter protocol.

Resolve them with canonical fixtures and documented evidence, not taste.
