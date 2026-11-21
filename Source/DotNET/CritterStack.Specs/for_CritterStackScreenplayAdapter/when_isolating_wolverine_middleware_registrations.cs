// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_isolating_wolverine_middleware_registrations : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace SharedMiddleware;
        public record ProcessOne;
        public record ProcessTwo;
        public record CreateOne;
        public record CreateTwo;
        public record HandlerNotice;
        public record OtherNotice;
        public record HttpNotice;
        public record FilteredNotice;
        public static class FirstHandler { public static void Handle(ProcessOne message) { } }
        public static class SecondHandler { public static void Handle(ProcessTwo message) { } }
        public static class FirstEndpoint
        {
            [Wolverine.Http.WolverinePost("/one")]
            public static void Post(CreateOne message) { }
        }
        public static class SecondEndpoint
        {
            [Wolverine.Http.WolverinePost("/two")]
            public static void Post(CreateTwo message) { }
        }
        public static class HandlerFirst
        {
            public static System.Threading.Tasks.Task<HandlerNotice> BeforeAsync() => System.Threading.Tasks.Task.FromResult(new HandlerNotice());
        }
        public class HandlerSecond { public static OtherNotice After() => new(); }
        public static class HttpMiddleware { public static HttpNotice Before() => new(); }
        public static class Replacement
        {
            [Wolverine.Attributes.WolverineBefore]
            public static HandlerNotice ReplacementPrepare() => new();
        }
        public static class Filtered { public static FilteredNotice Before() => new(); }
        public static class Configuration
        {
            public static void Configure(Wolverine.WolverineOptions options, Wolverine.Http.WolverineHttpOptions http)
            {
                options.Policies.AddMiddleware(typeof(HandlerFirst));
                options.Policies.AddMiddleware<HandlerSecond>();
                options.Policies.AddMiddleware(typeof(HandlerFirst));
                http.AddMiddleware(typeof(HttpMiddleware));
                options.Policies.AddMiddleware(typeof(Filtered), filter: _ => true);
            }
        }
        """;

    DotNetProjectCompilation _alpha = null!;
    DotNetProjectCompilation _beta = null!;
    DotNetProjectCompilation _changed = null!;
    AdapterContribution _first = null!;
    AdapterContribution _repeated = null!;
    AdapterContribution _changedResult = null!;
    AdapterContribution _afterChange = null!;
    AdapterContribution _combined = null!;

    void Establish()
    {
        _alpha = CreateProject(Source, "Alpha");
        var replacement = Source.Replace("typeof(HandlerFirst)", "typeof(Replacement)", StringComparison.Ordinal);
        _beta = CreateProject(replacement, "Beta");
        _changed = CreateProject(replacement, "Alpha");
    }

    void Because()
    {
        var adapter = new CritterStackScreenplayAdapter();
        _first = adapter.Analyze(new DotNetAnalysisContext([_alpha]), new DotNetAdapterOptions());
        _repeated = adapter.Analyze(new DotNetAnalysisContext([_alpha]), new DotNetAdapterOptions());
        _changedResult = adapter.Analyze(new DotNetAnalysisContext([_changed]), new DotNetAdapterOptions());
        _afterChange = adapter.Analyze(new DotNetAnalysisContext([_alpha]), new DotNetAdapterOptions());
        _combined = adapter.Analyze(new DotNetAnalysisContext([_alpha, _beta]), new DotNetAdapterOptions());
    }

    [Fact] void should_compile_each_project() => new[] { _alpha, _beta, _changed }.SelectMany(_ => _.Compilation.GetDiagnostics(TestContext.Current.CancellationToken)).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_reuse_registrations_for_two_handlers_and_two_endpoints() => Stages(_first).Count.ShouldEqual(6);
    [Fact] void should_apply_each_handler_registration_to_each_handler() => Stages(_first).Count(_ => _.Message.Contains("'BeforeAsync'", StringComparison.Ordinal) || _.Message.Contains("'After'", StringComparison.Ordinal)).ShouldEqual(4);
    [Fact] void should_apply_http_registration_only_to_endpoints() => Stages(_first).Where(_ => _.Message.Contains("'Before'", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Subject.Value.Contains("/SharedMiddleware.Create", StringComparison.Ordinal));
    [Fact] void should_keep_cascades_for_each_chain() => Cascades(_first).Count.ShouldEqual(6);
    [Fact] void should_keep_the_first_source_when_duplicate_stages_are_deduplicated() => Stages(_first).Where(_ => _.Message.Contains("'BeforeAsync'", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Source.StartLine == 38);
    [Fact] void should_keep_the_generic_registration_source() => Stages(_first).Where(_ => _.Message.Contains("'After'", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Source.StartLine == 39);
    [Fact] void should_keep_the_http_registration_source() => Stages(_first).Where(_ => _.Message.Contains("'Before'", StringComparison.Ordinal)).ShouldEachConformTo(_ => _.Source.StartLine == 41);
    [Fact] void should_keep_registration_order_within_each_handler_chain() => Stages(_first).Where(_ => _.Source.StartLine != 41).GroupBy(_ => _.Subject).ShouldEachConformTo(_ => string.Join(',', _.Select(stage => stage.Source.StartLine)) == "38,39");
    [Fact] void should_keep_cascade_evidence_at_the_registration_source() => Cascades(_first).ShouldEachConformTo(_ => _.Evidence.Source.Path == "Alpha/Handlers.cs" && _.Evidence.Source.StartLine is 38 or 39 or 41);
    [Fact] void should_not_cascade_runtime_filtered_messages() => Cascades(_first).Where(_ => _.Definition.Key.Target.Value.EndsWith("/SharedMiddleware.FilteredNotice", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_diagnose_the_runtime_filter_once_per_project() => Unresolved(_first).Count.ShouldEqual(1);
    [Fact] void should_keep_the_runtime_registration_source() => Unresolved(_first).Single().Source.StartLine.ShouldEqual(42);
    [Fact] void should_preserve_diagnostics_on_repeated_analysis() => Diagnostics(_repeated).ShouldEqual(Diagnostics(_first));
    [Fact] void should_preserve_all_fact_payloads_and_order_on_repeated_analysis() => FactSignatures(_repeated).SequenceEqual(FactSignatures(_first)).ShouldBeTrue();
    [Fact] void should_keep_fact_counts_on_repeated_analysis() => _repeated.Facts.Count.ShouldEqual(_first.Facts.Count);
    [Fact] void should_pick_up_changed_registration_on_a_fresh_run() => Stages(_changedResult).Count(_ => _.Message.Contains("'ReplacementPrepare'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_not_reuse_old_registrations_on_a_changed_run() => Stages(_changedResult).Where(_ => _.Message.Contains("'BeforeAsync'", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_restore_original_registrations_on_the_next_run() => Diagnostics(_afterChange).ShouldEqual(Diagnostics(_first));
    [Fact] void should_not_leak_changed_facts_into_the_next_run() => FactSignatures(_afterChange).SequenceEqual(FactSignatures(_first)).ShouldBeTrue();
    [Fact] void should_keep_both_projects_with_identical_metadata_names() => Stages(_combined).Count.ShouldEqual(12);
    [Fact] void should_keep_original_middleware_in_its_own_project() => Stages(_combined).Count(_ => _.Source.Path == "Alpha/Handlers.cs" && _.Message.Contains("'BeforeAsync'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_keep_replacement_middleware_in_its_own_project() => Stages(_combined).Count(_ => _.Source.Path == "Beta/Handlers.cs" && _.Message.Contains("'ReplacementPrepare'", StringComparison.Ordinal)).ShouldEqual(2);
    [Fact] void should_not_share_original_registrations_with_the_other_project() => Stages(_combined).Where(_ => _.Source.Path == "Beta/Handlers.cs" && _.Message.Contains("'BeforeAsync'", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_not_share_replacement_registrations_with_the_original_project() => Stages(_combined).Where(_ => _.Source.Path == "Alpha/Handlers.cs" && _.Message.Contains("'ReplacementPrepare'", StringComparison.Ordinal)).ShouldBeEmpty();
    [Fact] void should_diagnose_each_projects_runtime_filter_once() => Unresolved(_combined).Count.ShouldEqual(2);
    [Fact] void should_preserve_runtime_registration_sources() => Unresolved(_combined).Select(_ => _.Source.Path).ShouldContainOnly("Alpha/Handlers.cs", "Beta/Handlers.cs");

    static IReadOnlyList<GenerationDiagnostic> Stages(AdapterContribution contribution) => [.. contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.CompoundStageOmitted)];
    static IReadOnlyList<GenerationDiagnostic> Unresolved(AdapterContribution contribution) => [.. contribution.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved)];
    static IReadOnlyList<RelationshipFact> Cascades(AdapterContribution contribution) => [.. contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Cascades)];
    static string Diagnostics(AdapterContribution contribution) => string.Join('\n', contribution.Diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject}|{_.Source}|{_.Message}"));
    static IReadOnlyList<string> FactSignatures(AdapterContribution contribution) => [.. contribution.Facts.Select(_ => $"{_.GetType().FullName}|{System.Text.Json.JsonSerializer.Serialize(_, _.GetType())}")];
}
