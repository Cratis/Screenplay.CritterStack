<!--
Copyright (c) Cratis. All rights reserved.
Licensed under the MIT license. See LICENSE file in the project root for full license information.
-->

# Strategy and claim boundary

## Current position

`Cratis/Screenplay.CritterStack` is an optional, pre-release compatibility adapter. Given an authorized source context, it interprets bounded Marten and Wolverine source semantics and contributes neutral facts, evidence, and diagnostics through Screenplay.Generation. The generated `.play` document is a reviewable candidate, not an automatic migration result or a claim of behavioral equivalence.

The adapter is not a canonical Cratis prerequisite, production runtime, source of operational truth, automatic materialization authority, compatibility promise, or support commitment.

## Shipped technical behavior

The current package:

- consumes Roslyn compilations and the authored-source context supplied by its host;
- matches exercised Marten and Wolverine APIs and conventions by semantic metadata identity;
- emits neutral artifacts, relationships, evidence, and stable diagnostics for the behavior it can establish;
- omits or diagnoses bounded ambiguity and unsupported semantic shapes rather than deliberately selecting a convenient interpretation;
- composes with Screenplay.Generation, which resolves contributions, prints canonical `.play` source, and verifies that source with the Screenplay compiler; and
- produces deterministic output for the repository's exercised specification and compatibility matrix.

The adapter library does not start the analyzed application or connect to PostgreSQL. This does not make source loading non-executing: a host that evaluates an MSBuild workspace can execute project-controlled build logic. The host owns source authorization, workspace trust, project and target-framework selection, import bounds, and output handling.

## Why the source is public

Public source makes the implemented compatibility boundary inspectable. Maintainers can review:

- the exact framework identities and conventions that are recognized;
- how events, messages, responses, documents, streams, and side effects remain distinct;
- where evidence is exact, configured, conventional, or heuristic;
- which conditions produce `Unknown`, `Conflict`, or `Unsupported` diagnostics; and
- which pinned package and sample versions have repository-recorded evidence.

Publication does not turn those exercised versions into a general support matrix. Compatibility can change when an external framework changes, and no accepted owner, response boundary, deprecation policy, or support ceiling is established here.

## Information and legal boundaries

- Analyze only source the user is authorized to process.
- Do not put private source, secrets, connection strings, local paths, or private endpoints into fixtures, diagnostics, or public documentation.
- Keep physical workspace paths out of stable identities and public evidence.
- Use public framework contracts and independently authored compatibility logic.
- Preserve required attribution for any permitted third-party fixture material.
- Do not claim perfect recovery when static source evidence cannot establish runtime behavior.

## Explicit limitations

Current repository and package evidence does not establish:

- an automatic migration or automatic materialization workflow;
- execution of the recovered model by Stage;
- a Studio import, review, or adoption workflow;
- generation of Marten or Wolverine source from Screenplay;
- equivalence between recovered facts and runtime behavior;
- broad compatibility outside pinned, exercised versions;
- production readiness or production support; or
- adoption value for an external team.

A proposed Screenplay-to-Marten/Wolverine renderer is documented separately as an **unapproved design proposal**. The canonical renderer contract belongs to the [Stage renderer guide](https://github.com/Cratis/Stage/blob/main/Documentation/guides/build-renderer-target.md); this repository does not currently ship that target.

## Evidence required before broader positioning

Broader preview or adoption wording requires independently accepted evidence, including named ownership, a released host workflow, a wholly Cratis-owned deterministic release fixture, fail-closed ambiguity coverage, an extraction-fidelity report, human acceptance before materialization, explicit security and privacy boundaries, exact compatibility and withdrawal wording, and an authorized external exercise.

Until those gates are accepted, describe the existing package only as an optional, pre-release compatibility adapter that produces a reviewable Screenplay candidate from authorized Marten and Wolverine source evidence. Any adoption or support journey built around that package remains proposed until the corresponding evidence and commitments are accepted.
