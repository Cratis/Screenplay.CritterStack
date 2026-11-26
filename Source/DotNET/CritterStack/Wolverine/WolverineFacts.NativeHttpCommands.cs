// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Cratis.Screenplay.Generation.DotNet;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Source-native emission of effect-free, non-query Wolverine HTTP commands: declaration, direct members,
/// identifier role, entry mapping, source placement and the exact HTTP metadata diagnostic.
/// Endpoints with persistence, return, bus, compound-stage or Marten consequences are deliberately not
/// emitted here; they stay with the effect-bearing units that own those consequences.
/// </summary>
internal static partial class WolverineFacts
{
    /// <summary>
    /// Internal, deliberately not a public stable code: reports that this still-incomplete producer did not emit an endpoint.
    /// </summary>
    internal const string NativeHttpCommandOmittedCode = "WOLVERINE0031";

    /// <summary>
    /// Emits the native facts of every effect-free HTTP command endpoint in the context.
    /// </summary>
    /// <param name="context">The analysis context.</param>
    /// <param name="options">The adapter options.</param>
    /// <param name="adapter">The producing adapter identity.</param>
    /// <returns>The native contribution, including derived source placement.</returns>
    internal static AdapterContribution EmitNativeHttpCommands(
        DotNetAnalysisContext context,
        DotNetAdapterOptions options,
        AdapterIdentity adapter)
    {
        var facts = new List<GenerationFact>();
        var diagnostics = new List<GenerationDiagnostic>();
        var placements = new List<CritterStackPlacementIntent>();
        var hasSourceContext = context.Projects.Any(_ => _.SourceContext is not null);
        var subjects = new CritterStackSubjectResolver(hasSourceContext ? context : null, context);

        // Source observation only: the native Marten adapter owns the store facts and diagnostics.
        var (_, ownership) = Marten.MartenStoreScopeDiscovery.DiscoverOwnership(context, adapter, subjects);
        foreach (var project in context.Projects)
        {
            if (project.Compilation.GetTypeByMetadataName(WellKnownTypes.WolverineOptions) is null)
            {
                continue;
            }

            var registrations = WolverinePolicyDiscovery.DiscoverRegistrations(project);
            var discovery = WolverineHandlerDiscovery.Discover(project, subjects, registrations);
            var validation = WolverineValidationAuthorizationDiscovery.Discover(project, subjects);
            diagnostics.AddRange(discovery.Diagnostics);
            diagnostics.AddRange(validation.Diagnostics);
            diagnostics.AddRange(WolverineEventForwardingDiscovery.Discover(project));
            var scope = new WolverineFactScope
            {
                Project = project,
                Options = options,
                Adapter = adapter,
                Subjects = subjects,
                Resolver = ownership,
                MiddlewareRegistrations = registrations,
                Facts = facts,
                Placements = placements,
                Diagnostics = diagnostics
            };
            foreach (var entry in ObserveEntryPoints(project, discovery.Policy).Where(_ => _.Endpoint is { IsQuery: false }))
            {
                var endpoint = entry.Endpoint!;
                AnalyzeWithSlicePattern(scope, endpoint.Method, endpoint, owned => EmitNativeHttpCommand(owned, context, endpoint, validation));
            }
        }

        diagnostics.AddRange(subjects.Diagnostics);
        facts.AddRange(CritterStackSourcePlacement.Derive(context, options, placements, diagnostics));
        return EnumConceptFacts.AddTo(context, new AdapterContribution { Adapter = adapter, Facts = facts, Diagnostics = diagnostics });
    }

    static void EmitNativeHttpCommand(
        WolverineFactScope scope,
        DotNetAnalysisContext context,
        HttpEndpoint endpoint,
        WolverineValidationAuthorizationDiscoveryResult validation)
    {
        var owned = scope;
        scope = scope.WithOwnership(BackendOwnership.Legacy);
        if (EndpointStages(scope, endpoint).Count > 0)
        {
            scope.Diagnostics.Add(NativeHttpCommandOmitted(scope, endpoint, "has compound-stage consequences"));
            return;
        }

        var command = DescribeEndpointCommand(scope, endpoint);
        var deletesDocuments = !IsEffectFreeEndpointCommand(scope, command) && IsDocumentDeleteEndpointCommand(scope, command);
        if (deletesDocuments)
        {
            // Only document-delete commands take the source-proved backend ownership; effect-free commands stay Legacy.
            scope = owned;
            if (!scope.RequireOwnership(endpoint.Method))
            {
                return;
            }
        }
        else if (!IsEffectFreeEndpointCommand(scope, command))
        {
            scope.Diagnostics.Add(NativeHttpCommandOmitted(scope, endpoint, "has persistence, state-input, bus, return or Marten consequences"));
            return;
        }

        var placement = EndpointPlacementFor(scope, command);
        var key = scope.Key(command.CommandSubject, ArtifactKind.Command);
        var evidence = command.Evidence;
        scope.Facts.Add(NativeCommandDeclaration(scope, key, command));
        scope.Facts.AddRange(NativeCommandMembers(context, key, command));
        scope.Facts.Add(NativeCommandEntryPoint(scope, key, endpoint, evidence));
        scope.Placements.Add(new(
            scope.PlacementId($"wolverine:placement:command:{command.CommandSubject.Value}"),
            key,
            placement.SourceOwner,
            placement.Compatibility,
            evidence));
        scope.Diagnostics.Add(NativeHttpMetadata(command));
        AddEndpointValidationDiagnostics(scope, command, validation);
        if (deletesDocuments)
        {
            AddNativeDocumentDeletes(scope, EmissionFor(scope, command.CommandSubject, evidence), endpoint.Method);
        }
    }

    static bool IsEffectFreeEndpointCommand(WolverineFactScope scope, EndpointCommand command)
    {
        var shape = command.Shape;
        return HasNoPersistenceEffects(shape, scope.Project) &&
               !HasMartenOperations(scope, command.Endpoint.Method) &&
               !command.Endpoint.Method.Parameters.Any(_ => IsStateInputParameter(_, scope.Project)) &&
               shape.Aggregate is null &&
               shape.StreamBindings.Count == 0 &&
               shape.BusConsequences.Count == 0 &&
               shape.OutgoingMessages.Count == 0 &&
               shape.ReturnConsequences.All(_ => _.Kind == WolverineReturnConsequenceKind.HttpResponse);
    }

    // The native contribution is deliberately partial; every endpoint it cannot yet represent completely is reported, never silently dropped.
    static GenerationDiagnostic NativeHttpCommandOmitted(WolverineFactScope scope, HttpEndpoint endpoint, string reason) => new()
    {
        Code = NativeHttpCommandOmittedCode,
        Severity = GenerationDiagnosticSeverity.Warning,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"HTTP {endpoint.Verb} endpoint '{endpoint.Method.ContainingType.Name}.{endpoint.Method.Name}' {reason} and is not yet emitted by the native Wolverine contribution",
        Source = MethodEvidence(endpoint.Method, scope.Project, scope.Adapter, EvidenceStrength.Exact, "Wolverine HTTP endpoint").Source,
        Subject = MethodSubject(scope.Project, endpoint.Method, "command")
    };

    static ArtifactDeclarationFact NativeCommandDeclaration(WolverineFactScope scope, ArtifactKey key, EndpointCommand command) => new()
    {
        Id = new() { Value = BackendOccurrenceKeys.FactId($"{scope.Adapter.Id}:command-artifact:{key.Subject.Value}:{key.Kind}", key.Scope) },
        Subject = key.Subject,
        Evidence = command.Evidence,
        Definition = new() { Artifact = key, Name = command.CommandName, File = command.Evidence.Source?.Path }
    };

    static IReadOnlyList<GenerationFact> NativeCommandMembers(DotNetAnalysisContext context, ArtifactKey key, EndpointCommand command)
    {
        if (command.CommandType is not { } commandType)
        {
            return QueryMemberFacts(command.Endpoint.Method, key, context, command.Evidence);
        }

        var members = WolverineCommandMembers.DeclareNative(commandType, key, context, command.Evidence);
        var identity = WolverineCommandMembers.IdentifyNative(
            new WolverineCommandIdentityInput(commandType, null, [], false, null),
            key,
            command.Evidence);
        return identity is null ? members : [.. members, identity];
    }

    static EntryPointFact NativeCommandEntryPoint(WolverineFactScope scope, ArtifactKey key, HttpEndpoint endpoint, Evidence evidence)
    {
        var method = DotNetMethodIdentity.SubjectFor(scope.Project, endpoint.Method);
        return new()
        {
            Id = new() { Value = BackendOccurrenceKeys.FactId($"{scope.Adapter.Id}:command-entry:{method.Value}", key.Scope) },
            Subject = method,
            Evidence = evidence,
            Definition = new() { Method = method, Artifact = key }
        };
    }

    static GenerationDiagnostic NativeHttpMetadata(EndpointCommand command) => new()
    {
        Code = WolverineDiagnosticCodes.HttpMetadataOmitted,
        Severity = GenerationDiagnosticSeverity.Information,
        Outcome = GenerationDiagnosticOutcome.Unsupported,
        Message = $"HTTP {command.Endpoint.Verb} route '{command.Endpoint.Route}' for '{command.CommandName}' is not represented by the current Screenplay language",
        Source = command.Evidence.Source,
        Subject = command.CommandSubject
    };
}
