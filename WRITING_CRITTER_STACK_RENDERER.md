<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Unapproved design proposal: Screenplay-to-Critter Stack renderer

> **Status: unapproved design proposal.** This paper is not canonical onboarding, an accepted implementation plan, or evidence that a renderer package, CLI workflow, Stage execution path, support contract, or delivery commitment exists. The canonical renderer contract is the [Stage renderer guide](https://github.com/Cratis/Stage/blob/main/Documentation/guides/build-renderer-target.md).

This repository currently implements the opposite direction: Marten/Wolverine source to Screenplay. If maintainers later approve a Screenplay-to-Critter Stack target and version its realization profile, it should use `Cratis.Stage.Contracts`, not reverse `CritterStackScreenplayAdapter` or Generation facts.

The sections below preserve a candidate architecture and decision checklist for review. Do not use them to begin implementation until the profile and work are explicitly approved.

```text
Screenplay files
  -> Screenplay SemanticModelCompiler
  -> executable semantic model
  -> Critter Stack target admission
  -> CritterStackArtifactRenderPlanner
  -> deterministic ArtifactRenderPlan
  -> managed CLI publication
```

The generic renderer contract and checklist live in the [Stage renderer guide](https://github.com/Cratis/Stage/blob/main/Documentation/guides/build-renderer-target.md).

## Keep the two directions separate

| Direction | Contract | Owner |
| --- | --- | --- |
| Marten/Wolverine source → Screenplay | `IDotNetScreenplayAdapter` and neutral Generation facts | Screenplay.Generation + this repository |
| Screenplay → Marten/Wolverine source | `IArtifactRenderPlanner` and `ArtifactRenderPlan` | Stage contracts + a Critter Stack target package |

Do not route target generation through `AdapterContribution`, `ResolvedApplicationGraph`, or Roslyn source discovery. Rendering consumes Screenplay's executable semantic model.

## Choose the package boundary

A target implementation should live in this repository because it owns Marten/Wolverine compatibility and naming decisions.

Recommended layout:

```text
Source/DotNET/Rendering.CritterStack/
  CritterStackArtifactRenderPlanner.cs
  CritterStackRenderProfile.cs
  Admission/
  Emission/
  Naming/
  Renderers/
Source/DotNET/Rendering.CritterStack.Specs/
```

The package references:

- `Cratis.Stage.Contracts`;
- `Cratis.Screenplay` semantic contracts;
- no CLI package;
- target Marten/Wolverine packages only where target API types are required for compile verification.

CLI integration remains a separate, statically allowlisted wrapper.

## Version the target profile

Screenplay does not choose all framework realization concerns. The target profile must resolve them explicitly before planning:

| Choice | Required decision |
| --- | --- |
| Marten | Exact supported package generation |
| Wolverine | Exact supported package generation |
| Projection lifecycle | Inline or async; never inferred |
| Concept realization | Vogen, target-owned record structs, or another explicit strategy |
| Event stream identity | Guid or string strategy |
| Handler realization | Explicit session append or a documented Wolverine return convention |
| Queries | Exact Marten query pattern |
| HTTP | Disabled initially, or one explicit route/profile convention |
| Scaffold | Exact project and configuration template bytes |

The current source-compatibility pins in this repository are Marten `9.29.0`, WolverineFx `6.29.2`, and Vogen `8.0.7`. They are a useful first profile, not an implicit promise that every future patch behaves identically.

A concrete candidate for maintainers to approve is:

| Profile field | Proposed v1 value |
| --- | --- |
| Target | `critter-stack` |
| Target version | `marten-9.29-wolverine-6.29` |
| Renderer | `critter-stack-dotnet` |
| Renderer version | `1` |
| Target framework | `net10.0` |
| Projection lifecycle | Inline |
| Concept realization | Vogen 8.0.7 |
| Stream identity | Guid, unwrapped from the Vogen value |
| Handler persistence | Explicit `IDocumentSession.Events.Append(...)` |
| Query | Optional keyed snapshot through `IQuerySession.LoadAsync<T>` |
| HTTP | Disabled |
| Validation | **Unresolved approval blocker:** select and pin one Wolverine-compatible realization for `not empty` |
| Scaffold | Exact immutable project/configuration input bytes |

Do not implement the package until the validation row and every other changed value are approved. The owner is the Critter Stack maintainer team; the decision belongs in a tracked profile/compatibility document and package specs, not in CLI defaults.

## Start with a narrow complete vertical

The first target should support only the semantic subset that can be generated completely and compiled:

1. primitive concepts and composite types;
2. one state-change command;
3. one produced event with an explicit destination and mappings;
4. one read model;
5. one one-instance projection transition;
6. one optional snapshot lookup by key;
7. modeled success and rejection specifications.

Reject every other reachable semantic form. In particular, initially block:

- Automation and Translation slices;
- reactions and reducers not represented by the executable plan;
- conditional production;
- affected cardinality other than one;
- arbitrary query performers or filters;
- routes and HTTP metadata;
- transport topology and scheduling;
- tenancy, event aliases, upcasts, and custom serializers;
- policy, listener, saga, and middleware bodies;
- authorization or validation outside the admitted portable subset.

A small complete target is safer than broad code containing guesses or `TODO` placeholders.

## Map semantic artifacts to Critter Stack code

A recommended initial mapping is:

| Screenplay semantic artifact | Initial Critter Stack realization |
| --- | --- |
| Primitive concept | Profile-selected Vogen value object or target-owned record struct |
| Composite type | Immutable C# record |
| Command | Immutable C# record |
| Produced event | Immutable C# record |
| Event destination | Explicit stream identity passed to Marten append |
| State-change behavior | Wolverine handler method |
| Read model | Marten document record/class |
| Projection transition | `SingleStreamProjection<TReadModel, TId>` with exact mappings |
| Snapshot lookup | `IQuerySession` keyed lookup |
| Specification | Cratis specification fixture against generated public behavior |

Prefer an explicit handler append for the first target:

```csharp
public static class RegisterProjectHandler
{
    [WolverineHandler]
    public static void Handle(
        RegisterProject command,
        IDocumentSession session) =>
        session.Events.Append(
            command.ProjectId.Value,
            new ProjectRegistered(command.ProjectId, command.Name));
}
```

This avoids relying on target conventions that Screenplay does not state. A later profile can choose Wolverine return-value persistence when its semantics are fully admitted and pinned.

The produced event must carry every property required by the admitted mappings, including the mapped identifier. With the proposed Vogen/Guid profile, unwrap the stream identity through `.Value`; do not rely on an implicit conversion.

Do not generate HTTP attributes from artifact names. Screenplay's portable semantic model does not carry enough route intent for a trustworthy route.

The admitted vertical includes `not empty` validation and rejection specifications, but the Critter Stack realization is not yet selected. The profile must choose one exact validation package/middleware and show how the generated rejection spec executes it. Until then, validation admission blocks the renderer.

## Understand diagnostic ownership

Failures occur in three layers:

| Layer | Examples | Owner |
| --- | --- | --- |
| Screenplay compilation | Invalid syntax, references, or semantic binding | Screenplay compiler diagnostics |
| Portable execution-plan admission | Conditional production, non-one affected cardinality, unsupported query shape | `PLAN-*` issues from `SemanticExecutionPlan.Compile(...)` |
| Critter Stack realization | Unsupported profile, concept strategy, package generation, or target-specific mapping | `CRITTER-RENDER-*` diagnostics |

The current CLI creates the portable execution plan before invoking a target. If that step fails, the Critter Stack planner is not called. Do not promise a `CRITTER-RENDER-*` diagnostic for a concern already rejected by the portable plan. Rendering a broader ESM subset requires an additive Stage/CLI request contract first.

## Implement target admission

Create a `SemanticCritterStackAdmission` phase before emission. It receives the selected semantic slices and returns typed target diagnostics.

Suggested stable code families:

| Code | Meaning |
| --- | --- |
| `CRITTER-RENDER-001` | Profile or package generation is not supported |
| `CRITTER-RENDER-002` | Slice kind is not supported |
| `CRITTER-RENDER-003` | Command production cannot be realized exactly |
| `CRITTER-RENDER-004` | Projection transition/cardinality is unsupported |
| `CRITTER-RENDER-005` | Query contract is unsupported |
| `CRITTER-RENDER-006` | Concept realization is unresolved |
| `CRITTER-RENDER-007` | Framework-specific behavior is absent from the semantic model |

Each diagnostic includes the affected `SemanticId`. Errors block dependent artifacts. Never generate a fallback type, empty handler, no-op projection, or guessed configuration.

## Implement the planner

Create `CritterStackArtifactRenderPlanner : IArtifactRenderPlanner`.

The Cratis renderer's semantic context and admission helpers are internal, so a new target cannot reuse them. Implement a target-local `SemanticCritterStackContext` that indexes artifacts by `SemanticId`, recursively traverses nested features, selects the requested scope, and exposes stable selected slices. Do not copy display-name joins.

The planner should:

1. validate exact target, target-version, renderer, renderer-version, and required input name/version values;
2. select the requested application/module/feature/slice scope;
3. add application-wide scaffold inputs only for application scope;
4. run Critter Stack admission over every selected slice;
5. stop dependent emission when admission reports errors;
6. render concepts and composite types in stable semantic-ID order;
7. render each admitted state-change and state-view artifact;
8. render modeled specifications;
9. return `ArtifactRenderPlan.Create(...)` with every artifact and diagnostic.

Use `PlannedArtifact.CreateText(...)` for C# and project files. It normalizes line endings, encodes UTF-8 without a byte-order mark, and hashes content. `ArtifactRenderPlan` rejects traversal, duplicate paths, and case-insensitive collisions.

Do not write to the filesystem from the planner.

## Generate deterministic .NET artifacts

Keep target-specific naming in one component. Every path and identifier should derive from stable semantic identity plus the explicit profile, never from enumeration order or physical source paths.

Recommended path shape:

```text
Source/<Module>/<Feature>/<Slice>/<Slice>.cs
Source/<Module>/<Feature>/<Slice>/<Slice>.Specs.cs
Source/Common/<Concept>.cs
<Application>.csproj
```

The renderer may choose another documented shape, but application and narrower scopes must produce identical paths and bytes for shared artifacts. The profile must also declare whether a narrow scope is a compilable dependency closure or a review fragment; do not imply closure unless common concepts, types, and referenced artifacts are emitted.

Use a small target-owned C# builder initially. Extract a shared Stage .NET emitter only after a second renderer proves that the abstraction is genuinely common.

## Verify generated code

For each supported profile:

1. plan into memory;
2. materialize into a fresh temporary directory;
3. restore exact pinned Marten/Wolverine packages;
4. compile with warnings as errors;
5. run generated specification fixtures;
6. plan again and compare every path, hash, and byte;
7. run from a relocated workspace and compare output;
8. reject unsupported models without leaving publishable partial code.

MSBuild evaluation executes project-controlled code. Use trusted fixtures or isolation.

## Add a round-trip regression check

After generated source compiles, run `CritterStackScreenplayGenerator` over it and compare the recovered semantic graph with the original admitted Screenplay subset.

Use this only as a regression check:

```text
Screenplay admitted subset
  -> Critter Stack renderer
  -> generated Marten/Wolverine source
  -> CritterStack source adapter
  -> recovered Screenplay facts
```

A round trip is not proof of runtime equivalence. Target choices absent from Screenplay—projection lifecycle, routes, tenancy, transport, and framework policy—cannot round-trip as original intent.

## Register with Cratis CLI

The CLI renderer roster is intentionally static. Once the planner and package are reviewed:

1. publish and version the renderer package;
2. add its version to `cli/Directory.Packages.props` and its package reference to `cli/Source/Cli/Cli.csproj`;
3. add a `CritterStackRenderTarget` under `cli/Source/Cli/Commands/Render`;
4. resolve the exact profile and scaffold inputs;
5. invoke `CritterStackArtifactRenderPlanner`;
6. add the target explicitly to the `RenderTargetRoster` constructor;
7. update render-command help with the exact target name;
8. add a real `cratis render --target critter-stack` integration spec plus dependency-closure and managed-publication specs;
9. use the existing managed artifact publisher.

Do not load renderer plugins from the application workspace. Static admission protects the CLI from arbitrary code and framework-version conflicts.

## Required specification matrix

The renderer package should prove:

- exact profile admission and wrong-profile rejection;
- every supported ESM vertical;
- one blocking diagnostic per unsupported semantic concern;
- no dependent artifacts after an admission error;
- deterministic output under reversed semantic and input enumeration;
- application/module/feature/slice scope equivalence;
- stable output under relocated workspaces;
- path traversal and case-collision rejection;
- exact pinned-package compilation;
- generated specification behavior;
- source-adapter round-trip for the admitted subset;
- no filesystem, process, network, clock, or ambient service access during planning.

## Proposed implementation sequence

Only after the profile and implementation work are approved:

1. Add `Rendering.CritterStack` and `Rendering.CritterStack.Specs` projects to this solution.
2. Add one `RegisterProject.play` fixture matching the admitted Stage state-change/state-view vertical.
3. Compile it to ESM and write a red profile-admission spec.
4. Implement `SemanticCritterStackContext` and `SemanticCritterStackAdmission`.
5. Render common concepts and composite types.
6. Render the command, mapped event, and explicit Marten append.
7. Implement the approved `not empty` validation and prove the rejection specification.
8. Render the read model, inline single-stream projection, registration, and optional keyed query.
9. Materialize and compile against Marten 9.29.0, WolverineFx 6.29.2, and Vogen 8.0.7 with warnings as errors.
10. Add deterministic scope, relocation, input-version, and path-safety specs.
11. Add the source-adapter round-trip oracle for the admitted subset.
12. Publish the renderer package, then integrate it into the CLI static roster.

## Approval blockers

The proposed Critter Stack profile is not approved because validation realization and other target-profile choices remain unresolved. The portable executable semantic model is also intentionally narrower than the full Screenplay language.

Do not begin implementation from this proposal. Any future work requires explicit approval of the first versioned profile, supported vertical, ownership, compatibility boundary, and verification plan. Recheck the canonical Stage renderer guide at that time rather than treating this paper as current API authority.
