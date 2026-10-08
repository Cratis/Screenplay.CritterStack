// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_query_parameters_start_with_an_enum : Specification
{
    DotNetProjectCompilation _project = null!;
    AdapterContribution _result = null!;

    void Establish()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            using System;
            namespace Wolverine { public class WolverineOptions; }
            namespace Wolverine.Http { public class WolverineGetAttribute(string route) : Attribute; }
            namespace Orders
            {
                public enum OrderStatus { Open, Closed }
                public record Details(string Name);
                public static class OrderEndpoints
                {
                    [Wolverine.Http.WolverineGet("/orders")]
                    public static Details Get(OrderStatus status, Guid customerId) => new("Order");
                    [Wolverine.Http.WolverineGet("/statuses")]
                    public static Details ByStatus(OrderStatus status) => new("Status");
                    [Wolverine.Http.WolverineGet("/days")]
                    public static Details ByDay(DayOfWeek day, Guid customerId) => new("Day");
                }
            }
            """,
            path: "/workspace/Orders/Endpoints.cs");
        _project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Orders", [tree], ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
    }

    void Because() => _result = new CritterStackScreenplayAdapter().Analyze(new([_project]), new());

    [Fact] void should_compile_the_input() => _project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_the_authored_enum_parameter() => Property("Get", "status").IsIdentifier.ShouldBeFalse();
    [Fact] void should_keep_the_identifier_on_the_first_non_enum_parameter() => Property("Get", "customerId").IsIdentifier.ShouldBeTrue();
    [Fact] void should_not_make_an_authored_enum_the_only_identifier() => Query("ByStatus").Definition.Properties.Any(property => property.IsIdentifier).ShouldBeFalse();
    [Fact] void should_keep_an_external_enum_identifier() => Property("ByDay", "day").IsIdentifier.ShouldBeTrue();
    [Fact] void should_mark_one_identifier_for_an_external_enum_query() => Query("ByDay").Definition.Properties.Count(property => property.IsIdentifier).ShouldEqual(1);

    ArtifactFact Query(string name) => _result.Facts.OfType<ArtifactFact>().Single(fact => fact.Definition.Key.Kind == ArtifactKind.Query && fact.Definition.Name == name);

    PropertyDefinition Property(string query, string name) => Query(query).Definition.Properties.Single(property => property.Name == name);
}
