# Upstream provenance

This deterministic compatibility fixture is copied from:

- repository: <https://github.com/JasperFx/wolverine>
- commit: `af4807b5fb225ce7535c67785b74007fdad2dd9f`
- path: `src/Samples/IncidentService/IncidentService`
- license: MIT; see [`LICENSE.upstream`](LICENSE.upstream)

The project file replaces upstream project references with the equivalent published Wolverine 6.29.1 packages so the fixture tests consumer-facing NuGet APIs without compiling the entire Wolverine repository. Application source remains the canonical sample source at the pinned commit, except trailing whitespace is normalized to satisfy repository diff checks.
