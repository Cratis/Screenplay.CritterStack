// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_project_and_syntax_tree_order_is_reversed : given.a_shared_source_placement_application
{
    const string SecondaryFrameworkSource =
        """
        namespace Wolverine;

        public class WolverineOptions;
        """;

    const string SecondaryMessagesSource =
        """
        namespace Payments.Billing.Capture;

        public record CapturePayment(System.Guid PaymentId);
        public record PaymentCaptured(System.Guid PaymentId);
        public class Payment;
        """;

    const string SecondaryHandlerSource =
        """
        namespace Payments.Billing.Capture;

        public static class PaymentAggregateHandler
        {
            public static PaymentCaptured Handle(CapturePayment command, Payment payment) => new(command.PaymentId);
        }
        """;

    const string CorroboratingAdapterId = "test.corroborating";
    const string CritterStackAdapterId = "cratis.critter-stack";

    static readonly JsonSerializerOptions _canonicalJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        DictionaryKeyPolicy = null,
        Encoder = JavaScriptEncoder.Default,
        IncludeFields = false,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNamingPolicy = null,
        PropertyNameCaseInsensitive = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = false
    };

    GenerationVector _baseline = null!;
    SubjectId _primaryCommand = null!;
    SubjectId _primaryEvent = null!;
    GenerationVector _relocated = null!;
    GenerationVector _reversedProjects = null!;
    GenerationVector _reversedProviders = null!;
    GenerationVector _reversedTrees = null!;
    SubjectId _secondaryCommand = null!;
    SubjectId _secondaryEvent = null!;

    void Because()
    {
        var options = new CritterStackScreenplayOptions
        {
            Domain = "Ordering",
            FeatureRoot = "Source",
            NamespaceSegmentsToSkip = 1
        };
        var secondaryProject = CreateSecondaryProject();
        _primaryCommand = Project.SubjectForType(Project.Compilation.GetTypeByMetadataName("Application.Orders.Submit.SubmitOrder")!);
        _primaryEvent = Project.SubjectForType(Project.Compilation.GetTypeByMetadataName("Application.Orders.Submit.OrderSubmitted")!);
        _secondaryCommand = secondaryProject.SubjectForType(secondaryProject.Compilation.GetTypeByMetadataName("Payments.Billing.Capture.CapturePayment")!);
        _secondaryEvent = secondaryProject.SubjectForType(secondaryProject.Compilation.GetTypeByMetadataName("Payments.Billing.Capture.PaymentCaptured")!);
        _baseline = Generate([Project, secondaryProject], options);
        _reversedProjects = Generate([secondaryProject, Project], options);
        _reversedProviders = Generate([Project, secondaryProject], options, reverseProviders: true);
        _reversedTrees = Generate(
            [CreateProject(reverseTrees: true), CreateSecondaryProject(reverseTrees: true)],
            options);
        _relocated = Generate(
            [CreateProject(physicalRoot: "/relocated"), CreateSecondaryProject(physicalRoot: "/relocated")],
            options);
    }

    [Fact] void should_succeed_in_every_order() => Vectors.All(_ => _.Result.IsSuccess).ShouldBeTrue();
    [Fact] void should_include_the_primary_projects_command_and_event_facts() => new[] { _primaryCommand, _primaryEvent }.All(subject => _baseline.Result.Graph.Artifacts.Any(artifact => artifact.Key.Subject == subject)).ShouldBeTrue();
    [Fact] void should_include_the_secondary_projects_independent_command_and_event_facts() => new[] { _secondaryCommand, _secondaryEvent }.All(subject => _baseline.Result.Graph.Artifacts.Any(artifact => artifact.Key.Subject == subject)).ShouldBeTrue();
    [Fact] void should_receive_substantive_facts_from_both_providers_in_every_order() => Vectors.All(_ => _.CritterStackFactCount > 0 && _.CorroboratingFactCount > 0).ShouldBeTrue();
    [Fact] void should_generate_identical_utf8_bytes_for_reversed_projects() => _reversedProjects.GeneratedBytes.SequenceEqual(_baseline.GeneratedBytes).ShouldBeTrue();
    [Fact] void should_generate_identical_utf8_bytes_for_reversed_providers() => _reversedProviders.GeneratedBytes.SequenceEqual(_baseline.GeneratedBytes).ShouldBeTrue();
    [Fact] void should_generate_identical_utf8_bytes_for_reversed_trees() => _reversedTrees.GeneratedBytes.SequenceEqual(_baseline.GeneratedBytes).ShouldBeTrue();
    [Fact] void should_generate_identical_utf8_bytes_after_relocation() => _relocated.GeneratedBytes.SequenceEqual(_baseline.GeneratedBytes).ShouldBeTrue();
    [Fact] void should_keep_the_complete_resolved_graph_order_stable_for_reversed_projects() => _reversedProjects.CanonicalGraph.ShouldEqual(_baseline.CanonicalGraph);
    [Fact] void should_keep_the_complete_resolved_graph_order_stable_for_reversed_providers() => _reversedProviders.CanonicalGraph.ShouldEqual(_baseline.CanonicalGraph);
    [Fact] void should_keep_the_complete_resolved_graph_order_stable_for_reversed_trees() => _reversedTrees.CanonicalGraph.ShouldEqual(_baseline.CanonicalGraph);
    [Fact] void should_keep_the_complete_resolved_graph_order_stable_after_relocation() => _relocated.CanonicalGraph.ShouldEqual(_baseline.CanonicalGraph);
    [Fact] void should_keep_the_complete_diagnostic_order_stable_for_reversed_projects() => _reversedProjects.CanonicalDiagnostics.ShouldEqual(_baseline.CanonicalDiagnostics);
    [Fact] void should_keep_the_complete_diagnostic_order_stable_for_reversed_providers() => _reversedProviders.CanonicalDiagnostics.ShouldEqual(_baseline.CanonicalDiagnostics);
    [Fact] void should_keep_the_complete_diagnostic_order_stable_for_reversed_trees() => _reversedTrees.CanonicalDiagnostics.ShouldEqual(_baseline.CanonicalDiagnostics);
    [Fact] void should_keep_the_complete_diagnostic_order_stable_after_relocation() => _relocated.CanonicalDiagnostics.ShouldEqual(_baseline.CanonicalDiagnostics);

    IReadOnlyList<GenerationVector> Vectors => [_baseline, _reversedProjects, _reversedProviders, _reversedTrees, _relocated];

    DotNetProjectCompilation CreateSecondaryProject(bool reverseTrees = false, string physicalRoot = "/workspace")
    {
        var sources = new List<(string Source, string ProjectPath)>
        {
            (SecondaryMessagesSource, "Source/Billing/Capture/Messages.cs"),
            (SecondaryHandlerSource, "Source/Billing/Capture/Handler.cs")
        };
        if (reverseTrees)
        {
            sources.Reverse();
        }

        var authoredTrees = sources
            .Select(source => CSharpSyntaxTree.ParseText(source.Source, path: $"{physicalRoot}/Payments/{source.ProjectPath}"))
            .ToArray();
        var frameworkTree = CSharpSyntaxTree.ParseText(SecondaryFrameworkSource, path: $"{physicalRoot}/Payments/Framework.cs");
        var compilation = CSharpCompilation.Create(
            "Payments",
            [frameworkTree, .. authoredTrees],
            Project.Compilation.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var documents = authoredTrees
            .Select((tree, index) => new DotNetSourceDocument
            {
                SyntaxTree = tree,
                ProjectRelativePath = sources[index].ProjectPath,
                WorkspaceRelativePath = $"Payments/{sources[index].ProjectPath}"
            })
            .ToArray();

        return new()
        {
            Name = "Payments",
            Role = DotNetProjectRole.Application,
            ProjectPath = $"{physicalRoot}/Payments/Payments.csproj",
            SourceContext = DotNetSourcePaths.Create(
                "Payments/Payments",
                new DotNetSourcePathPolicy
                {
                    Version = 1,
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                documents),
            Compilation = compilation,
            AuthoredSyntaxTrees = authoredTrees.ToHashSet<SyntaxTree>()
        };
    }

    static GenerationVector Generate(
        IReadOnlyList<DotNetProjectCompilation> projects,
        CritterStackScreenplayOptions options,
        bool reverseProviders = false)
    {
        var contributions = new List<AdapterContribution>();
        var corroboratingAdapter = new RecordingAdapter(new CorroboratingArtifactAdapter(), contributions);
        var critterStackAdapter = new RecordingAdapter(new CritterStackScreenplayAdapter(), contributions);
        IReadOnlyList<IDotNetScreenplayAdapter> adapters = reverseProviders
            ? [critterStackAdapter, corroboratingAdapter]
            : [corroboratingAdapter, critterStackAdapter];
        var result = new CritterStackScreenplayGenerator(adapters).Generate(projects, options);
        var critterStackContribution = contributions.Single(_ => _.Adapter.Id == CritterStackAdapterId);
        var corroboratingContribution = contributions.Single(_ => _.Adapter.Id == CorroboratingAdapterId);

        // The resolver owns every list's canonical order. Serializing the public graph directly preserves that order,
        // includes every public stable semantic and provenance field, and makes any permutation-induced ordering bug visible.
        return new(
            result,
            System.Text.Encoding.UTF8.GetBytes(result.Source),
            JsonSerializer.Serialize(result.Graph, _canonicalJsonOptions),
            JsonSerializer.Serialize(result.Diagnostics, _canonicalJsonOptions),
            critterStackContribution.Facts.Count,
            corroboratingContribution.Facts.Count);
    }

    sealed record GenerationVector(
        GeneratedScreenplayDefinition Result,
        byte[] GeneratedBytes,
        string CanonicalGraph,
        string CanonicalDiagnostics,
        int CritterStackFactCount,
        int CorroboratingFactCount);

    sealed class RecordingAdapter(
        IDotNetScreenplayAdapter adapter,
        List<AdapterContribution> contributions) : IDotNetScreenplayAdapter
    {
        public AdapterIdentity Identity => adapter.Identity;

        public bool CanAnalyze(DotNetAnalysisContext context) => adapter.CanAnalyze(context);

        public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
        {
            var contribution = adapter.Analyze(context, options);
            contributions.Add(contribution);

            return contribution;
        }
    }

    sealed class CorroboratingArtifactAdapter : IDotNetScreenplayAdapter
    {
        public AdapterIdentity Identity { get; } = new() { Id = CorroboratingAdapterId, Version = "1.0.0" };

        public bool CanAnalyze(DotNetAnalysisContext context) => context.Projects.Any(_ => _.Name == "Payments");

        public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
        {
            var project = context.Projects.Single(_ => _.Name == "Payments");
            var aggregateType = project.Compilation.GetTypeByMetadataName("Payments.Billing.Capture.Payment")!;
            var subject = project.SubjectForType(aggregateType);
            var evidence = DotNetSource.EvidenceFor(
                aggregateType,
                Identity,
                project,
                EvidenceStrength.Exact,
                "Corroborates the authored Payment aggregate.");

            return new()
            {
                Adapter = Identity,
                Facts =
                [
                    new ArtifactFact
                    {
                        Id = new FactId { Value = $"test:corroborating:aggregate:{subject.Value}" },
                        Subject = subject,
                        Definition = new ArtifactDefinition
                        {
                            Key = new ArtifactKey { Subject = subject, Kind = ArtifactKind.Aggregate },
                            Name = aggregateType.Name,
                            File = evidence.Source?.Path,
                            Properties = []
                        },
                        Evidence = evidence
                    }
                ]
            };
        }
    }
}
