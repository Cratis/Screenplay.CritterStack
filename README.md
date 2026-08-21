# Screenplay.CritterStack

Generate verified [Cratis Screenplay](https://github.com/Cratis/Screenplay) definitions from Marten and Wolverine application source.

`Cratis.CritterStack.Screenplay` follows the same package architecture as `Cratis.Arc.Screenplay`: a host supplies Roslyn compilations, and the package analyzes framework conventions, builds one semantic application model, lowers it through the shared Screenplay generation SDK, prints canonical `.play` source, and verifies it with the Screenplay compiler.

This is an independent Cratis compatibility project. It is not affiliated with or endorsed by JasperFx. Marten, Wolverine, JasperFx, and Critter Stack names belong to their respective owners. Generated models may require human review wherever diagnostics report semantic loss.

## Goals

- Marten-only event stores, documents, aggregates, projections, and queries.
- Marten + Wolverine HTTP and message handlers.
- Current store-agnostic Wolverine event-sourcing APIs and legacy Marten-specific APIs.
- Markerless event/message discovery from actual framework usage.
- Deterministic output without starting the application or connecting to PostgreSQL.
- Explicit diagnostics whenever source behavior cannot be represented faithfully.

## Architecture

```text
Roslyn compilations
  -> Marten facts
  -> Wolverine facts
  -> Marten + Wolverine contextual interpretation
  -> Cratis.Screenplay.Generation
  -> verified .play source
```

The adapter matches framework APIs by metadata name and does not reference Marten or Wolverine runtime packages.

Shared generation infrastructure lives in [`Cratis/Screenplay.Generation`](https://github.com/Cratis/Screenplay.Generation). Cratis CLI owns `MSBuildWorkspace`, project/host selection, and output.

## Canonical fixtures

The compatibility plan uses:

- Wolverine's current `src/Samples/IncidentService`;
- `JasperFx/CritterStackHelpDesk` for Marten 6/Wolverine 1 behavior;
- BankAccountES and other focused applications from the local Critter Stack sample corpus.

See:

- [`STRATEGY.md`](STRATEGY.md) — why the adapter is public and how it supports visualization, interoperability, and migration
- [`CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md`](CRITTER_STACK_SCREENPLAY_RESEARCH_AND_ARCHITECTURE.md)
- [`CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md`](CRITTER_STACK_SCREENPLAY_IMPLEMENTATION_HANDOVER.md)
- [`IMPLEMENTATION_STATUS.md`](IMPLEMENTATION_STATUS.md)

## Build and test

After the first Screenplay.Generation packages are published:

```shell
dotnet test Screenplay.CritterStack.slnx --configuration Debug
dotnet build Screenplay.CritterStack.slnx --configuration Release
dotnet pack Screenplay.CritterStack.slnx --no-build --configuration Release -o Artifacts/NuGet
```

## License

Screenplay.CritterStack is licensed under the [MIT license](LICENSE).
