// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_consistent_aggregate_attributes : given.a_read_model_application
{
    protected override string ApplicationSource =>
        """
        namespace Aggregates;

        public record ChangeClass(System.Guid Id);
        public record ChangeMethod(System.Guid Id);
        public record ChangePlain(System.Guid Id);
        public record ChangeHttp(System.Guid Id, long Version);
        public record ChangeDerived(System.Guid Id);
        public record ChangeDisabled(System.Guid Id);
        public record ChangeFake(System.Guid Id);
        public record ClassChanged;
        public record MethodChanged;
        public record PlainChanged;
        public record HttpChanged;
        public record DerivedChanged;
        public record DisabledChanged;
        public record FakeChanged;
        public class Account { public System.Guid Id { get; set; } }
        public class ConsistentAggregateHandlerAttribute : System.Attribute;
        public class DerivedConsistentAttribute : Wolverine.Http.Marten.ConsistentAggregateAttribute;

        [Wolverine.Marten.ConsistentAggregateHandler]
        public static class ClassHandler
        {
            public static ClassChanged Handle(ChangeClass request, Account aggregate) => new();
        }
        public static class MethodHandler
        {
            [Wolverine.Marten.ConsistentAggregateHandler]
            public static MethodChanged Handle(ChangeMethod request, Account aggregate) => new();
        }
        public static class PlainHandler
        {
            public static PlainChanged Handle(ChangePlain request,
                [Wolverine.Marten.ConsistentAggregate] Account aggregate) => new();
        }
        public static class Endpoints
        {
            [Wolverine.Http.WolverinePost("/change")]
            public static void Change(ChangeHttp request,
                [Wolverine.Http.Marten.ConsistentAggregate(nameof(ChangeHttp.Id), VersionSource = nameof(ChangeHttp.Version))]
                JasperFx.Events.IEventStream<Account> aggregate) => aggregate.AppendOne(new HttpChanged());
        }
        public static class DerivedHandler
        {
            public static void Handle(ChangeDerived request,
                [DerivedConsistent] JasperFx.Events.IEventStream<Account> aggregate) => aggregate.AppendOne(new DerivedChanged());
        }
        public static class DisabledHandler
        {
            public static DisabledChanged Handle(ChangeDisabled request,
                [Wolverine.Marten.ConsistentAggregate(AlwaysEnforceConsistency = false)] Account aggregate) => new();
        }
        public static class FakeHandler
        {
            [ConsistentAggregateHandler]
            public static FakeChanged Handle(ChangeFake request, Account aggregate) => new();
        }
        """;

    void Because() => Generate();

    [Fact] void should_keep_the_authored_input_compilable() =>
        Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeFalse();

    [Fact] void should_recognize_class_method_parameter_http_and_derived_forms() =>
        Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Command)
            .Select(fact => fact.Definition.Name).ShouldContainOnly(["ChangeClass", "ChangeMethod", "ChangePlain", "ChangeHttp", "ChangeDerived", "ChangeDisabled"]);

    [Fact] void should_keep_the_existing_persisted_event_semantics() =>
        Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind == ArtifactKind.Event)
            .Select(fact => fact.Definition.Name).ShouldContainOnly(["ClassChanged", "MethodChanged", "PlainChanged", "HttpChanged", "DerivedChanged", "DisabledChanged"]);

    [Fact] void should_report_each_unrepresentable_consistency_source() =>
        ConsistencyDiagnostics().Count().ShouldEqual(5);

    [Fact] void should_locate_consistency_diagnostics_in_authored_source() =>
        ConsistencyDiagnostics().All(diagnostic => diagnostic.Source?.Path == "Aggregates/Handlers.cs").ShouldBeTrue();

    [Fact] void should_report_consistency_as_information_not_a_concurrency_fact() =>
        ConsistencyDiagnostics().All(diagnostic => diagnostic.Severity == GenerationDiagnosticSeverity.Information).ShouldBeTrue();

    [Fact] void should_retain_the_explicit_version_diagnostic() =>
        ConsistencyDiagnostics().Any(diagnostic => diagnostic.Source?.StartLine == 6).ShouldBeTrue();

    IEnumerable<GenerationDiagnostic> ConsistencyDiagnostics() => Contribution.Diagnostics
        .Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.StreamVersionOmitted);
}
