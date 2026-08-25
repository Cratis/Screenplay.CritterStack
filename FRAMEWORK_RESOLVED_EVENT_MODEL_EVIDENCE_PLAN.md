<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Framework-resolved event-model evidence plan

## Progress tracker

This table is the durable status index. A status may be changed only with a reviewable evidence link, immutable hash, or issue/PR reference. A planned gate is not evidence that the gate has run.

| Workstream | Status | Owner | Evidence |
| --- | --- | --- | --- |
| JasperFx/Wolverine event-model surface research | Provisional / in progress | Screenplay.CritterStack | This plan and `/Users/sindrewilting/CritterStackFanMadeOpsBoard` are local research only; durable public links or checked-in research evidence: _pending_ |
| Disposable net10 WolverineFx 6.30.0/JasperFx 2.55.0 characterization | Provisional / in progress | Research evidence | Locally observed warning-free build, two byte-identical captures, SHA-256 `7fa4248050375c6cfcf308a3db7df86a10a5b96a25aee7d149176b5fc253911d`, and no `IHostedService.StartAsync`; source, transcript, raw bytes, exact SDK/runtime/configuration/serializer, package hashes, and NuGet signature evidence: _pending durable publication_ |
| Arc v22.3.0 Screenplay adoption | Complete | Arc | [merge/tag `d88636977de7daf83e29b02a8b7308911a34f730`](https://github.com/Cratis/Arc/commit/d88636977de7daf83e29b02a8b7308911a34f730); `Cratis.Arc.Screenplay` SHA-256 `00a81a60ea75f61122ae0ecc92d19b4c78413f1b468f4286c64a3d48cbf266cd` |
| Generation 0.13 placement contract | Complete | Screenplay.Generation | Released 0.13 under [Generation #26](https://github.com/Cratis/Screenplay.Generation/issues/26); placement program A/B/C1 complete, C2/D remain downstream |
| Generation 0.13 CritterStack placement adoption | In progress | Screenplay.CritterStack | [CritterStack #57](https://github.com/Cratis/Screenplay.CritterStack/issues/57); [#44](https://github.com/Cratis/Screenplay.CritterStack/issues/44) is the separate atomic adapter/roster lane |
| Matching CLI placement adoption and release | Not started | CLI | [CLI #111](https://github.com/Cratis/cli/issues/111); [#95](https://github.com/Cratis/cli/issues/95) is the separate atomic adapter/roster lane |
| Repository-owned pinned 6.30.0/2.55.0 fixture, sidecar, and integration harness | Not started | Screenplay.CritterStack | [CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58); reproduction transcript, checked-in raw/normalized hashes, and CI run: _pending_ |
| Cratis-owned evidence envelope, exact compatibility profile, and strict parser | Not started | Screenplay.CritterStack | [CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58); characterized payload fingerprint: _pending_ |
| Cratis `evidence seal/verify` producer and reference CI workflow | Not started | Screenplay.CritterStack | [CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58); release, documentation, workflow, and attestation evidence: _pending before passive import_ |
| Comparison-only normalized-entry experiment | Not started | Screenplay.CritterStack | [CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58); agreement/conflict/unjoined/loss report and unchanged `.play` hashes: _pending_ |
| Granular resolved-evidence/provenance contracts | Not started | Screenplay.Generation | [Generation #38](https://github.com/Cratis/Screenplay.Generation/issues/38); #19/#23/#24 remain separate unless explicitly expanded |
| Application-owned CI artifact production adoption | Not started | Application CI owner | CritterStack #58 workflow adoption and immutable artifact evidence: _pending_ |
| CLI passive evidence import | Not started | CLI | [CLI #112](https://github.com/Cratis/cli/issues/112); release evidence: _pending_ |
| Production admission from a provenance-preserving exporter profile | Not started | Generation, Screenplay.CritterStack, CLI | [Generation #38](https://github.com/Cratis/Screenplay.Generation/issues/38), [CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58), and [CLI #112](https://github.com/Cratis/cli/issues/112); standard profile 1 remains permanently ineligible |
| Local evidence broker security phase | Not started | CLI/security review | [CLI #113](https://github.com/Cratis/cli/issues/113); implementation blocked pending approved threat model |

## Purpose and non-negotiable decisions

Screenplay alone is semantic authority. Authored source, generated source, framework-resolved metadata, and runtime observations can contribute evidence or proposals; none can redefine Screenplay semantics. The generated `.play` document is accepted only through the Screenplay model, compiler, canonical printer, and review process.

Roslyn is the source-evidence and identity-binding baseline: it supplies source claims and exact joins, but it is not a second semantic authority. The brittleness to address is CritterStack independently shadowing the **effective** Marten and Wolverine composition rules as those frameworks evolve. Official framework-resolved design-time metadata can corroborate or challenge Roslyn source evidence without replacing source analysis or Screenplay decisions.

Roslyn remains required for:

- authored-versus-generated distinction;
- exact source ranges and host-owned source identities;
- project-qualified CLR type and method identity;
- imperative append, send, publish, schedule, and persistence calls;
- Marten-specific configuration details not proven by the official descriptor;
- explicit unsupported/loss diagnostics;
- offline operation and legacy package support; and
- the separate explicitly selected source profile.

No implementation in this plan may:

- let framework evidence or Roslyn source evidence override a **cross-lane source-versus-resolved semantic conflict** by ordering or evidence strength; this prohibition does not change Generation's existing same-lane evidence-strength placement semantics;
- treat a short type name, display name, descriptor slice name, or array position as semantic identity;
- start an application automatically because evidence is missing or stale;
- add JasperFx, Wolverine, Marten, or `CritterWatch.SourceGeneration` as a runtime dependency of the core importer;
- load a CLR type named by JSON;
- mutate `.play` bytes during the initial experiment; or
- mix descriptor implementation into the current downstream adoption of the released Generation 0.13 placement lane or the separate atomic adapter/roster lane.

`CritterWatch.SourceGeneration` is explicitly **not** a dependency. Its public package identity and stability were not verified. Useful ideas from similarly named or fan-made source-generation code do not establish a supported public contract.

## Official evidence surface and execution boundary

The initial characterized producer set must be exactly:

- `WolverineFx` 6.30.0;
- `JasperFx` 2.55.0;
- `JasperFx.Events` 2.55.0; and
- `JasperFx.SourceGenerator` 2.55.0.

Version strings are insufficient. The profile must also pin the complete resolved package graph, exact `.nupkg` and loaded producer-assembly SHA-256 hashes, NuGet repository-signature verification evidence, and every package and registered source capable of contributing an `IEventModelDefinitionSource`. Any uncharacterized contributor rejects the profile.

The application entry point must opt into JasperFx command processing:

```csharp
using JasperFx;

var app = builder.Build();
return await app.RunJasperFxCommands(args);
```

The exact host shape and namespace qualification may differ. The prerequisite is that the selected entry point actually delegates arguments through the JasperFx `RunJasperFxCommands(args)` integration; a `using` directive or package reference alone proves nothing. The producer and harness verify command availability before capture. If the command is unavailable, acquisition emits a stable `EvidenceAcquisition.CommandUnavailable` diagnostic and produces no evidence artifact or `.play`; it never attempts another execution path.

That set provides the public metadata-mode command:

```shell
dotnet run \
  --project <project> \
  --framework <tfm> \
  --configuration <config> \
  --no-build \
  --no-restore \
  -- event-model --json <OUTPUT_FILE>
```

The command builds, composes, and compiles the Wolverine `HandlerGraph` in metadata mode without starting the host, transports, or persistence. This is safer than starting the application, but it is **not a sandbox**: application composition, module initializers, configuration callbacks, and other code used to construct the application still execute.

The JSON payload is written to `<OUTPUT_FILE>`. Standard output is status text and must never be parsed as the payload. The harness must read the file named on `--json` only after the process exits successfully.

### Observed disposable-fixture profile evidence

A disposable net10 application pinned to WolverineFx 6.30.0 plus JasperFx/JasperFx.Events 2.55.0 built with zero warnings. This invocation succeeded:

```shell
dotnet run \
  --project <fixture-project> \
  --framework net10.0 \
  --configuration <configuration> \
  --no-build \
  --no-restore \
  -- event-model --json <OUTPUT_FILE> --name Fixture
```

A registered `IHostedService.StartAsync` would throw if called; it was not called. Two runs produced byte-identical output files with SHA-256 `7fa4248050375c6cfcf308a3db7df86a10a5b96a25aee7d149176b5fc253911d`. Standard output contained status/logging text, standard error was empty, and the JSON payload existed only in the named output file. This proves hosted services did not start in this fixture; it does not make composition code a sandbox.

For this fixture and package tuple only, the exported `EventModelDescriptor` JSON had exactly these top-level members:

```text
name
slices
aggregates
hotspots
```

Each observed slice had these members:

```text
name
commandType
handlerType
emittedEvents
projectionTypes
readModelTypes
pattern
triggerKind
aggregateTypes
publishedMessages
externalSystems
hotspots
specifications
elements
edges
```

Property names were camelCase. Observed enum values were PascalCase strings, including `Command` and `MessageHandler`. An observed type descriptor had exactly `{ name, fullName, assemblyName }`. Slice `elements` and `edges` were computed redundancy, not independent semantic evidence.

A plain handler returning `OrderPlaced` appeared under `publishedMessages`, not `emittedEvents`, because the fixture had no event-sourced aggregate context. The importer must preserve that producer classification and must not infer an emitted event from the returned type's name.

The proof limits are narrower than the member names suggest. `EventModelDescriptor` exposes chain-level roles only: `handlerType` can identify only the first handler type, there is no handler method identity, output lists can describe the whole chain, and `readModelTypes` conflates loaded and produced types. `elements` and `edges` are rendering redundancy. Events listed for an aggregate support only `AggregateApplies`; they do not support broad aggregate-consumption semantics.

This inventory is provisional observed evidence, not a complete schema or universal contract for other applications, package versions, runtimes, or future 6.30.0 payloads. It does not prove the presence of `ServiceCapabilities`, `HandlerRelationshipDescriptor`, `SagaDescriptor`, or any other unobserved section. `ServiceCapabilities` remains a separate, started-host surface.

### Required pinned fixture matrix

Profile 1 is frozen only from a checked-in matrix that exercises every supported field, enum value, omission, nullability rule, and ordering rule across these fixtures:

- plain handler and multi-handler chain;
- event-sourced aggregate and DCB behavior, including applied events;
- HTTP endpoint;
- gRPC endpoint;
- external-system declarations;
- projections and read models, including cases that expose the loaded/produced ambiguity;
- specifications;
- hotspots;
- domain and trigger overlays;
- duplicate descriptors and same-name slices from multiple definition sources; and
- nullable fields, omitted fields, empty collections, and rejected unknown fields/enums.

The matrix records exact net10 SDK version, `dotnet --info` runtime/host details, TFM, configuration, RID, serializer assembly/version/options, source, command transcript, raw bytes, normalized bytes, package graph, `.nupkg`/assembly hashes, and repository signatures. Net9 is characterized as a separate matrix/profile and never inferred from net10 results. Until the matrix covers a payload shape, profile 1 stays explicitly narrow and rejects that shape rather than treating the disposable fixture as complete-schema evidence.

Evidence classes have different admission rules:

| Surface | Production requirement | Default effect on `.play` |
| --- | --- | --- |
| Authored/generated Roslyn source | Host-selected project and source-path policy | Source-evidence and identity-binding baseline |
| Standard `EventModelDescriptor` metadata-mode JSON | Exact characterized profile plus strict validation | Comparison-only permanently; no production semantic admission |
| Started-host `ServiceCapabilities` | Running application | Report-only |
| CritterWatch telemetry | Running application/agent | Report-only |
| Marten shard and projection runtime state | Running persistence | Report-only |
| Observed traffic and causation | Runtime observation | Report-only |

Started-host and observed surfaces never alter `.play` bytes by default. Promoting one requires a separate architecture decision; it is not implied by resolved-evidence admission.

## Three profiles and three independent rosters

### Profiles

1. **Source profile — separate explicit invocation.** Roslyn adapters produce the current model without resolved evidence. It is never selected implicitly after a compare/resolved-evidence failure.
2. **Resolved profile — additive and explicit.** The source profile plus required application-owned CI evidence. Missing, malformed, stale, rejected, or unavailable supplied evidence blocks before `.play` output. The standard JasperFx 6.30 assembled profile is comparison-only permanently; production admission requires a distinct provenance-preserving exporter profile.
3. **Observed profile — sidecar only.** Started-host capabilities, telemetry, shard state, and traffic enrich reports. They do not participate in generation or alter `.play` bytes.

### Rosters

Keep these registries separate in naming, code, configuration, reports, and public APIs:

- **source-adapter roster:** for example, the existing Vogen and CritterStack `IDotNetScreenplayAdapter` implementations;
- **evidence-format roster:** exact parsers/profiles such as `jasperfx-event-model/wolverinefx-6.30.0+jasperfx-2.55.0`; and
- **renderer-target roster:** Screenplay-to-target renderers, which are downstream of the semantic model and are not evidence providers.

An evidence profile is not a source adapter. A source adapter is not a renderer. Renderer availability must never admit evidence.

## End-to-end architecture

```text
application-owned build/CI
  -> build selected project/TFM/configuration
  -> verify JasperFx command-processing opt-in
  -> metadata-mode event-model command
  -> exact EventModelDescriptor bytes
  -> Cratis `evidence seal` using the released CritterStack schema/canonical serializer
  -> Cratis `evidence verify` in CI
  -> immutable payload + sidecar artifact

Cratis CLI (passive acquisition only after seal/verify and CI guidance release)
  -> select exact project/TFM/configuration and construct immutable host-expected acquisition context
  -> enforce safe path acquisition and read payload + sidecar bytes
  -> pass opaque bytes and expected context; do not duplicate sidecar parsing or canonicalization

Cratis.CritterStack.Screenplay
  -> solely parse, verify, and canonically serialize the sidecar
  -> select exact evidence-format profile
  -> strict UTF-8/JSON parse with resource limits
  -> preserve raw payload and semantic list order
  -> normalize only profile-declared unordered sets
  -> bind exact framework type identities to selected authored Roslyn symbols
  -> produce atomic claims, diagnostics, and comparison results

Cratis.Screenplay.Generation
  -> retain source and resolved variants with granular provenance
  -> expose cross-lane conflicts; never choose them by lane order or strength
  -> preserve existing same-lane evidence-strength placement semantics
  -> keep standard assembled profile entries comparison-only permanently
  -> admit claims only from a later reviewed provenance-preserving exporter profile
  -> lower through the one Screenplay semantic model

Screenplay compiler/canonical printer
  -> verified, reviewable `.play`

observed/runtime inputs
  -> provenance/report sidecar only
```

The core CritterStack importer receives bytes. It must not execute the producer, resolve assemblies, call `Assembly.Load`, instantiate framework types, use reflection on application assemblies, or access a database, broker, host, or CritterWatch.

## Application-owned CI production and artifact contract

### Required producer release and reference workflow

Before passive CLI import is implemented or application CI artifacts are called supported, Cratis must release and document `evidence seal` and `evidence verify`. The producer uses the same CritterStack-owned sidecar schema, strict verifier, and canonical serializer as the importer; there is one implementation, not a CLI reimplementation. The reference workflow must build the selected target, verify `RunJasperFxCommands(args)` availability, run the metadata command, seal exact payload bytes with host-expected context, verify the resulting artifact, and publish both files with retention/access guidance. Its no-sandbox warning and complete reproducibility inputs are part of the supported contract.

### Preferred v1 acquisition

The preferred acquisition is an artifact emitted and sealed by the application's own trusted CI after the application has already been built. This places execution of composition code inside the application's security boundary and makes the consumer passive.

The artifact contains two files:

```text
event-model.json
cratis-event-model-evidence.json
```

`event-model.json` is preserved byte-for-byte. `cratis-event-model-evidence.json` is a Cratis-owned sidecar. Packaging them in an archive is allowed, but archive extraction must use the same path and resource rules as direct files.

### Required sidecar fields

The first sidecar schema must carry at least:

```text
schemaId                         Cratis-owned envelope schema identifier
schemaVersion                    exact supported envelope version
profileId                        exact characterized evidence-format profile
producer                         command/tool identity
capturedAtUtc                    informational; never sufficient for freshness
sourceRepository                 non-secret repository identity when available
sourceRevision                   immutable commit/revision
sourceDirty                      whether the producing checkout was dirty and the exact policy
projectIdentity                  durable host-issued SourceContext project identity
projectDisplayPath               logical repository-relative display/acquisition path; never identity
projectFileSha256                exact selected project-file hash
projectGraphFingerprint          exact selected project/reference/build-input fingerprint
targetFramework                  exact TFM
configuration                    exact configuration
runtimeIdentifier                exact RID, including an explicit no-RID value
packages[]                       complete sorted resolved packages: ID, version, nupkg SHA-256, repository-signature result
packageGraphEdges[]              complete sorted dependency edges with target-specific resolution
packageGraphSha256               canonical complete selected-target package graph hash
assemblies[]                     complete producer/contributor name/version/MVID/file SHA-256 and package binding
contributors[]                   every package/source able to register IEventModelDefinitionSource, registration mechanism/order, and package/source hash; no per-source descriptor provenance implied
apiFingerprint                   observed producer API capability fingerprint
payloadFingerprint               observed payload shape/profile fingerprint
payloadSha256                    SHA-256 of exact event-model.json bytes
normalizedPayloadSha256          SHA-256 of profile-normalized payload
upstreamMerged                   always true for standard WolverineFx 6.30 assembled command output
sdkIdentity                      exact .NET SDK version and feature band
runtimeIdentity                  exact host/runtime identity
buildId                          CI/build identity
attestation                      reserved; absent in v1 unless characterized
signature                        reserved; absent in v1 unless characterized
```

The package graph is complete and canonicalized by ordinal package ID, exact version, content hash, and dependency edges. Exact `.nupkg` and assembly hashes plus the exact NuGet repository-signature verification outcome are required and profile-checked; versions do not suffice. The contributor roster must account for every package/source able to register `IEventModelDefinitionSource`; an unknown or uncharacterized contributor rejects the profile. A timestamp alone never makes evidence fresh.

A later signature/attestation revision must bind the exact payload hash, sidecar fields, project identity, TFM, configuration, source revision, and package graph. Signature support is additive only through a new characterized sidecar schema/profile.

### Freshness and identity

Passive import succeeds only when the evidence matches an immutable host-expected acquisition context created from the CLI-selected workspace target and passed to CritterStack. The context contains:

- durable host-issued `SourceContext` project identity, separate from the logical display/acquisition path;
- source revision and explicit dirty policy;
- exact TFM, configuration, and RID;
- selected project file and complete project-graph hashes;
- complete resolved package graph, package content hashes, repository-signature evidence, and contributor roster;
- API/profile fingerprint;
- exact SDK, runtime, serializer, and build identity; and
- raw payload hash expectation when supplied by the acquisition channel.

CritterStack alone compares these expected values with the parsed sidecar. CLI supplies expected values and safe acquired bytes; it does not parse, verify, or canonically serialize the sidecar.

A mismatch is stale or conflicting evidence, not a reason to regenerate it. Any supplied or required compare/resolved evidence failure blocks that invocation before generation and emits no `.play`. The source profile is available only through a separate explicit invocation. The CLI never starts the application.

## Exact wire-profile policy

The JasperFx wire is new and has no durable embedded schema version on which Cratis can rely. Therefore the initial profile is an allowlisted tuple, not a version range:

```text
profileId: jasperfx-event-model/wolverinefx-6.30.0+jasperfx-2.55.0+jasperfx.events-2.55.0+jasperfx.sourcegenerator-2.55.0/<net10-sdk-runtime-config-serializer-api-payload-fingerprint>
```

The disposable capture above is provisional research with raw SHA-256 `7fa4248050375c6cfcf308a3db7df86a10a5b96a25aee7d149176b5fc253911d`; it is not a durable API/payload-shape fingerprint or a complete-schema claim. Replace the profile placeholder only after the repository-owned pinned fixture matrix records every supported field/enum/null/omission/order case, the exact net10 SDK/runtime/configuration/RID/serializer, complete package and contributor graph, package/assembly hashes and repository signatures, and the documented fingerprint. Characterize net9 separately. Freeze DTOs and a Cratis-owned schema from those durable captured bytes, never from framework type names or this plan alone.

A profile defines:

- exact producer and contributor package IDs, versions, dependency graph, `.nupkg`/assembly hashes, repository signatures, and all `IEventModelDefinitionSource` registrations;
- exact SDK/runtime/configuration/RID/serializer identity;
- exact command shape, `RunJasperFxCommands(args)` prerequisite, and expected exit behavior;
- fixture-matrix-backed root/property inventory and required members;
- enum domains and handling policy;
- maximum document size, depth, string length, collection length, and claim count;
- semantic lists whose order must be preserved;
- collections proven to be unordered sets and their canonical keys;
- type-identity fields and whether they are sufficient for an exact join;
- computed redundancy fields, if present;
- optional fields that can be ignored safely;
- unconditional `upstreamMerged=true` for standard WolverineFx 6.30 assembled output, including weak-Name merge loss; and
- the normalization algorithm/version.

Unknown profile, required schema member, required enum value, or incompatible fingerprint fails the import atomically. Future Wolverine/JasperFx versions require explicit captured fixtures and characterized profiles. “Close enough” versions and hopeful parsing are forbidden.

## Strict parsing and normalization

### Parse before mapping

Use a strict UTF-8 reader over exact bytes. Do not deserialize directly into permissive framework DTOs. The parser must reject:

- invalid UTF-8, byte-order ambiguity not admitted by the profile, or trailing non-whitespace data;
- duplicate JSON properties at any object depth;
- multiple top-level values;
- documents exceeding profile size/depth/count/string limits;
- missing required members or wrong JSON token kinds;
- non-finite, overflowing, or otherwise invalid numeric values;
- unknown required enum values;
- duplicate identities with incompatible content;
- sidecar/payload identity conflicts;
- stale project, package, API, or payload fingerprints; and
- profile-declared impossible combinations.

Atomicity has two levels:

1. **Document atomicity.** Envelope, profile, normalization, and in-document structural violations reject the entire import with zero claims or comparison entries. In profile 1, incompatible duplicate payload identities always reject the document atomically. Hash, sidecar/context, contributor-roster, schema, enum, resource, and impossible-combination failures are in this level.
2. **Claim-granular comparison after a valid import.** Exact joins and cross-lane comparison can mark an individual entry unjoined or conflicted without dropping unrelated comparison entries. A zero/multiple symbol match is not a malformed document; it blocks only the relevant entry. No profile 1 entry can be admitted semantically.

Normalization completes before any entry escapes. This distinction prevents partial output from an invalid document without turning ordinary join limitations into whole-document loss.

### Ordering and canonicalization

- Preserve every semantic list in producer order.
- Canonicalize only collections proven by the captured profile to be unordered sets.
- Sort unordered sets by a documented ordinal stable key; reject colliding keys with incompatible values.
- Keep raw bytes, raw SHA-256, normalization version, and normalized SHA-256 distinct.
- Do not normalize type display names into identities.
- Do not sort claims merely to hide producer ordering drift; comparison rendering may group by stable identity while retaining source ordinals.

The disposable fixture's slices contained computed `elements` and `edges`. Do not turn them into independent semantic claims. Compare them only with relationships derivable from the primary slice fields as integrity redundancy. A disagreement is an in-document structural/integrity violation and rejects the profile 1 document atomically. Their absence in another payload creates no entry and no failure unless that exact characterized profile makes them required.

## Exact type and method binding

A framework type binds only through this compound key:

```text
durable host-issued SourceContext project identity
+ descriptor assemblyName that matches exactly one selected authored project output identity
+ full CLR metadata type name, including namespace, nesting, and generic arity
-> exactly one authored Roslyn INamedTypeSymbol
-> exactly one project-qualified SubjectId
```

The durable project identity is host-issued and independent of repository-relative display/acquisition paths. It scopes the join but never becomes a Screenplay semantic ID. If `assemblyName` matches zero or multiple selected authored project outputs, reject the relevant comparison entry as unjoined/conflicted; never guess from path or short name.

Rules:

- Short names and display names never establish identity.
- Descriptor slice names are weak display/merge suggestions only. They cannot establish type identity, slice identity, module/feature placement, stream ownership, or semantic role.
- Assembly-only metadata types and generated-only declarations cannot masquerade as authored symbols.
- Zero matches produces an unjoined claim/report entry.
- More than one match produces an identity conflict and marks only the relevant comparison entry conflicted/rejected.
- Profile 1 contains no handler method identity and emits no method-scoped relationship. A future profile may bind a method only when captured fields transform into the existing full .NET documentation method identity and match exactly one selected authored method.
- Framework evidence may corroborate authored/generated classification but cannot replace Roslyn's classification.

The existing project-aware `DotNetProjectCompilation`, durable host-issued source context, `DotNetMethodIdentity`, and project-qualified `SubjectId` remain the identity-binding baseline. They are not semantic authorities. The convenience single-compilation API cannot provide durable cross-project evidence identity and must not accept resolved evidence.

## Atomic claim vocabulary

The normalized layer is intentionally smaller than `EventModelDescriptor`. Profile 1 emits comparison/report entries, never a preassembled Screenplay slice or production-admissible semantic claim. Its complete initial vocabulary is:

| Entry | Subject/target | Exact profile 1 meaning |
| --- | --- | --- |
| `CommandType` | slice occurrence -> exact joined/unjoined type reference | The chain-level `commandType` value |
| `FirstHandlerType` | slice occurrence -> exact joined/unjoined type reference | The single `handlerType`, explicitly limited to the first handler type |
| `ChainEmits` | slice occurrence -> ordered event type reference | An `emittedEvents` entry for the whole chain; not attributed to a handler method |
| `ChainPublishes` | slice occurrence -> ordered message type reference | A `publishedMessages` entry for the whole chain; never promoted to persisted emission |
| `ListedAggregateType` | slice occurrence -> ordered aggregate type reference | A listed `aggregateTypes` value; no broad consumption inference |
| `ListedProjectionType` | slice occurrence -> ordered projection type reference | A listed `projectionTypes` value; no consumption/production inference |
| `AmbiguousReadModelReference` | slice occurrence -> ordered read-model type reference | A `readModelTypes` value whose loaded-versus-produced role is intentionally unresolved |
| `AggregateApplies` | exact aggregate reference -> exact applied event reference | Only the aggregate applied-event relationship directly represented by the characterized path |

Profile 1 explicitly does **not** emit `HandlerProduces`, method-level `Handles`/`HandlerHandles`, `HandlerLoads`, `ProjectionProduces`, or broad `AggregateConsumes`. It also does not infer projection consumption, stream ownership, placement, or persistence. Display labels, rendering `elements`/`edges`, and the upstream-merge limitation stay report metadata rather than relationship entries. Payload paths outside the characterized matrix reject the narrow profile unless declared safely ignorable by that exact profile.

The observed `emittedEvents` and `publishedMessages` collections are different evidence channels. In particular, the disposable fixture put a plain handler's returned `OrderPlaced` in `publishedMessages` because there was no event-sourced aggregate context. Normalization must retain `Publishes`/message-production evidence for that occurrence and must not promote it to an emitted/persisted event based on the CLR type name, application intent, or Roslyn return shape alone. Roslyn can still contribute independent imperative or Marten evidence; disagreement remains explicit.

Every normalized comparison entry carries:

```text
claimId                         deterministic identity from kind + exact subjects + discriminator
claimKind                       one profile-known comparison-entry kind
sourceSubjectId                 optional exact project-qualified SubjectId
targetSubjectId                 optional exact project-qualified SubjectId
relationshipDiscriminator       exact profile-known value
producerOrdinal                 original semantic-list position
rawJsonPointers[]               all primary payload locations supporting the claim
lane                            resolved
profileId                       exact evidence-format profile
producerPackageSet              exact versions plus nupkg/assembly hashes and signature results
packageGraphSha256              complete resolved graph hash
contributorRosterSha256          characterized IEventModelDefinitionSource contributor roster hash
rawPayloadSha256                exact payload hash
normalizedPayloadSha256         normalized hash
projectIdentity                 selected project
sourceRevision                  producing revision
apiFingerprint                  characterized API fingerprint
payloadFingerprint              characterized shape fingerprint
upstreamMerged                  true for every standard profile 1 entry
evidenceStrength                framework-resolved/configured category, not authority precedence
admissionState                  comparison-only/rejected/unjoined/conflicted
comparisonOutcome               agreement/conflict/source-only/resolved-only/unjoined/loss
sourceEvidenceIds[]             exact joined Roslyn evidence, when any
conflictSetId                   stable identity for incompatible variants
limitations[]                   stable codes, not free-text-only caveats
```

Exact duplicate entries from the same payload may collapse only when all semantic and provenance fields agree; retain every raw JSON pointer and ordinal. For profile 1, incompatible duplicate payload identities reject the whole document atomically rather than becoming claim-level conflicts.

## Upstream merge and conflict semantics

Standard WolverineFx 6.30 command output is already irreversibly assembled. JasperFx merges descriptor slices by weak `Name`, takes earlier-source-first scalar values, and unions list values. It does not preserve the source variant that supplied each resulting value. Therefore:

- set `upstreamMerged=true` unconditionally on the standard profile envelope and every comparison entry; a sidecar or normalized entry that says `false` rejects the document atomically;
- do not invent lost variants or treat a union member as attributable to a handler/source;
- keep the standard assembled profile comparison-only and permanently ineligible for production admission of slice relationships; and
- report weak-Name collisions, earlier-source scalar selection, union-list provenance loss, and contributor order as explicit limitations.

Production admission requires a new provenance-preserving per-source exporter, or equivalent upstream contract, that records for every `IEventModelDefinitionSource`: its `Subject`, registration index, source type, raw descriptor result, explicit null result or error, and the final assembled descriptor. That exporter gets a distinct characterized profile and review; standard command output cannot be upgraded in place.

After a document-valid import, cross-lane comparison follows these rules:

- Do not use payload order, adapter order, lane order, or evidence strength to pick a winner between source and resolved lanes.
- Equal entries from source and resolved lanes report agreement.
- Incompatible cross-lane entries report an explicit stable conflict set.
- Source-only and resolved-only entries remain visible.
- Unjoinable descriptor entries remain visible as unjoined evidence, never name-matched artifacts.
- An unjoined or conflicted entry does not drop unrelated comparison entries.

The no-winner rule is scoped to cross-lane source-versus-resolved semantic conflict. Generation's existing same-lane evidence-strength placement semantics remain unchanged. Evidence strength describes acquisition within its lane; it never resolves a cross-lane semantic conflict.

## Comparison-only experiment

The first experiment ends at a report. It must:

1. capture the actual pinned command output;
2. inventory and fingerprint the real payload before defining DTOs;
3. parse every captured fixture-matrix payload with the exact profile;
4. bind exact types to current authored Roslyn symbols; profile 1 has no method binding;
5. normalize only the supported comparison-entry vocabulary;
6. compare each entry with current Roslyn source evidence;
7. classify agreement, conflict, source-only, resolved-only, unjoined, and loss;
8. report unconditional `upstreamMerged=true`, weak-Name/earlier-source/union loss, and integrity redundancy results; and
9. prove generated facts and `.play` bytes are unchanged.

No comparison percentage can promote standard profile 1 to semantic admission. Production admission requires a separate provenance-preserving exporter profile, reviewed claim-by-claim precision, explicit loss behavior, and released Generation granular-claim/provenance contracts.

A useful report summary is:

```text
profile / package set / hashes / project identity
joined and unjoined type-reference counts
comparison-entry counts by kind
agreement / conflict / source-only / resolved-only / unjoined / loss
upstreamMerged: true / weak-Name, earlier-source-first, union-list loss
computed redundancy integrity result
admission mode: comparison-only
playBytesChanged: false
```

## Repository ownership and API sketches

### Screenplay

Sole semantic authority and owner of the language, parser/compiler, canonical source, and representability decisions. [Screenplay #148](https://github.com/Cratis/Screenplay/issues/148) owns render/recover fidelity only; it does not own descriptor placement or resolved-evidence provenance. Screenplay does not parse JasperFx JSON.

### Screenplay.Generation

Owner of neutral atomic facts, evidence variants, conflict sets, placement, granular provenance, deterministic lowering, and the admission contract. Generation 0.13 is already public; [#26](https://github.com/Cratis/Screenplay.Generation/issues/26) owns that released placement contract. Placement program A/B/C1 is complete; C2/D remains downstream adoption. [Generation #38](https://github.com/Cratis/Screenplay.Generation/issues/38) owns future resolved-evidence/granular provenance. [#19](https://github.com/Cratis/Screenplay.Generation/issues/19), [#23](https://github.com/Cratis/Screenplay.Generation/issues/23), and [#24](https://github.com/Cratis/Screenplay.Generation/issues/24) remain separate unless explicitly expanded.

Required future internal/public shape, subject to issue review:

```csharp
public sealed record GenerationClaimProvenance
{
    public required string Lane { get; init; }
    public required string EvidenceFormat { get; init; }
    public required string EvidenceId { get; init; }
    public required string PayloadSha256 { get; init; }
    public required bool UpstreamMerged { get; init; }
    public IReadOnlyList<string> SourcePointers { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
}

public sealed record GranularClaimContribution
{
    public required GenerationFact Claim { get; init; }
    public required GenerationClaimProvenance Provenance { get; init; }
    public required ClaimAdmissionState AdmissionState { get; init; }
}
```

Generation must permit one claim to be rejected/conflicted without discarding unrelated claims, while preserving atomic import failure for an invalid evidence document.

### Screenplay.CritterStack

Sole owner of the sidecar schema parser, verifier, canonical serializer, pinned evidence profiles, strict payload parser, DTOs created **after capture**, normalization, exact Roslyn join, comparison, diagnostics, fixture matrix, `evidence seal/verify` implementation, and the no-runtime-dependency invariant. The durable roadmap remains [CritterStack #29](https://github.com/Cratis/Screenplay.CritterStack/issues/29). [#57](https://github.com/Cratis/Screenplay.CritterStack/issues/57) owns current placement adoption, [#58](https://github.com/Cratis/Screenplay.CritterStack/issues/58) owns seal/verify/comparison evidence, and [#44](https://github.com/Cratis/Screenplay.CritterStack/issues/44) owns only the separate atomic adapter/roster lane.

Proposed public surface is byte-oriented, framework-free, and explicit about host expectations:

```csharp
public sealed record ResolvedEvidenceAcquisitionContext
{
    public required string SourceContextProjectIdentity { get; init; }
    public required string ProjectDisplayPath { get; init; }
    public required string SourceRevision { get; init; }
    public required bool SourceDirty { get; init; }
    public required SourceDirtyPolicy SourceDirtyPolicy { get; init; }
    public required string TargetFramework { get; init; }
    public required string Configuration { get; init; }
    public string? RuntimeIdentifier { get; init; }
    public required string ProjectFileSha256 { get; init; }
    public required string ProjectGraphSha256 { get; init; }
    public required string PackageGraphSha256 { get; init; }
    public required IReadOnlyList<ResolvedPackageEvidence> Packages { get; init; }
    public required IReadOnlyList<PackageGraphEdgeEvidence> PackageGraphEdges { get; init; }
    public required IReadOnlyList<EventModelSourceEvidence> Contributors { get; init; }
    public required string ApiProfileFingerprint { get; init; }
    public required string SdkIdentity { get; init; }
    public required string RuntimeIdentity { get; init; }
    public required string SerializerIdentity { get; init; }
    public required string BuildId { get; init; }
    public required string ExpectedPayloadSha256 { get; init; }
}

public sealed record ResolvedEventModelEvidenceInput
{
    public required ReadOnlyMemory<byte> Payload { get; init; }
    public required ReadOnlyMemory<byte> Sidecar { get; init; }
    public required DotNetAnalysisContext SourceContext { get; init; }
    public required ResolvedEvidenceAcquisitionContext ExpectedAcquisition { get; init; }
}

public interface IResolvedEventModelEvidenceCodec
{
    ReadOnlyMemory<byte> Seal(ResolvedEventModelSealInput input);
    ResolvedEventModelVerificationResult Verify(
        ReadOnlyMemory<byte> payload,
        ReadOnlyMemory<byte> sidecar,
        ResolvedEvidenceAcquisitionContext expected);
}

public interface IResolvedEventModelEvidenceImporter
{
    ResolvedEventModelImportResult Import(ResolvedEventModelEvidenceInput input);
}
```

Keep the exact wire DTOs, JSON pointers, fingerprint machinery, and profile dispatch internal until more than one characterized profile proves a stable abstraction. Prefer one additive options/input overload over changing existing generator constructors. The source-only public path remains byte-identical and dependency-compatible.

### Cratis CLI

Owner of workspace evaluation, exact project selection, safe passive file acquisition, construction of host-expected context, invocation of the CritterStack-owned seal/verify API, evidence policy, and machine reporting. CLI does not duplicate sidecar parsing, canonical serialization, signature verification, or profile logic. [CLI #111](https://github.com/Cratis/cli/issues/111) owns matching placement adoption, [#112](https://github.com/Cratis/cli/issues/112) owns passive import, [#113](https://github.com/Cratis/cli/issues/113) owns the optional broker threat model, and [#95](https://github.com/Cratis/cli/issues/95) owns only the separate atomic adapter/roster lane.

Proposed passive CLI shape, names subject to CLI review:

```text
cratis screenplay generate \
  --framework-evidence <event-model.json> \
  --framework-evidence-sidecar <cratis-event-model-evidence.json> \
  --framework-evidence-mode compare
```

No default search outside the selected workspace. No automatic producer execution. In compare/resolved mode, absent, stale, rejected, or unavailable evidence blocks with no `.play`. Machine output distinguishes accepted-for-comparison, rejected, conflicted, unjoined, and unsupported evidence; `admitted` is unavailable for the standard assembled profile.

### Application repository/CI

Owner of deciding whether composition code is safe to execute, building the exact target, verifying command opt-in, invoking the official command, running released Cratis `evidence seal/verify`, retaining the artifact, and controlling artifact access. Cratis publishes the producer and reference workflow before passive consumption is supported; it does not assume permission to execute arbitrary application composition.

### Observability/CritterWatch

Owner of started-host capabilities and observations. These are optional report-sidecar providers, not source adapters and not semantic authorities.

## Exact dependency and release order

Keep the placement and descriptor lanes separate. Do not parallelize releases across these dependency edges:

1. **Adopt released Generation 0.13 placement in CritterStack.** Generation 0.13 is already public under #26. Complete the downstream CritterStack placement issue/PR; do not put descriptor work in it. #44 remains atomic adapter/roster work only.
2. **Release matching CLI placement adoption under [CLI #111](https://github.com/Cratis/cli/issues/111).** #95 remains atomic adapter/roster work only. No descriptor code enters either placement release.
3. **Capture the pinned fixture matrix.** Commit source, transcript, exact raw output, hashes, package/signature evidence, field/enum inventory, SDK/runtime/configuration/serializer identity, and harness for every matrix category. This creates no production importer API.
4. **Characterize narrow profile 1.** Freeze the API/payload fingerprint, resource limits, ordering rules, enum domains, strict sidecar schema, unconditional upstream-merge limitation, and comparison-only vocabulary from durable captures. Net9 requires a distinct characterization.
5. **Implement and release CritterStack `evidence seal/verify`.** Make CritterStack the single schema/parser/verifier/canonical-serializer implementation and publish its reference application-CI workflow and no-sandbox/reproducibility guidance.
6. **Implement the CritterStack comparison importer.** Add framework-free parsing, normalization, exact Roslyn joining, diagnostics, and comparison reporting. Keep it internal or explicitly experimental. It cannot alter `.play` and the standard assembled profile can never be promoted.
7. **Review comparison evidence.** Run the mutation matrix, canonical byte-stability gates, independent precision review, and security review. Stop if identity or precision is insufficient.
8. **Implement and release CLI passive import.** Only after steps 5–7, CLI acquires bytes, constructs expected context, calls the released CritterStack verifier/importer, and blocks failed compare/resolved invocations with no `.play`.
9. **Design a provenance-preserving upstream exporter and the Generation contract under [Generation #38](https://github.com/Cratis/Screenplay.Generation/issues/38).** Release a post-0.13 contract for lanes, granular admission, provenance, conflicts, and unchanged source-only lowering, plus a distinct characterized exporter profile that preserves every source variant.
10. **Adopt the released admission contracts downstream.** Only the new provenance-preserving profile may enter a reviewed admission matrix. Publish Generation, then CritterStack, then CLI in that order; the standard assembled profile remains comparison-only.
11. **Consider a broker only as a separate phase.** Create and approve the broker-security issue before protocol or implementation work. It is never a prerequisite for passive import.

No repository-wide historical Wolverine/Marten pins change as part of steps 3–10.

## Isolated fixture and integration harness

Create a new tree later; do not place it under or modify `Integration/Canonical`:

```text
Integration/ResolvedEventModel/
  WolverineFx-6.30.0-JasperFx-2.55.0/
    README.md
    Directory.Build.props
    Directory.Packages.props
    Matrix/
      PlainHandler/
      EventSourcedAggregateAndDcb/
      Http/
      Grpc/
      ExternalSystems/
      ProjectionsAndReadModels/
      SpecificationsAndHotspots/
      DomainAndTriggerOverlay/
      DuplicateAndSameNameSources/
      NullableAndOmittedFields/
    Captured/
      <matrix-case>/event-model.raw.json
      <matrix-case>/cratis-event-model-evidence.json
      field-enum-order-inventory.json
      sdk-runtime-serializer.json
      package-graph-hashes-signatures.json
    Harness/
      Harness.csproj
      Program.cs
```

The fixture pins WolverineFx 6.30.0, JasperFx 2.55.0, JasperFx.Events 2.55.0, and JasperFx.SourceGenerator 2.55.0 locally, including exact package/assembly hashes and repository-signature evidence. It is not an eighth canonical source-to-`.play` fixture and must not change:

- `Integration/Canonical/BankAccountES.expected`;
- `Integration/Canonical/CqrsMinimalApi.expected`;
- `Integration/Canonical/Helpdesk.expected`;
- `Integration/Canonical/IncidentService.expected` or its source fixture;
- `Integration/Canonical/MartenWithProjectAspire.expected`;
- `Integration/Canonical/Reports.expected`;
- `Integration/Canonical/VogenConcepts.expected` or its source fixture; or
- repository-wide historical package pins in `Directory.Packages.props`.

### Harness behavior

The harness must:

1. require an already restored and built fixture and record the exact SDK/runtime/TFM/configuration/RID/serializer identity;
2. verify the selected entry point exposes the JasperFx command integration through `RunJasperFxCommands(args)`; do not infer this from a namespace import or package reference; emit `EvidenceAcquisition.CommandUnavailable` and stop if unavailable;
3. invoke the exact `dotnet run ... --no-build --no-restore -- event-model --json <temp-file> --name Fixture` command;
4. use an absolute, harness-owned temporary output path and a bounded timeout;
5. capture exit code, standard output status text, and standard error separately;
6. read JSON only from the output file after a zero exit code;
7. prove standard output is not treated as payload;
8. hash exact raw bytes before parsing;
9. parse and normalize through the candidate profile and verify the normalized hash;
10. run every fixture-matrix case twice in clean temporary directories and compare raw/normalized results according to the profile;
11. register an `IHostedService` whose `StartAsync` throws, proving host startup would fail if attempted and that metadata mode does not call it;
12. inventory every package/source that can register `IEventModelDefinitionSource`, its registration order, and package/source hash; reject uncharacterized contributors;
13. verify complete resolved package graph, exact `.nupkg`/assembly hashes, and NuGet repository signatures;
14. avoid all database, broker, transport, and CritterWatch packages/configuration;
15. fail if a forbidden network/persistence dependency appears in the fixture graph; and
16. modify checked-in captures only when passed an explicit `--write` flag.

Normal verification is read-only. `--write` must print old/new raw and normalized hashes and require a clean, exact package profile. CI never uses `--write`.

The throwing `IHostedService` proves only that hosted services do not start. It does not prove composition is sandboxed; module initializers and application configuration still execute.

## OpsBoard research-corpus boundary

`/Users/sindrewilting/CritterStackFanMadeOpsBoard` contains useful patterns for:

- `ServiceCapabilities` documents and hydration;
- HandlerGraph/declared-model ideas;
- inferred, observed, and confirmed provenance distinctions;
- observed causation and event-append edges; and
- separation of declared and runtime evidence.

It has source files but no `.csproj`, solution, or package manifests. It is research corpus, not an executable fixture or package-identity authority.

Do not copy these patterns from it:

- `PublishedTypes` -> `EmittedEvents` as a semantic shortcut;
- first-wins merging;
- private reflection into framework internals;
- portable-PDB paths or local source files as durable identity; or
- any source-generation dependency whose public identity/stability is unverified.

Roslyn source identity and the official public descriptor are the supported paths in this plan.

## Diagnostic families

Reserve the following conceptual families before allocating public numeric codes. Code allocation happens in the focused CritterStack/CLI issues and becomes stable only on release.

| Family | Examples | Default consequence |
| --- | --- | --- |
| `EvidenceEnvelope` | Invalid UTF-8, duplicate property, trailing data, malformed sidecar, hash mismatch, resource limit | Reject entire import |
| `EvidenceProfile` | Unknown profile, producer package mismatch, unknown required property/enum, API/payload fingerprint drift | Reject entire import |
| `EvidenceAcquisition` | Missing file, unsafe path, unavailable JasperFx command, stale project graph, TFM/configuration/RID mismatch, dirty/revision policy mismatch | Compare/resolved invocation blocks with no `.play`; source profile requires a separate invocation |
| `EvidenceIdentity` | Missing assembly/full type, zero/multiple selected-output or Roslyn joins, method identity unavailable, project conflict | Unjoin/conflict only the relevant comparison entry after a valid import; never short-name join |
| `EvidenceIntegrity` | Computed Elements/Edges disagree, incompatible duplicate payload identity, non-deterministic normalized hash | Profile 1 rejects the entire document atomically with zero entries |
| `EvidenceMerge` | `upstreamMerged`, source/resolved disagreement, lost per-source variants | Explicit limitation/conflict; never choose winner |
| `EvidenceLoss` | Descriptor field has no atomic claim or Screenplay representation | Stable report; no invented fact |
| `EvidenceAdmission` | Comparison-only entry, rejected entry, unsupported profile | Standard profile has no `.play` effect; only a future provenance-preserving profile can be reviewed for admission |
| `ObservedEvidence` | Started-host/telemetry/shard/traffic evidence supplied | Report-sidecar only |

CritterStack owns sidecar parse/verify/canonicalization/signature, payload parse/profile/identity/normalization diagnostics, and context comparison diagnostics. CLI owns safe acquisition, workspace selection, and construction of expected context. Generation owns cross-contribution conflict and admission diagnostics. Do not reuse `MARTEN####` or `WOLVERINE####` for envelope failures.

## Security rules

1. Passive import is the only v1 CLI acquisition mode.
2. Missing, rejected, or stale evidence never triggers `dotnet run`, application startup, restore, build, database access, broker access, or network discovery.
3. The application owns execution of composition code and the trust policy for its CI runner.
4. Treat payload and sidecar as untrusted bytes even when produced by trusted CI.
5. Require regular files beneath an approved workspace/artifact root; reject traversal, rooted archive members, symlink escapes, devices, and ambiguous case aliases.
6. Apply byte, depth, string, collection, and claim-count limits before allocation grows with attacker input.
7. Verify raw hash before semantic parsing and verify all identity/fingerprint fields before comparison; profile 1 has no semantic admission.
8. Never resolve or load a CLR type from a JSON string.
9. Never load a JasperFx/Wolverine/Marten assembly in the importer process.
10. Do not execute serializer callbacks, polymorphic type-name handling, `Type.GetType`, reflection activation, or dynamic code from evidence.
11. Do not log payload contents by default; diagnostics use bounded JSON pointers, hashes, and safe identities. Redact paths and environment-derived values.
12. Preserve exact bytes for audit while enforcing artifact access controls and retention policy.
13. Signatures/attestations are versioned, explicit, and fail closed; they do not make composition code safe.
14. Observed/runtime evidence is report-only and cannot promote itself to semantic admission.
15. A future broker requires a separate threat model, mutual authentication, authorization, replay protection, freshness/nonces, origin binding, rate/size limits, protocol versioning, audit logging, shutdown behavior, and security review before implementation.

## Deterministic verification gates

All gates below are planned until linked in the progress table. The disposable characterization is provisional local research; it does not complete the repository-owned fixture matrix, sidecar, harness, parser, comparison, package, or release gates.

### Provisional disposable-characterization observations

These observations remain in progress until source, transcript, raw bytes, package/assembly hashes, repository signatures, and exact SDK/runtime/configuration/RID/serializer identity are checked in or publicly immutable.

- A local net10 fixture built warning-free against WolverineFx 6.30.0 and JasperFx/JasperFx.Events 2.55.0.
- `event-model --json <file> --name Fixture` exited successfully.
- A throwing `IHostedService.StartAsync` was not called.
- Standard output was status/logging, standard error was empty, and payload bytes came from the output file.
- Two output-file captures were byte-identical at SHA-256 `7fa4248050375c6cfcf308a3db7df86a10a5b96a25aee7d149176b5fc253911d`.
- No normalized hash, repository fixture, sidecar, strict parser, or CI harness gate is claimed complete.

### Repository capture gates

- Every fixture-matrix category and every supported field/enum/null/omission/order case is captured; profile 1 rejects shapes outside the matrix.
- Exact net10 SDK/runtime/TFM/configuration/RID/serializer identity is recorded; net9 has a separate characterization.
- Complete producer/contributor tuple includes JasperFx.SourceGenerator 2.55.0, all `IEventModelDefinitionSource` registrations, full package graph, exact `.nupkg`/assembly hashes, and NuGet repository-signature evidence.
- The selected entry point exposes JasperFx command processing through `RunJasperFxCommands(args)`, and the command exits zero and writes the named file; unavailable command emits the acquisition diagnostic and no artifact.
- Standard output contains status only and is never parsed as payload.
- Throwing `IHostedService.StartAsync` is not called.
- No database, broker, transport listener, or CritterWatch dependency/configuration exists.
- Two clean captures meet documented raw/normalized determinism expectations.
- Checked-in capture changes only under `--write`.

### Parser/profile gates

- Strict rejection tests cover every envelope/profile rule.
- Unknown required schema/enum/profile and incompatible duplicate payload identities fail document-atomically with zero entries.
- Semantic list order survives parse/normalize/serialize/report.
- Only profile-declared sets canonicalize.
- Raw and normalized hashes are independently verified.
- Computed redundancy, if present, emits no claims and is checked for integrity only.

### Join/comparison gates

- Every joined profile 1 entry uses durable host project identity + descriptor `assemblyName` matching exactly one selected authored project output + full CLR metadata name, then exactly one authored Roslyn symbol.
- Durable host project identity remains separate from display path and never becomes a Screenplay semantic ID.
- Profile 1 emits no method-scoped entry.
- Short/slice/display-name collision fixtures never join.
- `upstreamMerged=true` is unconditional for standard command output and weak-Name/earlier-source/union loss survives every layer.
- Agreement, conflict, source-only, resolved-only, unjoined, and loss all have deterministic specs.
- Cross-lane conflicts remain conflicts under shuffled adapter/evidence input.
- Comparison mode produces byte-identical facts and `.play` to source-only mode.

### Repository/package gates

- All seven existing canonical fixtures and hashes remain unchanged.
- A separate explicit source-profile invocation preserves offline/legacy byte identity. Any supplied or required compare/resolved evidence failure blocks that invocation before generation and emits no `.play`.
- Debug specs, Release build, package validation, pack, and clean package consumer pass with zero warnings/errors.
- Public API comparison against the latest released `Cratis.CritterStack.Screenplay` baseline passes.
- Packed dependency graph contains no JasperFx, Wolverine, Marten, CritterWatch, or fixture-only package in the core importer dependency closure.
- CLI package/native/installed-tool gates run only in the downstream release phase.

Do not mark any gate complete without a fresh command result and immutable evidence link.

## Mutation matrix

| Mutation | Expected raw hash | Expected normalized hash | Expected result |
| --- | --- | --- | --- |
| Byte-identical input | Same | Same | Same claims/report |
| Insignificant object whitespace | Different | Same if profile normalization permits | Same claims; raw hash records difference |
| JSON object property order | Different | Same for object-member canonicalization | Same claims |
| Reorder profile-declared unordered package/type set | Different | Same | Same claims |
| Reorder semantic handler/event/projection list | Different | Different | Preserve new ordinal; report drift, never silently sort |
| Duplicate JSON property | Different | None | Reject atomically |
| Trailing second JSON value/data | Different | None | Reject atomically |
| Invalid UTF-8 | Different | None | Reject atomically |
| Exceed size/depth/string/count limit | Different | None | Reject atomically before claims |
| Unknown required root/member | Different | None | Reject profile atomically |
| Unknown required enum value | Different | None | Reject profile atomically |
| Unknown explicitly profile-ignorable optional member | Different | Profile-defined | Ignore only as characterized; retain drift evidence |
| Conflicting duplicate semantic identity | Different | None | Profile 1 always rejects the entire document atomically; never first-wins |
| Change descriptor display/slice name only | Different | Different | Weak display delta only; no identity/placement change |
| Short-name collision across projects/assemblies | Any | Any | No short-name join; unjoined/identity conflict |
| Change project identity, TFM, or configuration in sidecar | Payload may match | None | Reject as identity conflict/stale evidence |
| Change exact package set or package graph hash | Payload may match | None | Reject profile/freshness |
| Change API/payload fingerprint | Payload may match | None | Reject until a new characterized profile exists |
| Change computed `Elements`/`Edges` only, if present | Different | None | Integrity mismatch rejects the profile 1 document atomically with zero entries |
| Standard assembled output loses per-source variants | Any | Profile-defined | Set `upstreamMerged=true` unconditionally; comparison-only permanently |
| Remove variants from a provenance-preserving exporter profile | Different | None | Reject atomically; never downgrade to assembled profile |
| Remove evidence files | None | None | Compare/resolved invocation blocks with no `.play`; source profile is a separate explicit invocation; never start application |
| Corrupt signature in a future signed profile | Any | None | Reject compare/resolved invocation with no `.play`; source profile requires a separate explicit invocation |

## Precision review and semantic-admission gate

Production admission is claim-granular, not descriptor-wide, and is unavailable to standard assembled profile 1. The following gate applies only to a separately characterized provenance-preserving exporter profile. For each candidate claim kind, record:

- payload paths and exact profile versions;
- identity prerequisites;
- source fact(s) used for comparison;
- positive and adversarial fixture counts;
- agreement, conflict, unjoined, and loss counts;
- false-positive and false-negative review findings;
- proof that per-source variants are preserved and `upstreamMerged=false`;
- Screenplay representation and lowering behavior;
- conflict behavior; and
- reviewer decision: comparison-only, admitted, or rejected.

Minimum admission conditions:

- zero known false-positive identity joins;
- exact project/assembly/full-name binding for every admitted type;
- exact method identity for every method-scoped claim;
- no unresolved required enum/profile drift;
- no first/last/order/strength winner selection for cross-lane source-versus-resolved conflicts, while preserving Generation's existing same-lane evidence-strength placement behavior;
- stable granular provenance through Generation and lowering;
- explicit conflict and loss diagnostics;
- unchanged source-only and observed-profile `.play` bytes; and
- reviewed package/API/security gates.

A resolved-only claim is not automatically true. A source-only claim is not automatically wrong. Screenplay alone decides semantic representability and admission over explicit variants; Roslyn remains source evidence and the identity-binding baseline, never an authority.

## Stop conditions

Stop the affected phase and do not widen scope when any of these occurs:

- the repository-owned 6.30.0 reproduction does not produce the documented output file, diverges without explanation from the observed fixture profile, or starts the throwing hosted service;
- the fixture requires a database, broker, CritterWatch, started host, private reflection, or unpinned restore;
- the captured payload lacks enough assembly/project/full-name information for exact joins;
- the payload/profile has unknown required schema, enum, or shape;
- raw/normalized capture is non-deterministic without an understood and safely normalized cause;
- a parser mutation yields partial claims after an atomic failure;
- an identity collision joins by short/display/slice name;
- computed redundancy becomes an independent claim source;
- a cross-lane source-versus-resolved disagreement is resolved by first-wins, last-wins, adapter order, payload order, or strength, or same-lane Generation placement semantics are accidentally changed;
- `upstreamMerged` is hidden or per-source variants are invented;
- any existing canonical fixture or hash changes during the comparison experiment;
- the core package gains a JasperFx, Wolverine, Marten, CritterWatch, reflection-loading, database, or broker dependency;
- Generation lacks the granular claim/provenance contract needed by a proposed semantic admission;
- public/package compatibility fails;
- unrun gates are represented as passed;
- descriptor work would delay or contaminate the Generation 0.13 placement/CLI release lane; or
- broker work begins without its separate approved security issue and threat model.

If exact identity is unavailable, the acceptable outcome is a durable comparison/report limitation, not a weaker join.

## Issue plan

### Existing owners

- [Screenplay.Generation #26](https://github.com/Cratis/Screenplay.Generation/issues/26) owns the already released Generation 0.13 placement contract; A/B/C1 complete, C2/D remain downstream.
- [Screenplay.Generation #19](https://github.com/Cratis/Screenplay.Generation/issues/19), [#23](https://github.com/Cratis/Screenplay.Generation/issues/23), and [#24](https://github.com/Cratis/Screenplay.Generation/issues/24) do not own placement or granular resolved evidence unless explicitly expanded.
- [Screenplay.CritterStack #29](https://github.com/Cratis/Screenplay.CritterStack/issues/29) is the durable CritterStack roadmap.
- [Screenplay.CritterStack #44](https://github.com/Cratis/Screenplay.CritterStack/issues/44) and [CLI #95](https://github.com/Cratis/cli/issues/95) own only the atomic adapter/roster lane.
- [Screenplay #148](https://github.com/Cratis/Screenplay/issues/148) owns render/recover fidelity.

### Focused issues

1. **[CritterStack #57](https://github.com/Cratis/Screenplay.CritterStack/issues/57): Adopt released Generation 0.13 source placement.** Own C2 placement adoption and release; exclude descriptor/profile work and #44.
2. **[CLI #111](https://github.com/Cratis/cli/issues/111): Adopt released CritterStack and Generation source placement.** Own D placement adoption and release; exclude descriptor/profile work and #95.
3. **[CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58): Seal, verify, and compare pinned JasperFx EventModelDescriptor evidence.** Own the fixture matrix, durable captures, exact comparison-only profile, sidecar codec/verifier, CI workflow, strict parser, identity joins, reports, mutations, and dependency gates.
4. **[CLI #112](https://github.com/Cratis/cli/issues/112): Passively import application-owned event-model CI artifacts.** Own safe path selection, host-expected context construction, CritterStack verifier/importer invocation, blocking reports, and byte handoff; exclude sidecar duplication, producer execution, and broker work.
5. **[Generation #38](https://github.com/Cratis/Screenplay.Generation/issues/38): Add granular provenance for provenance-preserving resolved evidence.** Own the future exporter-facing lane/provenance/conflict/admission contract; keep #19/#23/#24 separate unless explicitly expanded.
6. **[CLI #113](https://github.com/Cratis/cli/issues/113): Threat-model and secure a local framework-evidence broker.** Own the later protocol and security review; implementation remains blocked until the threat model is accepted.

Issue descriptions should link this plan and the actual owner issues rather than duplicate or broaden their state.

## Execution checklist

### A. Finish the independent current placement lane

- [x] Release Generation 0.13 placement under Generation #26; placement program A/B/C1 complete.
- [x] Open [CritterStack #57](https://github.com/Cratis/Screenplay.CritterStack/issues/57) for C2 adoption.
- [ ] Complete the #57 implementation/PR and release.
- [ ] Run and link CritterStack placement release gates; keep #44 atomic adapter/roster work separate.
- [x] Open [CLI #111](https://github.com/Cratis/cli/issues/111) for D adoption.
- [ ] Complete the #111 implementation/PR and release; keep #95 separate.
- [ ] Confirm descriptor/profile code was absent from those releases.

### B. Capture before schema

- [x] Open [CritterStack #58](https://github.com/Cratis/Screenplay.CritterStack/issues/58) for fixture, seal/verify, strict comparison import, and CI guidance.

Disposable characterization is provisional/in progress:

- [ ] Check in or publish immutable disposable source, transcript, raw bytes, exact SDK/runtime/TFM/configuration/RID/serializer identity, package/assembly hashes, and repository-signature evidence.
- [ ] Preserve the local observations: warning-free net10 build, throwing `IHostedService.StartAsync` not called, and two byte-identical captures at SHA-256 `7fa4248050375c6cfcf308a3db7df86a10a5b96a25aee7d149176b5fc253911d`.

Repository-owned durable capture remains:

- [ ] Add the complete isolated fixture matrix without touching canonical fixtures or root historical pins.
- [ ] Pin WolverineFx/JasperFx/JasperFx.Events/JasperFx.SourceGenerator and record the complete contributor/package graph, exact `.nupkg`/assembly hashes, and NuGet repository signatures.
- [ ] Record exact net10 SDK/runtime/TFM/configuration/RID/serializer; characterize net9 separately.
- [ ] Gate actual `RunJasperFxCommands(args)` command availability rather than a namespace/package heuristic, plus the command-unavailable diagnostic.
- [ ] Add the no-start `IHostedService` sentinel and run the exact metadata command against every built matrix case.
- [ ] Save exact output-file bytes and transcripts; do not scrape standard output.
- [ ] Record raw and normalized hashes plus every supported field/enum/null/omission/order case.
- [ ] Set `upstreamMerged=true` unconditionally and document weak-Name, earlier-source-first scalar, and union-list loss.
- [ ] Verify computed `elements`/`edges` only as integrity redundancy.
- [ ] Derive and freeze the narrow API/payload fingerprint from durable captured evidence; reject shapes outside it.

### C. Seal, verify, and compare

- [ ] Freeze the CritterStack-owned sidecar schema/profile 1 from the durable fixture matrix.
- [ ] Implement and release the shared CritterStack schema parser/verifier/canonical serializer and `evidence seal/verify` producer.
- [ ] Publish the reference application-CI workflow and reproducibility/no-sandbox guidance before passive import work.
- [ ] Implement resource-bounded strict parsing with duplicate-property detection and the two atomicity levels.
- [ ] Implement profile-declared order preservation/set canonicalization plus raw and normalized hashes.
- [ ] Implement durable host project identity + exactly one selected output assembly + full CLR type joins; profile 1 has no method joins.
- [ ] Normalize only command type, first handler type, chain emits/publishes, listed aggregate/projection types, ambiguous read-model references, and `AggregateApplies`.
- [ ] Exclude `HandlerProduces`, method-level `Handles`, `HandlerLoads`, `ProjectionProduces`, and broad `AggregateConsumes`.
- [ ] Implement agreement/conflict/source-only/resolved-only/unjoined/loss reporting with unrelated entries retained after valid import.
- [ ] Prove profile 1 is comparison-only permanently and cannot affect facts or `.play` bytes.
- [ ] Run the complete mutation matrix and dependency/API gates.

### D. Passive import and later admission contracts

- [x] Open [CLI #112](https://github.com/Cratis/cli/issues/112) for passive import.
- [ ] Implement CLI passive import only after released `evidence seal/verify` and CI guidance; CLI passes bytes plus host-expected context to CritterStack.
- [ ] Prove every supplied/required compare/resolved failure blocks with no `.play`; source is a separate explicit invocation.
- [ ] Review comparison precision and stop on insufficient identity.
- [x] Open [Generation #38](https://github.com/Cratis/Screenplay.Generation/issues/38) for resolved-evidence/granular provenance.
- [ ] Design a provenance-preserving per-source exporter that records each source `Subject`, registration index/source type, raw descriptor/null/error, and the final assembled descriptor.
- [ ] Design/release post-0.13 Generation granular claim/provenance contracts for that new profile.
- [ ] Approve an explicit claim admission matrix for the new profile only.
- [ ] Publish Generation, then CritterStack, then CLI admission support, in that order; never admit standard profile 1.

### E. Deferred observed/broker work

- [x] Open [CLI #113](https://github.com/Cratis/cli/issues/113) for the threat model; implementation remains blocked pending approval.
- [ ] Keep `ServiceCapabilities`, CritterWatch, Marten runtime state, and traffic report-only.
- [ ] Create no broker until the separate security issue is approved.
- [ ] Do not infer a dependency on `CritterWatch.SourceGeneration`.

## Completion definition

This plan is complete only when the progress table points to immutable evidence for every completed row. The initial comparison milestone is complete when the full pinned matrix has durable 6.30.0/2.55.0 bytes and environment/package/signature evidence, the CritterStack-owned `evidence seal/verify` and reference CI workflow are released, profile 1 is strictly parsed and exactly joined where possible, and comparison is proven unable to alter existing facts or `.play` bytes. Standard assembled profile 1 is permanently comparison-only. Production admission is a later milestone requiring a provenance-preserving per-source exporter profile, released Generation granular-claim/provenance support, an approved claim matrix, passive CLI acquisition, deterministic package/security gates, and the ordered release chain.

The source profile remains a separate explicit invocation, resolved evidence remains a required proposal/comparison input when selected, observed evidence remains a report sidecar, and Screenplay alone remains semantic authority.
