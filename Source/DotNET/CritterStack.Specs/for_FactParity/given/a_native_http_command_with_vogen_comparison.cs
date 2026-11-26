// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.Screenplay.Generation.DotNet.Vogen;

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>
/// Composes the real Vogen concept adapter and the native effect-free Wolverine HTTP command fragment in both registration orders and compares
/// each to the compatibility composition, all through the snapshot generation overload.
/// </summary>
public abstract class a_native_http_command_with_vogen_comparison : a_native_http_command_comparison
{
    const string VogenSharedTypesSource =
        """
        namespace Vogen
        {
            public sealed class ValueObjectAttribute<T> : System.Attribute;
        }
        """;

    const string ApplicationSource =
        """
        namespace FactParity
        {
            [Vogen.ValueObject<string>]
            public partial struct CustomerCode;
            public sealed record Submit(System.Guid Id, CustomerCode Code, CustomerCode? ReferralCode);
            public static class SubmitEndpoint
            {
                [Wolverine.Http.WolverinePost("/submit")]
                public static void Post(Submit command) { }
            }
        }
        """;

    // Generated Vogen output only corroborates the authored declaration; it is not an authored tree.
    const string GeneratedSource =
        """
        namespace FactParity
        {
            [System.CodeDom.Compiler.GeneratedCode("Vogen", "8.0.7")]
            public partial struct CustomerCode
            {
                public string Value { get; }
            }
        }
        """;

    protected GeneratedScreenplayDefinition Oracle { get; private set; } = null!;

    protected GeneratedScreenplayDefinition VogenThenWolverine { get; private set; } = null!;

    protected GeneratedScreenplayDefinition WolverineThenVogen { get; private set; } = null!;

    protected AdapterContribution VogenContribution { get; private set; } = null!;

    protected AdapterContribution WolverineContribution { get; private set; } = null!;

    protected AdapterIdentity VogenIdentity { get; private set; } = null!;

    protected AdapterIdentity WolverineIdentity { get; private set; } = null!;

    protected GeneratedScreenplayDefinition[] Orders => [VogenThenWolverine, WolverineThenVogen];

    protected void ComposeInBothOrders()
    {
        var project = Project();
        var context = new DotNetAnalysisContext([project]);
        var options = new CritterStackScreenplayOptions { Domain = "FactParity" };
        var bridge = new real_wolverine_descriptor_bridge();
        var vogen = new VogenConceptScreenplayAdapter();
        WolverineIdentity = bridge.Identity;
        VogenIdentity = vogen.Identity;
        WolverineContribution = bridge.Analyze(context, new());
        VogenContribution = vogen.Analyze(context, new());

        // TEST-ONLY oracle snapshot: the established compatibility composition (bind to the independent concept, then drop missing ones) wrapped
        // into an admitted snapshot, because legacy marten:/wolverine: fact ids cannot pass real runner admission. Native admission stays real.
        var legacy = new CritterStackScreenplayAdapter();
        var composed = ConceptTypeReferenceBinder.WithoutMissingConcepts(
            ConceptTypeReferenceBinder.Bind(context, [legacy.Analyze(context, new()), VogenContribution]));
        Oracle = new ScreenplayDefinitionGenerator().Generate(
            LegacyOracleSnapshot([(bridge.Descriptor with { Identity = legacy.Identity }, composed[0]), (vogen.Descriptor, composed[1])]),
            new() { Domain = "FactParity" });

        VogenThenWolverine = new CritterStackScreenplayGenerator([vogen, bridge]).Generate([project], options);
        WolverineThenVogen = new CritterStackScreenplayGenerator([bridge, vogen]).Generate([project], options);
    }

    protected bool BothProducersAdmitted(GeneratedScreenplayDefinition result) =>
        result.AdapterRun is { Adapters.Length: 2 }
        && Admitted(result, WolverineIdentity)
        && Admitted(result, VogenIdentity);

    protected bool MatchesTheOracle(GeneratedScreenplayDefinition result) =>
        ArtifactsMatchWithOnlyTheDocumentedDelta(result, Oracle)
        && Relationships(result) == Relationships(Oracle)
        && Diagnostics(result).SequenceEqual(Diagnostics(Oracle))
        && result.Source == Oracle.Source
        && result.IsSuccess == Oracle.IsSuccess;

    protected IEnumerable<PropertyDefinition> CommandProperties(GeneratedScreenplayDefinition result) =>
        Commands(result).SelectMany(command => command.Properties);

    static DotNetProjectCompilation Project()
    {
        var framework = CSharpSyntaxTree.ParseText($"{Framework}\n{ApplicationSource}", path: "/workspace/FactParity/Application.cs");
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        using var sharedTypes = new MemoryStream();
        CSharpCompilation.Create(
            "Vogen.SharedTypes",
            [CSharpSyntaxTree.ParseText(VogenSharedTypesSource)],
            platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).Emit(sharedTypes).Success.ShouldBeTrue();
        var compilation = CSharpCompilation.Create(
            "FactParity",
            [framework, CSharpSyntaxTree.ParseText(GeneratedSource, path: "/workspace/obj/Vogen.g.cs")],
            [.. platform, MetadataReference.CreateFromImage(sharedTypes.ToArray())],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return new()
        {
            Name = "FactParity",
            ProjectPath = "/workspace/FactParity/FactParity.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework },
            SourceContext = DotNetSourcePaths.Create(
                "FactParity/FactParity",
                new DotNetSourcePathPolicy { DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [new DotNetSourceDocument
                {
                    SyntaxTree = framework,
                    ProjectRelativePath = "Application.cs",
                    WorkspaceRelativePath = "FactParity/Application.cs"
                }])
        };
    }
}
