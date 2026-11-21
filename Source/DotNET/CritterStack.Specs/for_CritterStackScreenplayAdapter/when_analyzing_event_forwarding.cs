// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_event_forwarding : given.an_event_forwarding_application
{
    IReadOnlyList<GenerationDiagnostic> _diagnostics = null!;

    void Because() => _diagnostics = [.. Contribution.Diagnostics.Where(_ => _.Code == "WOLVERINE0027")];

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_only_the_five_forwarding_registrations() => _diagnostics.Count.ShouldEqual(5);
    [Fact] void should_name_the_event_and_transformed_message() => _diagnostics.Single(_ => _.Message.Contains("TransformedTo", StringComparison.Ordinal)).Message.ShouldContain("IncidentCategorised' to 'Forwarding.TryAssignPriority");
    [Fact] void should_name_the_relayed_event() => _diagnostics.Single(_ => _.Message.Contains("PublishEventsToWolverine", StringComparison.Ordinal)).Message.ShouldContain("Forwarding.IncidentCategorised");
    [Fact] void should_report_strict_order() => _diagnostics.Count(_ => _.Message.Contains("ProcessEventsWithWolverineHandlersInStrictOrder", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_report_the_custom_subscription() => _diagnostics.Single(_ => _.Message.Contains("SubscribeToEvents", StringComparison.Ordinal)).Message.ShouldContain("Forwarding.CustomSubscription");
    [Fact] void should_report_enabled_fast_forwarding_only() => _diagnostics.Count(_ => _.Message.Contains("UseFastEventForwarding", StringComparison.Ordinal)).ShouldEqual(1);
    [Fact] void should_locate_every_omission() => _diagnostics.All(_ => _.Source?.Path.EndsWith("Configuration.cs", StringComparison.Ordinal) == true).ShouldBeTrue();
    [Fact] void should_classify_as_warning_unsupported() => _diagnostics.All(_ => _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldBeTrue();
    [Fact] void should_not_invent_consumer_or_reaction_facts() => Contribution.Facts.OfType<ArtifactFact>().Any(_ => _.Definition.Key.Kind == ArtifactKind.Reaction).ShouldBeFalse();
    [Fact] void should_not_invent_consumes_relationships() => Contribution.Facts.OfType<RelationshipFact>().Any(_ => _.Definition.Key.Kind == RelationshipKind.Consumes).ShouldBeFalse();
}
