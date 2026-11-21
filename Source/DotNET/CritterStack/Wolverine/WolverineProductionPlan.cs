// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation;
using Microsoft.CodeAnalysis;

namespace Cratis.CritterStack.Screenplay.Wolverine;

/// <summary>
/// Whether one produced event is linked declaratively to its command, and through which stream identity.
/// </summary>
/// <param name="EventType">The produced event type.</param>
/// <param name="Declarative">Whether the production is a declarative produces link rather than a handler reference.</param>
/// <param name="StreamIdentity">The command member that identifies the target stream, when known.</param>
sealed record WolverineProductionDecision(INamedTypeSymbol EventType, bool Declarative, ISymbol? StreamIdentity);

/// <summary>
/// Everything a command handler produces, as read from source.
/// </summary>
/// <param name="CommandName">The command name.</param>
/// <param name="CommandSubject">The command subject.</param>
/// <param name="CommandSource">The handler source range.</param>
/// <param name="ReturnEvents">Events the handler returns, which Wolverine appends on its behalf.</param>
/// <param name="ReturnIdentity">The command member identifying the stream of the returned events, when known.</param>
/// <param name="BodyEvents">Events the handler body appends or adds to a Wolverine event collection.</param>
/// <param name="SessionAppends">Direct Marten session appends.</param>
/// <param name="Yields">Events yielded by an aggregate handler.</param>
/// <param name="ImperativeDcbEvents">Events appended imperatively through a dynamic consistency boundary.</param>
/// <param name="EventStreamAppendEvents">Events appended through an IEventStream&lt;T&gt; handler parameter.</param>
/// <param name="HasCompoundValidation">Whether compound validation decides whether events are produced.</param>
sealed record WolverineProductionSources(
    string CommandName,
    SubjectId CommandSubject,
    SourceRange? CommandSource,
    IReadOnlyList<ITypeSymbol> ReturnEvents,
    ISymbol? ReturnIdentity,
    IReadOnlyList<ITypeSymbol> BodyEvents,
    IReadOnlyList<WolverineProduction> SessionAppends,
    IReadOnlyList<WolverineProduction> Yields,
    IReadOnlyList<ITypeSymbol> ImperativeDcbEvents,
    IReadOnlyList<INamedTypeSymbol> EventStreamAppendEvents,
    bool HasCompoundValidation);

/// <summary>
/// Decides which produced events a command links with produces, and reports every production that falls back to a handler reference.
/// </summary>
/// <remarks>
/// A fallback is a warning when production evidence is genuinely omitted or unresolved, and information when the handler
/// reference accurately represents a known structural pattern, such as an IEventStream&lt;T&gt; parameter or a dynamic consistency boundary.
/// </remarks>
static class WolverineProductionPlan
{
    /// <summary>
    /// Decides how each produced event is represented.
    /// </summary>
    /// <param name="sources">The productions read from source.</param>
    /// <param name="diagnostics">Receives one diagnostic per production that is not linked, with its severity.</param>
    /// <returns>The decision for each produced event, in a stable order.</returns>
    public static IReadOnlyList<WolverineProductionDecision> Decide(
        WolverineProductionSources sources,
        List<GenerationDiagnostic> diagnostics)
    {
        var produced = new ProducedEvents(
            Distinct(sources.ReturnEvents),
            Distinct(sources.Yields.Select(_ => _.EventType)),
            Distinct(sources.BodyEvents),
            Distinct(sources.SessionAppends.Select(_ => _.EventType)));
        var events = Distinct(produced.Returns.Concat(produced.Yields).Concat(produced.Body).Concat(produced.Session));
        var blockers = Blockers(sources, produced);

        if (blockers.Count == 0)
        {
            return
            [
                .. events.Select(eventType => new WolverineProductionDecision(
                    eventType,
                    Declarative: true,
                    sources.SessionAppends.FirstOrDefault(_ => SymbolEqualityComparer.Default.Equals(_.EventType, eventType))?.StreamIdentity ??
                    sources.ReturnIdentity))
            ];
        }

        Report(sources, blockers, diagnostics);
        return
        [
            .. events.Select(eventType => new WolverineProductionDecision(
                eventType,
                Declarative: IsDeclarativeReturn(sources, produced, eventType),
                StreamIdentity: null))
        ];
    }

    static void Report(WolverineProductionSources sources, IEnumerable<Blocker> blockers, List<GenerationDiagnostic> diagnostics) =>
        diagnostics.AddRange(blockers.Select(blocker => new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.ProductionLinkOmitted,
            Severity = blocker.Severity,
            Outcome = blocker.Outcome,
            Message = $"Command '{sources.CommandName}' is not linked to event '{blocker.EventType.Name}' with produces because {blocker.Reason}; the model references the handler instead",
            Source = blocker.Source ?? sources.CommandSource,
            Subject = sources.CommandSubject
        }));

    static bool IsDeclarativeReturn(WolverineProductionSources sources, ProducedEvents produced, ITypeSymbol eventType) =>
        Contains(produced.Returns, eventType) &&
        !Contains(produced.Body, eventType) &&
        !Contains(produced.Session, eventType) &&
        !Contains(produced.Yields, eventType) &&
        !Contains(sources.ImperativeDcbEvents, eventType) &&
        !sources.HasCompoundValidation;

    static List<Blocker> Blockers(WolverineProductionSources sources, ProducedEvents produced)
    {
        var (returnEvents, yieldEvents, bodyEvents, sessionEvents) = produced;
        var blockers = new List<Blocker>();
        foreach (var production in sources.SessionAppends.Concat(sources.Yields).Where(_ => _.LossReason is not null))
        {
            blockers.Add(Omitted(production.EventType, production.LossReason!, production.LossOutcome, production.Source));
        }

        blockers.AddRange(bodyEvents
            .Where(_ => !Contains(sessionEvents, _))
            .Select(_ => Omitted(_, "it is added to a Wolverine event collection or appended through a persistence call whose stream is not read from source")));
        blockers.AddRange(Distinct(sources.ImperativeDcbEvents)
            .Select(_ => Structural(_, "it is appended imperatively through a dynamic consistency boundary")));
        blockers.AddRange(Distinct(sources.EventStreamAppendEvents)
            .Select(_ => Structural(_, "it is appended through an IEventStream<T> handler parameter")));
        if (sources.HasCompoundValidation)
        {
            blockers.AddRange(Distinct(returnEvents.Concat(yieldEvents).Concat(sessionEvents))
                .Select(_ => Omitted(_, "compound validation decides whether it is produced")));
        }

        var identities = sources.SessionAppends
            .Select(_ => _.StreamIdentity)
            .Concat(returnEvents.Length + sources.Yields.Count > 0 ? [sources.ReturnIdentity] : [])
            .OfType<ISymbol>()
            .Distinct(SymbolEqualityComparer.Default)
            .Count();
        if (identities > 1)
        {
            blockers.AddRange(sessionEvents.Concat(returnEvents).Concat(yieldEvents)
                .Select(_ => Omitted(_, "the handler appends to more than one stream")));
        }

        return
        [
            .. blockers
                .GroupBy(_ => $"{_.EventType.ToDisplayString()}\u001f{_.Reason}", StringComparer.Ordinal)
                .Select(_ => _.First())
                .OrderBy(_ => _.EventType.ToDisplayString(), StringComparer.Ordinal)
                .ThenBy(_ => _.Reason, StringComparer.Ordinal)
        ];
    }

    static bool Contains(IEnumerable<ITypeSymbol> types, ITypeSymbol type) =>
        types.Any(_ => SymbolEqualityComparer.Default.Equals(_, type));

    static INamedTypeSymbol[] Distinct(IEnumerable<ITypeSymbol> types) =>
    [
        .. types
            .OfType<INamedTypeSymbol>()
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<INamedTypeSymbol>()
    ];

    // Production evidence that source does not resolve, so the model genuinely omits it: reported as a warning.
    static Blocker Omitted(
        INamedTypeSymbol eventType,
        string reason,
        GenerationDiagnosticOutcome outcome = GenerationDiagnosticOutcome.Unsupported,
        SourceRange? source = null) =>
        new(eventType, reason, outcome, source, GenerationDiagnosticSeverity.Warning);

    // A known pattern that the handler reference represents accurately, where no declarative link is possible by design.
    static Blocker Structural(INamedTypeSymbol eventType, string reason) =>
        new(eventType, reason, GenerationDiagnosticOutcome.Unsupported, null, GenerationDiagnosticSeverity.Information);

    sealed record ProducedEvents(
        INamedTypeSymbol[] Returns,
        INamedTypeSymbol[] Yields,
        INamedTypeSymbol[] Body,
        INamedTypeSymbol[] Session);

    sealed record Blocker(
        INamedTypeSymbol EventType,
        string Reason,
        GenerationDiagnosticOutcome Outcome,
        SourceRange? Source,
        GenerationDiagnosticSeverity Severity);
}
