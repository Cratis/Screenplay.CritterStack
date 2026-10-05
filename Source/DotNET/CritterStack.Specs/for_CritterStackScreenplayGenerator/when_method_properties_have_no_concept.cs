// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_method_properties_have_no_concept : Specification
{
    DotNetProjectCompilation _project = null!;
    GeneratedScreenplayDefinition _result = null!;
    GeneratedScreenplayDefinition _direct = null!;

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
                public class WolverineGetAttribute(string route) : Attribute;
                public class WolverinePostAttribute(string route) : Attribute;
            }
            namespace Marten.Linq
            {
                public interface IMartenQueryable<T> : IQueryable<T>;
                public interface ICompiledQuery<TDoc, TOut> { Expression<Func<IMartenQueryable<TDoc>, TOut>> QueryIs(); }
            }
            namespace Marten
            {
                public class StoreOptions;
                public interface IQuerySession { Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query); }
            }
            namespace Vogen { public class ValueObjectAttribute<T> : Attribute; }
            namespace Methods
            {
                [Flags] public enum Access { Read = 1, Write = 2 }
                public record MyRecord(string Name);
                public struct MyStruct { public int Value; }
                [Vogen.ValueObject<Guid>] public struct Identifier;
                public record Details(string Name);
                public class Plan : Marten.Linq.ICompiledQuery<Details, Details>
                {
                    public MyRecord Filter;
                    public Access Permissions;
                    public Identifier Identity;
                    public Expression<Func<Marten.Linq.IMartenQueryable<Details>, Details>> QueryIs() => query => query.First();
                }
                public static class DetailsEndpoints
                {
                    [Wolverine.Http.WolverineGet("/flags")]
                    public static Details Flags(Access access) => new("Flags");
                    [Wolverine.Http.WolverinePost("/flags")]
                    public static void ChangeFlags(Access access) { }
                    [Wolverine.Http.WolverineGet("/generated")]
                    public static Details Generated(GeneratedChoice choice) => new("Generated");
                    [Wolverine.Http.WolverineGet("/records")]
                    public static Details Records(MyRecord[] records) => new("Records");
                    [Wolverine.Http.WolverineGet("/structs")]
                    public static Details Structs(MyStruct? value) => new("Structs");
                    [Wolverine.Http.WolverineGet("/plan")]
                    public static Task<Details> Execute(Marten.IQuerySession session) => session.QueryAsync(new Plan());
                }
            }
            """,
            path: "/workspace/Methods/Endpoints.cs");
        var generated = CSharpSyntaxTree.ParseText("namespace Methods; public enum GeneratedChoice { One, Two }", path: "/workspace/obj/Choices.g.cs");
        _project = new DotNetProjectCompilation
        {
            Name = "Methods",
            ProjectPath = "/workspace/Methods/Methods.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Methods", [tree, generated], ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { tree }
        };
    }

    void Because()
    {
        _result = new CritterStackScreenplayGenerator([new CritterStackScreenplayAdapter()]).Generate([_project], new CritterStackScreenplayOptions { Domain = "Methods" });
        _direct = new ScreenplayDefinitionGenerator().Generate([new CritterStackScreenplayAdapter().Analyze(new([_project]), new())], new ScreenplayGenerationOptions { Domain = "Methods" });
    }

    [Fact] void should_compile_the_input() => _project.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_the_flags_query() => _result.Source.ShouldContain("query Flags");
    [Fact] void should_keep_the_flags_command() => _result.Source.ShouldContain("command ChangeFlags");
    [Fact] void should_keep_the_generated_enum_query() => _result.Source.ShouldContain("query Generated");
    [Fact] void should_keep_the_record_collection_query() => _result.Source.ShouldContain("query Records");
    [Fact] void should_keep_the_nullable_struct_query() => _result.Source.ShouldContain("query Structs");
    [Fact] void should_keep_the_query_with_record_and_unadapted_value_object_plan_fields() => _result.Source.ShouldContain("query Execute");
    [Fact] void should_not_reference_missing_concepts() => _result.Diagnostics.Where(diagnostic => diagnostic.Code == "GEN0016").ShouldBeEmpty();
    [Fact] void should_not_reference_missing_concepts_for_direct_adapter_consumers() => _direct.Diagnostics.Where(diagnostic => diagnostic.Code == "GEN0016").ShouldBeEmpty();
    [Fact] void should_not_bind_any_property_to_a_missing_concept() => _result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).SelectMany(variant => variant.Definition.Properties).Select(property => property.Type.Subject).OfType<SubjectId>().ShouldBeEmpty();
    [Fact] void should_preserve_collection_shape() => Property("Records", "records").Type.IsCollection.ShouldBeTrue();
    [Fact] void should_preserve_optionality() => Property("Structs", "value").Type.IsOptional.ShouldBeTrue();
    [Fact] void should_report_located_flags_loss() => _result.Diagnostics.Single(diagnostic => diagnostic.Code == CritterStackDiagnosticCodes.FlagsEnumOmitted).Source!.Path.ShouldEqual("Methods/Endpoints.cs");

    PropertyDefinition Property(string artifactName, string name) => _result.Graph.Artifacts.Single(artifact => artifact.Variants[0].Definition.Name == artifactName).Variants[0].Definition.Properties.Single(property => property.Name == name);
}
