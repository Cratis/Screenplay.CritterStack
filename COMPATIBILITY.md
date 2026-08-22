<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Critter Stack compatibility reference

## What compatibility means

`Cratis.CritterStack.Screenplay` performs static source interpretation. Compatibility means that a pinned application compiles, the adapter recognizes the asserted source patterns, the generated Screenplay compiles and is print-stable, and every asserted semantic loss has a stable diagnostic.

It does **not** mean runtime behavioral equivalence, complete coverage of every Marten or Wolverine API, or support for every patch in a major line. The adapter never starts the application or connects to PostgreSQL while generating.

Maintaining an explicit compatibility reference is necessary rather than overkill. Marten and Wolverine have moved projection types, introduced source generators, renamed event-sourcing attributes, and changed method conventions across major versions. An unqualified “supports Critter Stack” statement would hide those differences.

## Canonically verified package sets

The canonical workflow pins source commits and verifies these exact package combinations:

| Fixture | Source pin | Marten | Wolverine | Coverage |
| --- | --- | ---: | ---: | --- |
| BankAccountES | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.0 | 6.23.1 | Aggregate handlers, snapshots, single-stream projection, commands, queries, and validation loss |
| CqrsMinimalApi | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.0 | 6.23.1 | Ordinary document CRUD, conventional identity, and HTTP entry points |
| Reports | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.1 | 6.23.1 | `IMartenOp`, document persistence, and custom typed conventional identity |
| MartenWithProjectAspire | `JasperFx/CritterStackSamples@2c94389bcb5face1070d0409ef284973e8aaceea` | 9.20.1 | — | Generic and instance projection registration, async lifecycle, multi-stream projection, and exact `EventProjection.Create` document storage with explicit value-flow loss |
| IncidentService | license-attributed fixture from `JasperFx/wolverine@af4807b5fb225ce7535c67785b74007fdad2dd9f` | 9.23.0 | 6.29.1 | Current HTTP aggregate workflow, response wrappers, direct append/delete, messages, delay, and query |
| CritterStackHelpDesk | `JasperFx/CritterStackHelpDesk@b67659dd7ca6d8ff07e7b9dad20affc4a37b6062` | 6.3.0 | 1.11.1 | Legacy attributes/returns, API-worker contracts, event forwarding, and generated-source exclusion |

Passing one row proves only the behaviors asserted by that fixture. It does not promote the entire Marten or Wolverine major line to verified status.

The 2026-08-21 local canonical run passed all six fixtures. It intentionally did not suppress upstream warnings: Reports pins vulnerable `Microsoft.OpenApi` 2.0.0 (`NU1903`), MartenWithProjectAspire pins vulnerable `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.8.1 (`NU1902`), and legacy HelpDesk targets out-of-support net7.0 and has pre-existing nullability warnings. These applications are built and statically analyzed but never started. Their immutable pins make drift visible; they are compatibility evidence, not dependency recommendations.

## Research baselines

The source architecture was reviewed at:

| Product | Commit | Source version context |
| --- | --- | --- |
| JasperFx | `da9fd17d69df5ff41940800bfb34ad4d88a88391` | `V2.53.0-1` |
| Marten | `a483b09f881f1576152aa42a27b37cc17fab252f` | `V9.28.0-10` |
| Wolverine | `af4807b5fb225ce7535c67785b74007fdad2dd9f` | `V6.29.1` |

On 2026-08-21, the NuGet registry listed Marten 9.29.0 and WolverineFx/WolverineFx.Marten 6.29.2 as the newest stable versions. These registry observations are drift indicators, not automatic support claims:

- <https://api.nuget.org/v3-flatcontainer/marten/index.json>
- <https://api.nuget.org/v3-flatcontainer/wolverinefx/index.json>
- <https://api.nuget.org/v3-flatcontainer/wolverinefx.marten/index.json>

## Support tiers

Use these terms consistently:

1. **Canonical** — the bundled adapter version passes the exact pinned package set and asserted behaviors in CI.
2. **Source-reviewed** — framework source/docs were analyzed and metadata names are implemented, but no exact application fixture proves the behavior.
3. **Recognized with loss** — the construct is detected and produces a stable diagnostic because Screenplay or the adapter cannot preserve it.
4. **Unknown** — package/API generation is outside canonical evidence or uses unresolved customization; generation must not guess.
5. **Unsupported** — the adapter deliberately excludes the construct and explains why.

The adapter should fail closed for a newer major version until canonical evidence exists. A newer patch or minor within a source-reviewed major may be attempted, but the result must identify the detected version and remain human-reviewed.

## Version provenance

Roslyn exposes assembly identities, but assembly versions do not always equal NuGet package versions. Exact package provenance belongs at the workspace boundary owned by Cratis CLI, where `project.assets.json` and the selected target framework are available.

CLI `v2.12.0` implements the runtime provenance report. It records:

- bundled adapter package version;
- selected target framework;
- package ID and resolved NuGet version when available;
- referenced assembly identity/version as corroboration;
- API capability fingerprints;
- matched canonical/source-reviewed support tier;
- recognition status;
- semantic conformance;
- Screenplay lowering fidelity;
- diagnostics for unknown or unsupported framework generations and unresolved provider options.

The initial capability fingerprints cover projection arity/families, lifecycle namespaces, compiled queries, subscriptions, current/legacy Wolverine handler and aggregate metadata, event-capture wrappers, DCB models, and side effects. Extend them with `DeleteEvent<T>` forms, event-boundary APIs, persisted-event side-effect wrappers, and forwarding APIs as those source-profile gaps land. Package versions, assembly capabilities, recognition, semantic conformance, and Screenplay lowering fidelity remain separate dimensions.

Canonical source commits stay in this file and the pinned workflow rather than being repeated in every generated result. This reference, the CLI provenance report, and the pinned canonical workflow together form the compatibility contract.
