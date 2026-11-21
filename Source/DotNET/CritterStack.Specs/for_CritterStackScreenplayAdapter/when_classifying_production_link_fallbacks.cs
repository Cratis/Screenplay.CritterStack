// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_classifying_production_link_fallbacks : given.a_wolverine_slice_pattern_application
{
    AdapterContribution _result = null!;

    void Establish() => Project = CreateApplication(
        """
        namespace Wolverine.Marten
        {
            public class Events : System.Collections.Generic.List<object>;
        }

        namespace Patterns
        {
            public record Record;
            public record Recorded;
            public record RecordedAlso;
            public record Collect;
            public record Collected;
            public class State;
            public static class RecordHandler
            {
                public static void Handle(Record command, JasperFx.Events.IEventStream<State> stream)
                {
                    stream.AppendOne(new Recorded());
                    stream.AppendOne(new RecordedAlso());
                }
            }

            public static class CollectHandler
            {
                public static Wolverine.Marten.Events Handle(Collect command)
                {
                    Wolverine.Marten.Events events = [new Collected()];
                    return events;
                }
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_report_one_fallback_per_event_stream_append() => Fallbacks("Record").Count.ShouldEqual(2);
    [Fact] void should_report_event_stream_appends_as_information() => Fallbacks("Record").ShouldEachConformTo(diagnostic => diagnostic.Severity == GenerationDiagnosticSeverity.Information);
    [Fact] void should_name_the_event_stream_parameter() => Fallbacks("Record").ShouldEachConformTo(diagnostic => diagnostic.Message.Contains("it is appended through an IEventStream<T> handler parameter", StringComparison.Ordinal));
    [Fact] void should_report_one_fallback_for_the_event_collection() => Fallbacks("Collect").Count.ShouldEqual(1);
    [Fact] void should_report_the_event_collection_as_a_warning() => Fallbacks("Collect").Single().Severity.ShouldEqual(GenerationDiagnosticSeverity.Warning);
    [Fact] void should_name_the_event_collection() => Fallbacks("Collect").Single().Message.ShouldContain("it is added to a Wolverine event collection");

    IReadOnlyList<GenerationDiagnostic> Fallbacks(string command) =>
        [.. _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.ProductionLinkOmitted && diagnostic.Message.Contains($"Command '{command}' ", StringComparison.Ordinal))];
}
