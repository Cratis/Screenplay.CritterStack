// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_endpoint_idempotency : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace EndpointPolicies;
        public record Create(string Id);
        public record Response(string Id);
        [Wolverine.Attributes.Topic("orders")]
        public static class CreateEndpoint
        {
            [Wolverine.Http.WolverinePost("/orders")]
            [Wolverine.Http.DeduplicatedWithResponse]
            [Wolverine.Attributes.AlwaysPublishResponse]
            public static Response Post(Create message) => new(message.Id);
        }
        """;

    static readonly string[] _policies =
    [
        "Wolverine.Attributes.Topic",
        "Wolverine.Http.DeduplicatedWithResponse",
        "Wolverine.Attributes.AlwaysPublishResponse"
    ];

    DotNetProjectCompilation _project = null!;
    DotNetProjectCompilation _baseline = null!;
    AdapterContribution _contribution = null!;
    AdapterContribution _withoutPolicies = null!;

    void Establish()
    {
        _project = CreateProject(Source);
        var baseline = _policies.Aggregate(Source, (source, policy) => source.Replace(policy, policy.Replace("Wolverine", "Lookalike", StringComparison.Ordinal), StringComparison.Ordinal));
        _baseline = CreateProject(baseline);
    }

    void Because()
    {
        _contribution = Analyze(_project);
        _withoutPolicies = Analyze(_baseline);
    }

    [Fact] void should_compile_both_fixtures() => new[] { _project, _baseline }.SelectMany(_ => _.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_all_three_unrepresented_policies() => Policies.Count.ShouldEqual(3);
    [Fact] void should_anchor_each_policy_to_its_attribute() => Policies.Select(_ => _.Source.StartLine).ShouldContainOnly(4, 8, 9);
    [Fact] void should_classify_policy_losses_as_unsupported_warnings() => Policies.ShouldEachConformTo(_ => _.Severity == GenerationDiagnosticSeverity.Warning && _.Outcome == GenerationDiagnosticOutcome.Unsupported);
    [Fact] void should_name_each_policy() => _policies.ShouldEachConformTo(policy => Policies.Any(_ => _.Message.Contains(policy.Split('.').Last() + "Attribute", StringComparison.Ordinal)));
    [Fact] void should_leave_all_endpoint_facts_byte_identical() => SerializedFacts(_contribution).ShouldEqual(SerializedFacts(_withoutPolicies));
    [Fact] void should_not_match_same_named_lookalike_attributes() => _withoutPolicies.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.EndpointIdempotencyOmitted).ShouldBeEmpty();
    [Fact] void should_not_duplicate_command_execution() => _contribution.Facts.OfType<ArtifactFact>().Count(_ => _.Definition.Key.Kind == ArtifactKind.Command).ShouldEqual(1);

    IReadOnlyList<GenerationDiagnostic> Policies => [.. _contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.EndpointIdempotencyOmitted)];

    static string SerializedFacts(AdapterContribution contribution) => string.Join('\n', contribution.Facts
        .OrderBy(_ => _.Id.Value, StringComparer.Ordinal)
        .Select(_ => System.Text.Json.JsonSerializer.Serialize(_, _.GetType())));
}
