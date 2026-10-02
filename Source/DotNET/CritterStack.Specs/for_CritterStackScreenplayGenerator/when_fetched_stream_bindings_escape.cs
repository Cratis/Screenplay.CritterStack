// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_fetched_stream_bindings_escape : given.a_wolverine_fetch_many_application
{
    static readonly string[] _escapes =
    [
        "streams.Escape();",
        "slot.Escape();",
        "streams[0].Escape();",
        "Escapes.Accept(streams);",
        "Escapes.Accept(slot);",
        "Escapes.Accept(streams[0]);",
        "Escapes.Change(ref streams);",
        "Escapes.Change(ref slot);",
        "Escapes.Replace(out streams);",
        "Escapes.Replace(out slot);",
        "Action callback = () => slot.AppendOne(new Escaped());",
        "Action callback = () => streams[0].AppendOne(new Escaped());",
        "void Callback() => slot.AppendOne(new Escaped());",
        "foreach (var stream in streams) { stream.Escape(); }",
        "var alias = slot;",
        "var state = slot.Aggregate;"
    ];
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => UseApplication(
        $$"""
        using System;
        using System.Threading.Tasks;
        namespace Escaping;
        public class Account;
        public record Escaped;
        public static class Escapes
        {
            public static void Escape<T>(this T value) { }
            public static void Accept<T>(T value) { }
            public static void Change<T>(ref T value) { }
            public static void Replace<T>(out T value) => value = default!;
        }
        {{string.Join('\n', _escapes.Select((escape, index) => $$"""
        public record Change{{index}}(Guid Id);
        public static class Escape{{index}}Handler
        {
            public static async Task Handle(Change{{index}} command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                var slot = streams[0];
                {{escape}}
                slot.AppendOne(new Escaped());
                streams[0].AppendOne(new Escaped());
            }
        }
        """))}}
        """);

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Escaping" });

    [Fact] void should_compile_the_escape_shapes() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_not_invent_any_escaped_append() => _result.Graph.Relationships.Where(relationship => relationship.Key.Kind == RelationshipKind.Appends).ShouldBeEmpty();
    [Fact] void should_not_invent_any_escaped_event() => _result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.Event).ShouldBeEmpty();
    [Fact] void should_retain_independent_fetch_reads() => _result.Graph.Relationships.Count(relationship => relationship.Key.Kind == RelationshipKind.Reads).ShouldEqual(_escapes.Length);
    [Fact] void should_report_each_unsupported_direct_append() => _result.Diagnostics.Count(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.EventWriteTargetUnresolved).ShouldEqual(_escapes.Length * 2);
    [Fact] void should_locate_all_append_losses() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.EventWriteTargetUnresolved).All(diagnostic => diagnostic.Source?.Path == "Batches/Handlers.cs" && diagnostic.Source.StartLine > 0).ShouldBeTrue();
}
