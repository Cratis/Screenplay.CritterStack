// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using application = Cratis.CritterStack.Screenplay.given.a_marten_store_scope_application;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity;

public class when_custom_projections_emit_native_declarations : application
{
    const string Source =
        """
        using Marten;
        namespace Marten.Events.Projections { public interface IProjection; }
        namespace StoreScope
        {
            public class AuditProjection : Marten.Events.Projections.IProjection;
            public static class Configuration
            {
                public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    services.AddMarten(options => { options.EventModelName = "Primary"; options.Projections.Add<AuditProjection>(); });
                }
            }
        }
        """;

    AdapterContribution _native = null!;
    GeneratedScreenplayDefinition _expected = null!;
    GeneratedScreenplayDefinition _actual = null!;

    void Because()
    {
        var project = ProjectFor(Source);
        project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var contributions = given.native_query_contributions.Observe(project);
        _native = contributions.Marten;
        _expected = Generate(contributions.Compatibility);
        _actual = Generate(_native);
    }

    [Fact] void should_preserve_every_artifact_field() => Artifacts(_actual).ShouldEqual(Artifacts(_expected));
    [Fact] void should_preserve_every_relationship_field() => Relationships(_actual).ShouldEqual(Relationships(_expected));
    [Fact] void should_preserve_complete_diagnostics() => diagnostic_parity.Meaning(_actual.Diagnostics).ShouldEqual(diagnostic_parity.Meaning(_expected.Diagnostics));
    [Fact] void should_preserve_printed_source() => _actual.Source.ShouldEqual(_expected.Source);
    [Fact] void should_preserve_verification_status() => _actual.IsSuccess.ShouldEqual(_expected.IsSuccess);
    [Fact] void should_not_emit_compatibility_artifacts() => _native.Facts.OfType<ArtifactFact>().ShouldBeEmpty();
    [Fact] void should_declare_the_custom_projection() => _native.Facts.OfType<ArtifactDeclarationFact>().Single().Definition.Name.ShouldEqual("AuditProjection");
    [Fact] void should_not_infer_custom_processing_members() => _native.Facts.OfType<ArtifactMemberTypeUseFact>().ShouldBeEmpty();

    static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).ToArray());
    static string Relationships(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions).ToArray());
}
