// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_enums_are_used_by_method_backed_artifacts : Specification
{
    DotNetProjectCompilation _project = null!;
    GeneratedScreenplayDefinition _result = null!;

    void Establish()
    {
        var tree = CSharpSyntaxTree.ParseText(
            """
            using System;
            using System.Linq;
            using System.Linq.Expressions;
            using System.Threading.Tasks;
            namespace Wolverine { public class WolverineOptions; }
            namespace Wolverine.Http
            {
                public abstract class WolverineHttpMethodAttribute(string route) : Attribute;
                public class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
                public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
            }
            namespace Marten.Linq
            {
                public interface IMartenQueryable<T> : IQueryable<T>;
                public interface ICompiledQuery<TDoc, TOut>
                {
                    Expression<Func<IMartenQueryable<TDoc>, TOut>> QueryIs();
                }
            }
            namespace Marten
            {
                public class StoreOptions;
                public interface IQuerySession
                {
                    Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query);
                }
            }
            namespace Methods
            {
                public enum RouteChoice { First, Second }
                public enum QueryChoice { Low, High }
                public enum PlanChoice { Open, Closed }
                public enum ReturnChoice { Visible, Hidden }
                public record Details(ReturnChoice Visibility);
                public record PlainDetails(string Name);
                public class Plan : Marten.Linq.ICompiledQuery<PlainDetails, PlainDetails>
                {
                    public PlanChoice Selection;
                    public Expression<Func<Marten.Linq.IMartenQueryable<PlainDetails>, PlainDetails>> QueryIs() => query => query.First();
                }
                public static class ChoiceEndpoints
                {
                    [Wolverine.Http.WolverinePost("/choices/{choice}")]
                    public static void Choose(RouteChoice choice) { }
                    [Wolverine.Http.WolverineGet("/choices")]
                    public static Details Search(QueryChoice? choice, QueryChoice[] choices) => new(ReturnChoice.Visible);
                    [Wolverine.Http.WolverineGet("/plans")]
                    public static Task<PlainDetails> Execute(Marten.IQuerySession session) => session.QueryAsync(new Plan());
                }
            }
            """,
            path: "/workspace/Methods/Endpoints.cs");
        _project = new DotNetProjectCompilation
        {
            Name = "Methods",
            ProjectPath = "/workspace/Methods/Methods.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Methods", [tree], ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
    }

    void Because() => _result = new CritterStackScreenplayGenerator().Generate([_project], new CritterStackScreenplayOptions { Domain = "Methods" });

    [Fact] void should_compile_the_input() => _project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_generate_a_valid_candidate() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_discover_every_referenced_enum() => _result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.Concept).Select(artifact => artifact.Variants[0].Definition.Name).ShouldContainOnly(["RouteChoice", "QueryChoice", "PlanChoice", "ReturnChoice"]);
    [Fact] void should_bind_the_route_parameter() => Property("Choose", "choice").Type.Subject!.Value.ShouldContain(".RouteChoice");
    [Fact] void should_not_invent_an_enum_command() => _result.Graph.Artifacts.Where(artifact => artifact.Key.Kind == ArtifactKind.Command).Select(artifact => artifact.Variants[0].Definition.Name).ShouldContainOnly(["Choose"]);
    [Fact] void should_bind_the_nullable_query_parameter() => Property("Search", "choice").Type.Subject!.Value.ShouldContain(".QueryChoice");
    [Fact] void should_preserve_parameter_optionality() => Property("Search", "choice").Type.IsOptional.ShouldBeTrue();
    [Fact] void should_bind_the_collection_query_parameter() => Property("Search", "choices").Type.Subject!.Value.ShouldContain(".QueryChoice");
    [Fact] void should_preserve_parameter_collection_shape() => Property("Search", "choices").Type.IsCollection.ShouldBeTrue();
    [Fact] void should_bind_compiled_plan_fields() => Property("Execute", "selection").Type.Subject!.Value.ShouldContain(".PlanChoice");
    [Fact] void should_not_leave_undeclared_field_types() => new global::Cratis.Screenplay.ScreenplayCompiler().Compile(_result.Source).Diagnostics.Any(diagnostic => diagnostic.Code == "PLAY0165").ShouldBeFalse();

    PropertyDefinition Property(string artifactName, string propertyName) => _result.Graph.Artifacts.Single(artifact => artifact.Variants[0].Definition.Name == artifactName).Variants[0].Definition.Properties.Single(property => property.Name == propertyName);
}
