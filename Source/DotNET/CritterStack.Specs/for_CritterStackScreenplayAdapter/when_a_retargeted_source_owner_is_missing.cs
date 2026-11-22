// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_a_retargeted_source_owner_is_missing : Specification
{
    DotNetProjectCompilation _project = null!;
    CritterStackSubjectResolver _subjects = null!;
    INamedTypeSymbol _model = null!;
    AdapterContribution _contribution = null!;
    bool _blocked;

    void Establish()
    {
        MetadataReference[] platformReferences = [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))];
        var domain = Compile("Domain", "namespace Domain; public class OrderSummary : Shared.Base;", [.. platformReferences, SharedReference("1.0.0.0", platformReferences)]);
        var reference = domain.ToMetadataReference();
        var consumer = Compile(
            "Application",
            """
            namespace Wolverine { public class WolverineOptions; }
            namespace Wolverine.Http
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class WolverineGetAttribute(string route) : System.Attribute;
            }
            namespace Application
            {
                public static class OrderEndpoints
                {
                    [Wolverine.Http.WolverineGet("/orders")]
                    public static Domain.OrderSummary GetOrder() => new();
                    [Wolverine.Http.WolverineGet("/orders/find")]
                    public static Domain.OrderSummary FindOrder() => new();
                }
            }
            """,
            [.. platformReferences, SharedReference("2.0.0.0", platformReferences), reference]);

        // A changed dependency forces Roslyn to bind Domain through a retargeted assembly symbol.
        var boundAssembly = consumer.GetAssemblyOrModuleSymbol(reference);
        boundAssembly.ShouldNotBeNull();
        ReferenceEquals(boundAssembly, domain.Assembly).ShouldBeFalse();
        _model = consumer.GetTypeByMetadataName("Domain.OrderSummary");
        SymbolEqualityComparer.Default.Equals(boundAssembly, _model.ContainingAssembly).ShouldBeTrue();
        SymbolEqualityComparer.Default.Equals(domain.Assembly, _model.ContainingAssembly).ShouldBeFalse();
        _project = new DotNetProjectCompilation
        {
            Name = "Application",
            ProjectPath = "/workspace/Application/Application.csproj",
            SourceRoot = "/workspace",
            SourceContext = DotNetSourcePaths.Create(
                "Application/Application",
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                [new DotNetSourceDocument
                {
                    SyntaxTree = consumer.SyntaxTrees.Single(),
                    ProjectRelativePath = "Source.cs",
                    WorkspaceRelativePath = "Application/Source.cs"
                }]),
            Compilation = consumer,
            AuthoredSyntaxTrees = consumer.SyntaxTrees.ToHashSet()
        };
        _subjects = new(new([_project]));
    }

    void Because()
    {
        var checkpoint = _subjects.Checkpoint();
        _ = _subjects.SubjectForType(_project, _model);
        _ = _subjects.SubjectForType(_project, _model);
        _blocked = _subjects.HasBlockingDiagnosticsSince(checkpoint);
        _contribution = new CritterStackScreenplayAdapter().Analyze(new([_project]), new DotNetAdapterOptions());
    }

    [Fact] void should_block_uses_of_the_missing_source_owner() => _blocked.ShouldBeTrue();
    [Fact] void should_deduplicate_the_resolver_ownership_diagnostic() => _subjects.Diagnostics.Count.ShouldEqual(1);
    [Fact] void should_report_exactly_one_ownership_diagnostic() => _contribution.Diagnostics.Count(_ => _.Code == DotNetSourceStructureDiagnosticCodes.MissingSourceMapping).ShouldEqual(1);
    [Fact] void should_report_missing_ownership_as_an_error() => _contribution.Diagnostics.Single(_ => _.Code == DotNetSourceStructureDiagnosticCodes.MissingSourceMapping).Severity.ShouldEqual(GenerationDiagnosticSeverity.Error);
    [Fact] void should_report_missing_ownership_as_unknown() => _contribution.Diagnostics.Single(_ => _.Code == DotNetSourceStructureDiagnosticCodes.MissingSourceMapping).Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unknown);
    [Fact] void should_omit_all_project_facts() => _contribution.Facts.ShouldBeEmpty();

    static CSharpCompilation Compile(string name, string source, IReadOnlyList<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source, path: $"/workspace/{name}/Source.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return compilation;
    }

    static PortableExecutableReference SharedReference(string version, IReadOnlyList<MetadataReference> platformReferences)
    {
        var compilation = Compile(
            "Shared",
            $$"""
            [assembly: System.Reflection.AssemblyVersion("{{version}}")]
            namespace Shared; public class Base;
            """,
            platformReferences);
        using var stream = new MemoryStream();
        compilation.Emit(stream).Success.ShouldBeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
