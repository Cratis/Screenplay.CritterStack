// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_persistence_bound_parameters : given.a_persistence_bound_parameter_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_compile_the_fixture() => Project.Compilation.GetDiagnostics().Where(_ => _.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_retain_each_persistence_bound_read() => Reads.Count.ShouldEqual(4);
    [Fact] void should_retain_the_entity_binding() => ReadFor("ServiceSummary").Definition.Key.Discriminator.ShouldEqual("entity");
    [Fact] void should_require_the_entity_by_default() => ReadFor("ServiceSummary").Definition.IsOptional.ShouldBeFalse();
    [Fact] void should_keep_the_entity_binding_singular() => ReadFor("ServiceSummary").Definition.IsCollection.ShouldBeFalse();
    [Fact] void should_retain_the_optional_entity_binding() => ReadFor("Overrides").Definition.Key.Discriminator.ShouldEqual("entity");
    [Fact] void should_mark_the_optional_entity_as_optional() => ReadFor("Overrides").Definition.IsOptional.ShouldBeTrue();
    [Fact] void should_keep_the_optional_entity_binding_singular() => ReadFor("Overrides").Definition.IsCollection.ShouldBeFalse();
    [Fact] void should_retain_the_first_or_default_binding() => ReadFor("Defaults").Definition.Key.Discriminator.ShouldEqual("first-or-default");
    [Fact] void should_mark_the_nullable_singleton_as_optional() => ReadFor("Defaults").Definition.IsOptional.ShouldBeTrue();
    [Fact] void should_keep_the_singleton_binding_singular() => ReadFor("Defaults").Definition.IsCollection.ShouldBeFalse();
    [Fact] void should_retain_the_queryable_binding() => ReadFor("Heartbeat").Definition.Key.Discriminator.ShouldEqual("queryable");
    [Fact] void should_require_the_queryable_binding() => ReadFor("Heartbeat").Definition.IsOptional.ShouldBeFalse();
    [Fact] void should_mark_the_queryable_as_a_collection() => ReadFor("Heartbeat").Definition.IsCollection.ShouldBeTrue();
    [Fact] void should_select_the_message_after_a_queryable_parameter() => CommandNames.ShouldContain("HeartbeatsRequested");
    [Fact] void should_not_report_loss() => Contribution.Diagnostics.ShouldBeEmpty();

    IReadOnlyList<RelationshipFact> Reads =>
        [.. Contribution.Facts.OfType<RelationshipFact>().Where(_ => _.Definition.Key.Kind == RelationshipKind.Reads)];

    IReadOnlyList<string> CommandNames =>
        [.. _graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Command).Select(_ => _.Variants.Single().Definition.Name)];

    RelationshipFact ReadFor(string documentName)
    {
        var target = _graph.Artifacts.Single(_ => _.Key.Kind == ArtifactKind.ReadModel && _.Variants.Single().Definition.Name == documentName).Key.Subject;
        return Reads.Single(_ => _.Definition.Key.Target == target);
    }
}
