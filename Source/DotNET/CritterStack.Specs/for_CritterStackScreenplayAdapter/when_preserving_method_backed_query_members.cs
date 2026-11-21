// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_preserving_method_backed_query_members : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace QueryMembers;
        public enum Mode { Fast, Slow }
        public record View(string Name);
        public static class ViewEndpoint
        {
            [Wolverine.Http.WolverineGet("/view/{orderId}")]
            public static View Get(System.Guid orderId, Mode mode, System.Guid other) => new("view");
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);

    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_the_parameter_order() => Properties.Select(_ => _.Name).ShouldContainOnly("orderId", "mode", "other");
    [Fact] void should_take_the_identifier_from_the_first_non_enum_parameter() => string.Join(",", Properties.Where(_ => _.IsIdentifier).Select(_ => _.Name)).ShouldEqual("orderId");

    IReadOnlyList<PropertyDefinition> Properties => _contribution.Facts
        .OfType<ArtifactFact>()
        .Single(_ => _.Definition.Key.Kind == ArtifactKind.Query)
        .Definition.Properties;
}
