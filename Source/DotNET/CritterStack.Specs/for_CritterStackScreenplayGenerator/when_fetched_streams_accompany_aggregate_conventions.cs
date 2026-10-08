// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_fetched_streams_accompany_aggregate_conventions : given.a_wolverine_fetch_many_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Establish() => UseApplication(
        """
        using System;
        using System.Threading.Tasks;
        using JasperFx.Events;
        using Wolverine.Persistence.EventSourcing;
        namespace Wolverine.Persistence.EventSourcing
        {
            public class DeciderFunctionAttribute : Attribute;
            public class WriteModelAttribute : Attribute;
        }
        namespace Wolverine.Marten
        {
            public class AggregateHandlerAttribute : DeciderFunctionAttribute;
            public class WriteAggregateAttribute : Attribute;
        }
        namespace Wolverine.Http
        {
            public class WolverinePostAttribute(string route) : Attribute;
        }
        namespace Mixed
        {
            public class Account { public Guid Id { get; set; } }
            public class Other;
            public record Change(Guid AccountId, Guid OtherId);
            public record Changed;
            public record FetchedChanged;
            public record HttpFetchedChanged;
            public record ParameterFetchedChanged;
            public static class ChangeHandler
            {
                [Wolverine.Marten.AggregateHandler]
                public static async Task<Changed> Handle(Change command, Account account, Marten.IDocumentSession session)
                {
                    var streams = await session.Events.FetchManyForWriting<Other>([command.OtherId]);
                    streams[0].AppendOne(new FetchedChanged());
                    return new();
                }
            }
            public record HttpChange(Guid AccountId, Guid OtherId);
            public record HttpChanged;
            public static class HttpEndpoints
            {
                [Wolverine.Http.WolverinePost("/change")]
                public static async Task<HttpChanged> Post(HttpChange command, [Wolverine.Marten.WriteAggregate] Account account, Marten.IDocumentSession session)
                {
                    var streams = await session.Events.FetchManyForWriting<Other>([command.OtherId]);
                    streams[0].AppendOne(new HttpFetchedChanged());
                    return new();
                }
            }
            public record ParameterChange(Guid AccountId, Guid OtherId);
            public record ParameterChanged;
            public record FollowUp;
            public static class ParameterHandler
            {
                public static async Task<FollowUp> Handle(ParameterChange command, [WriteModel] IEventStream<Account> account, Marten.IDocumentSession session, int unused)
                {
                    var streams = await session.Events.FetchManyForWriting<Other>([command.OtherId]);
                    streams[0].AppendOne(new ParameterFetchedChanged());
                    account.AppendOne(new ParameterChanged());
                    return new();
                }
            }
        }
        """);

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Mixed" });

    [Fact] void should_compile_the_input() => Project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_valid_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_keep_aggregate_returned_events() => Names(ArtifactKind.Event).ShouldContain("Changed");
    [Fact] void should_keep_http_aggregate_returned_events() => Names(ArtifactKind.Event).ShouldContain("HttpChanged");
    [Fact] void should_not_turn_aggregate_returns_into_messages() => Names(ArtifactKind.Message).ShouldNotContain("Changed");
    [Fact] void should_not_turn_http_aggregate_returns_into_messages() => Names(ArtifactKind.Message).ShouldNotContain("HttpChanged");
    [Fact] void should_keep_the_aggregate_identity() => Identifier("Change").ShouldEqual("accountId");
    [Fact] void should_keep_the_http_aggregate_identity() => Identifier("HttpChange").ShouldEqual("accountId");
    [Fact] void should_keep_the_single_parameter_identity() => Identifier("ParameterChange").ShouldEqual("accountId");
    [Fact] void should_keep_parameter_stream_returns_as_cascades() => Names(ArtifactKind.Message).ShouldContain("FollowUp");
    [Fact] void should_keep_all_fetched_appends() => _result.Graph.Relationships.Count(relationship => relationship.Key.Kind == RelationshipKind.Appends && relationship.Key.Target.Value.EndsWith(".Other", StringComparison.Ordinal)).ShouldEqual(3);
    [Fact] void should_report_only_fetched_loading_loss() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.MultipleStreamMetadataOmitted).All(diagnostic => diagnostic.Message.Contains("FetchManyForWriting", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_preserve_the_parameter_owned_feature() => _result.Source.ShouldNotContain("feature ParameterChange");
    [Fact] void should_not_invent_an_infrastructure_aggregate() => Names(ArtifactKind.Aggregate).ShouldNotContain("IDocumentSession");

    IEnumerable<string> Names(ArtifactKind kind) => _result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == kind).Select(artifact => artifact.Variants[0].Definition.Name);
    string Identifier(string command) => _result.Graph.Artifacts.Single(artifact => artifact.Key.Kind == ArtifactKind.Command && artifact.Variants[0].Definition.Name == command).Variants[0].Definition.Properties.Single(property => property.IsIdentifier).Name;
}
