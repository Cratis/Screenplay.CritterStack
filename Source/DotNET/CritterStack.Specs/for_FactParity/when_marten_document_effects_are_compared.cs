// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// Compares the native Marten document effects, in either contribution order, to the compatibility oracle with full diagnostics.
/// </summary>
public abstract class when_marten_document_effects_are_compared : Specification
{
    protected const string Framework =
        """
        namespace Marten
        {
            public interface IDocumentStore;
            public sealed class StoreOptions
            {
                public MartenRegistry Schema { get; } = new();
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
            public sealed class MartenRegistry { public void For<T>() { } }
            public interface IQuerySession
            {
                T? Load<T>(int id) where T : class;
                System.Linq.IQueryable<T> Query<T>();
            }
            public interface IDocumentOperations : IQuerySession
            {
                void Store<T>(params T[] documents);
                void Insert<T>(params T[] documents);
                void Update<T>(params T[] documents);
                void Delete<T>(int id);
                void Delete(object entity);
                void DeleteWhere<T>(System.Linq.Expressions.Expression<System.Func<T, bool>> predicate);
            }
            public interface IDocumentSession : IDocumentOperations;
            public static class MartenServiceCollectionExtensions
            {
                public static void AddMartenStore<T>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<StoreOptions> configure) where T : class, IDocumentStore { }
            }
        }
        namespace Marten.Ancillary
        {
            public interface IQuerySession<T> : Marten.IQuerySession { }
            public interface IDocumentSession<T> : Marten.IDocumentSession, IQuerySession<T> { }
        }
        namespace Marten.Events.Projections
        {
            public class ProjectionOptions { public void Add<T>() { } }
            public abstract class EventProjection
            {
                public virtual System.Threading.Tasks.ValueTask ApplyAsync(Marten.IDocumentOperations operations, object e, System.Threading.CancellationToken token) => default;
            }
        }
        namespace Wolverine
        {
            public class WolverineOptions;
        }
        namespace Wolverine.Marten
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public class MartenStoreAttribute(System.Type storeType) : System.Attribute;
        }
        """;

    protected GeneratedScreenplayDefinition Expected { get; private set; } = null!;

    protected GeneratedScreenplayDefinition Native { get; private set; } = null!;

    protected GeneratedScreenplayDefinition Reverse { get; private set; } = null!;

    protected AdapterContribution Contribution { get; private set; } = null!;

    protected void Compare(string application)
    {
        var project = Project($"{Framework}\n{application}");
        var contributions = given.native_query_contributions.Observe(project);
        Contribution = contributions.Marten;
        Expected = Generate(contributions.Compatibility);
        Native = Generate(contributions.Marten, contributions.Wolverine);
        Reverse = Generate(contributions.Wolverine, contributions.Marten);
    }

    [Fact] void should_preserve_every_artifact_definition() => Artifacts(Native).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_artifact_definition_in_reverse_order() => Artifacts(Reverse).ShouldEqual(Artifacts(Expected));
    [Fact] void should_preserve_every_relationship_definition() => Relationships(Native).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_every_relationship_definition_in_reverse_order() => Relationships(Reverse).ShouldEqual(Relationships(Expected));
    [Fact] void should_preserve_complete_diagnostics() => Diagnostics(Native).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_preserve_complete_diagnostics_in_reverse_order() => Diagnostics(Reverse).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_preserve_printed_source() => Native.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_printed_source_in_reverse_order() => Reverse.Source.ShouldEqual(Expected.Source);
    [Fact] void should_preserve_verification_status() => Native.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_not_emit_compatibility_document_artifacts() => Contribution.Facts.OfType<ArtifactFact>().Where(fact => fact.Definition.Key.Kind != ArtifactKind.Concept).ShouldBeEmpty();

    protected static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(Definitions(result)
        .Select(definition => definition with
        {
            Properties = [.. definition.Properties.Select(property => property with { Type = property.Type with { TargetArtifactKind = null } })]
        }));

    protected static string Relationships(GeneratedScreenplayDefinition result) =>
        JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions));

    protected static string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);

    protected static IEnumerable<ArtifactDefinition> Definitions(GeneratedScreenplayDefinition result) =>
        result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition);

    protected static int Handlers(GeneratedScreenplayDefinition result) => Definitions(result).Count(definition => definition.Key.Kind == ArtifactKind.Handler);

    protected static int Warnings(GeneratedScreenplayDefinition result, string code) => result.Diagnostics.Count(diagnostic => diagnostic.Code == code);
}
