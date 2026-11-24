// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>Independent source observations for contract vectors, never translated compatibility facts.</summary>
static class fact_vectors
{
    internal static readonly AdapterIdentity MartenAdapter = new() { Id = "marten-fact-vector", Version = "1.0.0" };
    internal static readonly AdapterIdentity WolverineAdapter = new() { Id = "wolverine-fact-vector", Version = "1.0.0" };

    internal static DotNetProjectCompilation Project(string source) =>
        Project([CSharpSyntaxTree.ParseText(source, path: "/workspace/FactParity/Application.cs")]);

    internal static DotNetProjectCompilation Project(IReadOnlyList<SyntaxTree> trees)
    {
        var compilation = CSharpCompilation.Create(
            "FactParity",
            trees,
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return new()
        {
            Name = "FactParity", ProjectPath = "/workspace/FactParity/FactParity.csproj", SourceRoot = "/workspace",
            SourceContext = DotNetSourcePaths.Create(
                "FactParity/FactParity",
                new() { Version = 1, DisplayRoot = DotNetSourceDisplayRoot.Workspace, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
                [.. trees.Select(tree => new DotNetSourceDocument
                {
                    SyntaxTree = tree, ProjectRelativePath = Path.GetFileName(tree.FilePath), WorkspaceRelativePath = $"FactParity/{Path.GetFileName(tree.FilePath)}"
                })]),
            Compilation = compilation, AuthoredSyntaxTrees = trees.ToHashSet()
        };
    }

    internal static INamedTypeSymbol Type(DotNetProjectCompilation project, string name) => project.Compilation.GetTypeByMetadataName(name)!;

    internal static ArtifactKey Key(DotNetProjectCompilation project, INamedTypeSymbol type, ArtifactKind kind) => new()
    {
        Subject = project.SubjectForType(type), Kind = kind
    };

    internal static Evidence Evidence(DotNetProjectCompilation project, ISymbol symbol, AdapterIdentity adapter, EvidenceStrength strength = EvidenceStrength.Exact) =>
        CritterStackSource.EvidenceFor(symbol, adapter, project, strength, "Independent source observation");

    internal static ArtifactDeclarationFact Declaration(ArtifactKey key, INamedTypeSymbol type, Evidence evidence, string suffix = "declaration") => new()
    {
        Id = new() { Value = $"{evidence.Adapter.Id}:{key.Subject.Value}:{suffix}" }, Subject = key.Subject, Evidence = evidence,
        Definition = new() { Artifact = key, Name = type.Name, File = evidence.Source?.Path }
    };

    internal static IReadOnlyList<GenerationFact> Surface(DotNetProjectCompilation project, INamedTypeSymbol type, ArtifactKey key, AdapterIdentity adapter)
    {
        var evidence = Evidence(project, type, adapter);
        return [Declaration(key, type, evidence), .. DotNetTypeUseFacts.Emit(type, key, new([project]), evidence)];
    }

    internal static ArtifactMemberRoleFact Role(ArtifactKey key, string member, Evidence evidence, string suffix = "role") => new()
    {
        Id = new() { Value = $"{evidence.Adapter.Id}:{key.Subject.Value}:{member}:{suffix}" }, Subject = key.Subject, Evidence = evidence,
        Definition = new() { Member = new() { Artifact = key, Name = member }, Role = ArtifactMemberRoleKind.EventSourceIdentifier }
    };

    internal static AdapterContribution Contribution(AdapterIdentity adapter, IReadOnlyList<GenerationFact> facts) => new() { Adapter = adapter, Facts = facts };

    internal static AdapterContribution Failure(DotNetProjectCompilation project, IMethodSymbol method)
    {
        var evidence = Evidence(project, method, WolverineAdapter);
        var warning = new GenerationDiagnostic
        {
            Code = WolverineDiagnosticCodes.FailureStageConditionOmitted, Severity = GenerationDiagnosticSeverity.Warning,
            Outcome = GenerationDiagnosticOutcome.Unsupported, Message = "Failure-only source observation", Subject = CritterStackMethodFacts.SubjectFor(project, method), Source = evidence.Source
        };
        return Contribution(WolverineAdapter, [CritterStackMethodFacts.FailureOnly(CritterStackMethodFacts.Observe(project, method), evidence, warning)]);
    }

    internal static GeneratedScreenplayDefinition Generate(params AdapterContribution[] contributions) =>
        new ScreenplayDefinitionGenerator().Generate(contributions, new() { Domain = "FactParity" });

    internal static ArtifactDefinition Definition(GeneratedScreenplayDefinition result, ArtifactKey key) =>
        result.Graph.Artifacts.Single(_ => _.Key == key).Variants.Single().Definition;

    internal static ArtifactDefinition CompatibilityDefinition(AdapterContribution contribution, ArtifactKey key) =>
        contribution.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key == key).Definition;

    internal static string Identifiers(ArtifactDefinition definition) => string.Join(',', definition.Properties.Where(_ => _.IsIdentifier).Select(_ => _.Name));

    internal static string Members(ArtifactDefinition definition) => System.Text.Json.JsonSerializer.Serialize(definition.Properties);
}
