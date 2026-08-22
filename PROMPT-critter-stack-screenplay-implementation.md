<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Fresh-session prompt: implement source-to-Screenplay adapters

Copy everything below this line into a fresh Pi session started in `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack`.

---

You are taking over the autonomous implementation of source-derived Screenplay generation for Marten and Marten + Wolverine applications.

You have authority to inspect and modify the required Cratis repositories, use `gh` and `git`, create branches/worktrees, update dependency manifests where the implementation requires it, run builds/specs, make logical commits, push branches, create reviewed PRs with release notes and semantic labels, monitor CI, fix failures, merge green PRs, close resolved issues, and clean up branches. Never commit credentials, tokens, secrets, private user data, local agent artifacts, generated caches, or anything harmful to Cratis.

Work autonomously. Make best-effort decisions from repository evidence and the purpose of Screenplay. Ask only when a decision is truly impossible to make safely from source, tests, documentation, or canonical samples.

## Read first

Read these files completely before editing:

1. `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack/AGENTS.md`
2. `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack/.ai/rules/framework.md`
3. `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack/CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md`
4. `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack/CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md`
5. `/Volumes/sourcecode/repos/cratis/Screenplay.CritterStack/IMPLEMENTATION_STATUS.md`
6. `/Volumes/sourcecode/repos/cratis/Screenplay.Generation/IMPLEMENTATION_STATUS.md`
7. Relevant C#/spec/commit/PR rules and the `ship-changes` skill.
8. Each repository's own `AGENTS.md` and `.agents/PROJECT.md` before changing that repository.

Treat the two Critter Stack documents as the architectural baseline. Update the handover's current-status section as work lands.

## Repositories and research baselines

The following clones should exist under `/Volumes/sourcecode/repos`:

- `cratis/Screenplay`
- `cratis/Screenplay.Generation`
- `cratis/Screenplay.CritterStack`
- `cratis/Arc`
- `cratis/cli`
- `JasperFx`
- `Marten`
- `Wolverine`
- `CritterStackHelpDesk`

Additional corpus:

- `~/CritterStackSamples`
- `/Volumes/sourcecode/repos/Wolverine/src/Samples/IncidentService`

Research baselines were:

- Screenplay `a47cb2dd5f664f2aae351cd0986b8674475326e4`
- Arc `1e4750ff5784d77a15330e21ed2b0d49e188116a`
- JasperFx `da9fd17d69df5ff41940800bfb34ad4d88a88391`
- Marten `a483b09f881f1576152aa42a27b37cc17fab252f`
- Wolverine `af4807b5fb225ce7535c67785b74007fdad2dd9f`
- CritterStackHelpDesk `b67659dd7ca6d8ff07e7b9dad20affc4a37b6062`

Check current branches/statuses before acting. Never overwrite unrelated working-tree changes.

## Mission

Implement a source-first adapter architecture that can generate deterministic, valid `.play` definitions from:

1. Marten applications;
2. Marten + Wolverine applications;
3. current and legacy Critter Stack conventions;
4. future systems/frameworks/languages through a stable semantic seam.

Preserve existing Arc generation behavior and compatibility.

## Locked architecture

Use this pipeline:

```text
adapter facts
  -> resolved application graph
  -> lowerable Screenplay model
  -> Cratis.Screenplay AST
  -> canonical printer
  -> Screenplay compiler verification
```

Low-level adapters emit facts/evidence, never AST nodes. Ecosystem packages expose a complete compilation-in/source-out generator façade that composes the shared resolver/lowerer internally, matching the existing `Cratis.Arc.Screenplay` package.

Repository/package ownership for the MVP:

```text
Cratis/Screenplay
  Cratis.Screenplay

Cratis/Screenplay.Generation
  Cratis.Screenplay.Generation.Contracts
  Cratis.Screenplay.Generation
  Cratis.Screenplay.Generation.DotNet

Cratis/Screenplay.CritterStack
  Cratis.CritterStack.Screenplay

Cratis/Arc
  Cratis.Arc.Screenplay

Cratis/cli
  workspace loading and generator selection
```

Keep `Cratis.Screenplay` as compiler/AST/printer/editor only.

Keep the existing `Cratis.Arc.Screenplay` public API and generator path unchanged during the MVP. Do not move or type-forward its broad public model surface.

The CLI continues owning `MSBuildWorkspace` initially. Do not implement an adapter-host executable, plugin discovery, JSON-RPC, runtime app startup, or a public out-of-process protocol yet. Make fact contracts serialization-friendly so these can be added when a real non-.NET adapter exists.

## Hard semantic rules

- Use Roslyn semantic symbols and metadata names, not textual method-name guesses where binding is available.
- Use stable assembly/project + fully qualified symbol identities; short names are never merge keys.
- Preserve evidence strength and source provenance.
- Resolution is deterministic, idempotent, order-independent, and conflict preserving.
- Never let adapter order decide a conflict.
- Never classify an ordinary Wolverine cascade or broker publish as an appended event.
- Interpret HTTP response wrappers and Marten aggregate context before classifying returns.
- When a handler takes `IEventStream<T>`, direct appends are stream events; unrelated return values retain ordinary cascade semantics.
- Do not infer aggregate/projection mappings absent from implemented `Apply`/projection source.
- Marten alone does not define commands; require entry-point evidence.
- Exclude generated code as primary evidence; use it only for corroboration.
- Do not start hosts or connect to PostgreSQL by default.
- Emit explicit stable diagnostics for recognized but omitted/approximated behavior.
- Every generated document must compile and pass print/compile/print stability.

## Current checkpoint — do not repeat completed work

- Screenplay.Generation PR #1 and Critter Stack PR #2 are merged; both repositories are tagged `v0.1.0`, build cleanly, and have verified nupkgs attached to their GitHub releases.
- Nuget.org publication is blocked only by two UI-created owner-scoped trusted-publishing policies assigned to `@einari`: Generation issue #2 and Critter Stack issue #1. NuGet has no policy-management API/CLI.
- Arc PR #2594 is merged and Arc 22.0.0 is published against Screenplay 4.2.1.
- CLI PR #84 is merged and released as `v2.11.0`. It uses a discoverable allowlisted provider registry, net7/net9 framework-reference repair, authored-source compilation rejection (`CLI0008`), multi-host rejection (`CLI0009`), and explicit no-match/multiple-provider diagnostics (`CLI0010`/`CLI0011`).
- Final CLI evidence before merge: all four PR checks passed; 568 CLI specs passed locally; Release built with zero warnings/errors; BankAccountES and IncidentService auto generation passed.
- CLI `v2.11.0` publishing succeeded for NuGet, GitHub assets, and Homebrew. The Homebrew-installed tool generated and validated Arc, BankAccountES, and IncidentService from `/tmp`; output was 74/160/133 lines. Generation diagnostics were 2 warnings/2 info, 1 warning/9 info, and 4 warnings/6 info respectively; IncidentService validation retained 7 known undeclared-type warnings while the other two had none.
- CLI PR #88 is merged and released as `v2.12.0`. It owns selected-target resolved package provenance, assembly/capability corroboration, and separate support-tier, recognition, semantic-conformance, and lowering-fidelity reporting. `CLI0012`/`CLI0013` fail closed outside admitted framework generations; `CLI0014` reports unsupported provider options. Verification passed 623 specs, a zero-warning/error Release build, all four PR checks, sentinel packing, and the NuGet/native/GitHub/Homebrew release workflow. Because adapter publication remains blocked, CLI still bundles 0.1.0 and caps exact current package sets at `SourceReviewed`; update it to 0.3.0 after publication.
- Pinned canonical verification is merged in Critter Stack PR #11; issue #5 is closed.
- Critter Stack PR #14 is merged. It adds ordinary Marten document facts/relationships, `MARTEN0003`, CqrsMinimalApi/Reports canonical checks, and the first bounded delivery toward Marten issue #3.
- Critter Stack PR #18 is merged and released as `v0.3.0`. Marten projection discovery recognizes generic, instance, snapshot, and live registrations, including inherited JasperFx `Add(...)` APIs, preserves configured evidence, and reports unsupported async/live lifecycle semantics as `MARTEN0004`. Pinned MartenWithProjectAspire verification covers single-stream, multi-stream, and event projections.
- Critter Stack PR #21 is merged and released as `v0.4.0`. Wolverine analysis recognizes current/legacy handler, ignore, return, response, and exact event-stream metadata; excludes ignored/open-generic/abstract handlers; classifies return slots before emitting facts; and preserves strict response/event/wrapper/cascade/`OutgoingMessages`/side-effect separation. Verification passed 72 specs, zero-warning/error builds, sentinel packing, all PR checks, and all six canonical fixtures.
- Public adapter strategy is committed in `STRATEGY.md`.
- Pattern-discovery research and a compatibility reference are committed. They define evidence-based State Change/State View/Automation/Translation classification, exact canonical package sets, source baselines, and support tiers; CLI `v2.12.0` implements the runtime package-provenance report.
- Critter Stack `v0.4.0` publish run `32565807453` passed release/restore/build/pack and failed only at NuGet OIDC login because the owner-scoped trusted-publishing policy is still absent.

## Exact resume sequence

1. Check whether Generation issue #2 and Critter Stack issue #1 are resolved. If policies now exist, rerun Generation publish run `32435010554` and Critter Stack runs `32437573817` (baseline 0.1.0), `32540542422` (0.3.0), and `32565807453` (current 0.4.0), verify the package IDs on nuget.org, close the issues, and remove release-asset bootstrap restore steps from Generation/Critter Stack/CLI workflows.
2. Enable package validation after publication (Generation #3, Critter Stack #7).
3. After adapter publication, update CLI from the temporary 0.1.0 bootstrap to current 0.4.0 and confirm exact package sets promote from `SourceReviewed` to `Canonical`.
4. Continue the remaining source-profile gaps from `CRITTER_STACK_PATTERN_DISCOVERY_RESEARCH.md`: configured handler discovery/activation policies, direct bus send/publish/request-reply, delivery options, transport topology, sagas, DCB, subscriptions/forwarding, and projection side effects. Preserve the slot-level consequence separation landed in v0.4.0.
5. Continue remaining Marten completeness (#3) or Wolverine completeness (#4) against the pinned canonical workflow.
6. Design language additions only from measured diagnostics in Cratis/Screenplay#128.

The historical stages below explain architecture and acceptance criteria; most foundation stages are already delivered.

## Practical execution history

### 0. Finish and release the shared SDK

Completed except nuget.org trusted-publishing policy bootstrap.

### 1. Neutral generation core

Add immutable documented fact/evidence/diagnostic contracts, deterministic resolution, lowerable model, AST emitter, printer, and verifier.

Specs must prove shuffled-input determinism, duplicate idempotence, conflict diagnostics, provenance, valid output, and round-trip stability.

### 2. .NET analysis utilities

Add compilation/project context, symbol catalog, semantic-model routing, generated-source handling, source roots, type shapes, nullability, and bounded value flow. Do not put `MSBuildWorkspace` in this package.

### 3. Marten foundation

Use `~/CritterStackSamples/BankAccountES` first. Discover documents, identity, markerless events, stream starts/appends, snapshots, aggregate methods, projections, direct operations, and queries.

Do not fabricate commands from storage calls without entry-point evidence.

### 4. Wolverine + Marten context

Use canonical `/Volumes/sourcecode/repos/Wolverine/src/Samples/IncidentService`.

Support exact handler discovery, HTTP binding, route-only identity, optimistic version, `CreationResponse`, `EmptyResponse`, `UpdatedAggregate`, `Events`, `EventsToAppend`, `OutgoingMessages`, `IStartStream`, `IMartenOp`, direct appends/deletes, delayed dispatch, state validation, and query classification.

### 5. CLI

Add `--provider auto|arc|marten|critter-stack`.

- Keep Arc on its existing path.
- Load one workspace.
- Direct project target includes its transitive project-reference closure.
- One host in a solution may be selected automatically.
- Multiple hosts must produce a diagnostic and require an explicit project target.
- Never silently merge deployable hosts.
- Preserve stdout/file/error behavior.
- Replace integer severity casts with explicit mapping.

Install and test the actual global tool from outside fixture repositories.

### 6. Broaden compatibility

Add:

- `MartenWithProjectAspire` for Marten-only async/multi-stream/EventProjection;
- `CritterStackHelpDesk` for Marten 6/Wolverine 1 and API/worker/contracts;
- CqrsMinimalApi, OutboxDemo, BookingMonolith, Reports, one Fleet service, and ProjectManagement for their documented edge cases.

### 7. Measure language gaps

Do not expand the grammar speculatively. First inventory actual loss diagnostics. Then design the smallest high-value language additions, likely direct document operations, publish/cascade semantics, command outgoing messages, HTTP metadata, and projection lifecycle.

Any AST capability added within the current major must use binary-compatible additive properties, not positional record constructor parameters.

## Canonical acceptance assertions

### BankAccountES

- event returns under aggregate workflow become persisted events;
- HTTP results do not;
- aggregate/read state and identity are correct;
- snapshots/reducers list only actual consumed events;
- enabled validation is represented;
- no database startup occurs.

### IncidentService

- Log produces `IncidentLogged`, with generated stream identity and 201 response kept separate;
- Categorise uses route identity/version, appends `IncidentCategorised`, and returns 204;
- Close appends `IncidentClosed`, returns updated aggregate, and delays `ArchiveIncident` as a message;
- Archive appends `Archived` and records document deletion loss once;
- Get is a query;
- internal/inactive methods and comments do not become artifacts.

### CritterStackHelpDesk

- markerless contracts link across API and worker projects;
- demos/tests are excluded or explicitly resolved;
- one message may have multiple handlers/entry points;
- stream type and projected decision state may differ;
- event forwarding, local cascade, explicit publish, Rabbit transport, and side effect remain distinct;
- generated sources do not duplicate artifacts;
- absent auth/tenancy/saga/scheduling/upcast behavior is not invented.

## Verification discipline

For every logical unit:

1. run proactive LSP diagnostics;
2. build Debug and Release with zero errors and Release zero warnings;
3. run affected specs;
4. run project-wide diagnostics for edited files;
5. inspect the full diff;
6. commit only buildable coherent work.

For public APIs, add/update documentation and release notes.

Before PR merge:

- search for a real related issue;
- use the repository PR template;
- choose the correct `minor`/`patch`/`major`/`no-release` label;
- monitor CI to green;
- merge only green, close fully resolved issues explicitly, verify closure, and clean branches.

## Security

Before every commit and PR:

- scan the diff for credentials, tokens, private endpoints, connection strings, user data, generated secrets, local paths, `.pi/`, caches, and build outputs;
- treat MSBuild evaluation as execution of repository-controlled code;
- never emit environment values into facts/diagnostics;
- constrain output paths;
- do not auto-run repository-provided adapter binaries;
- review every dependency manifest change explicitly.

## Start now

Read the durable status files, inspect the two trusted-publishing issues, then follow the exact resume sequence above. Do not recreate repositories or redo Stages 0–5. Implement, specify, verify, commit, and ship the next unblocked dependency-ordered unit while keeping this handover current.
