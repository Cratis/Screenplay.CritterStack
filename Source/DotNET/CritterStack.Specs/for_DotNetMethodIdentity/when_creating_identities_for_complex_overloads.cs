// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_DotNetMethodIdentity;

public class when_creating_identities_for_complex_overloads : given.a_dot_net_method_identity_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "IdentitySamples" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_separate_same_simple_type_names_from_different_namespaces() => HandlerSubjects("Same").Distinct(StringComparer.Ordinal).Count().ShouldEqual(2);
    [Fact] void should_keep_same_named_parameter_types_readable() => HandlerNames("Same").ShouldContainOnly(["Handler.Same(Alpha.Payload, Marten.IDocumentSession)", "Handler.Same(Beta.Payload, Marten.IDocumentSession)"]);
    [Fact] void should_include_multiple_parameters() => HandlerName("Combine").ShouldEqual("Handler.Combine(Alpha.Payload, Beta.Payload, string, Marten.IDocumentSession)");
    [Fact] void should_include_ref_in_out_ref_readonly_and_params_modifiers() => HandlerName("Modifiers").ShouldEqual("Handler.Modifiers(ref int, in long, out short, ref readonly decimal, Marten.IDocumentSession, params string[])");
    [Fact] void should_include_generic_method_arity() => HandlerName("Generic").ShouldEqual("Handler.Generic`2(TFirst, TSecond, Marten.IDocumentSession)");
    [Fact] void should_include_arrays_nested_and_generic_types() => HandlerName("Shapes").ShouldEqual("Handler.Shapes(IdentitySamples.Outer<Alpha.Payload>.Nested<Beta.Payload>[], System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Alpha.Payload[,]>>, Marten.IDocumentSession)");
    [Fact] void should_keep_all_method_subjects_collision_safe() => Handlers.Select(_ => _.Key.Subject).Distinct().Count().ShouldEqual(Handlers.Count);
    [Fact] void should_keep_generated_overloads_from_interfering_with_authored_overloads() => HandlerNames("Same").Count.ShouldEqual(2);
    [Fact] void should_not_emit_generated_generic_overloads() => HandlerNames("Generic").Count.ShouldEqual(1);
    [Fact] void should_keep_display_names_human_readable() => Handlers.SelectMany(_ => _.Variants).Select(_ => _.Definition.Name).Any(_ => _.Contains("global::", StringComparison.Ordinal) || _.Contains('%', StringComparison.Ordinal)).ShouldBeFalse();

    IReadOnlyList<ResolvedArtifact> Handlers =>
    [
        .. _result.Graph.Artifacts.Where(_ =>
            _.Key.Kind == ArtifactKind.Handler &&
            _.Variants.Any(variant => variant.Definition.Name.StartsWith("Handler.", StringComparison.Ordinal)))
    ];

    string HandlerName(string methodName) => HandlerNames(methodName).Single();
    IReadOnlyList<string> HandlerNames(string methodName) =>
    [
        .. Handlers
            .SelectMany(_ => _.Variants)
            .Select(_ => _.Definition.Name)
            .Where(_ => _.StartsWith($"Handler.{methodName}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
    ];
    IReadOnlyList<string> HandlerSubjects(string methodName) =>
    [
        .. Handlers
            .Where(_ => _.Variants.Any(variant => variant.Definition.Name.StartsWith($"Handler.{methodName}", StringComparison.Ordinal)))
            .Select(_ => _.Key.Subject.Value)
    ];
}
