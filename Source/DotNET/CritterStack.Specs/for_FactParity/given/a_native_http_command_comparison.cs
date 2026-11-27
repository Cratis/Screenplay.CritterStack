// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using static Cratis.CritterStack.Screenplay.for_FactParity.given.fact_vectors;
using diagnostic_parity = Cratis.CritterStack.Screenplay.given.diagnostic_parity;

namespace Cratis.CritterStack.Screenplay.for_FactParity.given;

/// <summary>
/// Compares the native effect-free HTTP command fragment, run through the real Wolverine descriptor, identity and probe in the public
/// described-roster generator, to the compatibility oracle with full diagnostics.
/// </summary>
public abstract class a_native_http_command_comparison : Specification
{
    protected const string Framework =
        """
        #nullable enable
        namespace Wolverine
        {
            public sealed class WolverineOptions;
            public class DeliveryOptions;
            public interface IMessageBus
            {
                System.Threading.Tasks.ValueTask PublishAsync<T>(T message);
                System.Threading.Tasks.ValueTask SendAsync<T>(T message, DeliveryOptions? options = null);
            }
            public sealed class WolverineIgnoreAttribute : System.Attribute;
        }
        namespace Wolverine.Persistence.EventSourcing
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class ReadModelAttribute : System.Attribute;
        }
        namespace Wolverine.Http
        {
            public sealed class WolverinePostAttribute(string route) : System.Attribute;
            public sealed class WolverinePutAttribute(string route) : System.Attribute;
            public sealed class WolverineDeleteAttribute(string route) : System.Attribute;
        }
        """;

    /// <summary>The Marten surface a delete endpoint needs, added after <see cref="Framework"/> by the Marten comparisons.</summary>
    protected const string MartenFramework =
        """
        namespace Marten
        {
            public interface IDocumentStore;
            public sealed class StoreOptions { public MartenRegistry Schema { get; } = new(); }
            public sealed class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();
                public sealed class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> Identity(System.Linq.Expressions.Expression<System.Func<T, object>> member) => this;
                }
            }
            public interface IQuerySession
            {
                T? Load<T>(System.Guid id) where T : class;
                System.Threading.Tasks.Task<T?> LoadAsync<T>(System.Guid id, System.Threading.CancellationToken token = default) where T : class;
                System.Collections.Generic.IReadOnlyList<T> LoadMany<T>(params System.Guid[] ids) where T : class;
                System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<T>> LoadManyAsync<T>(params System.Guid[] ids) where T : class;
                System.Linq.IQueryable<T> Query<T>();
            }
            public interface IDocumentOperations : IQuerySession
            {
                void Store<T>(System.Collections.Generic.IEnumerable<T> entities) where T : notnull;
                void Store<T>(params T[] entities) where T : notnull;
                void Insert<T>(params T[] documents);
                void Update<T>(params T[] documents);
                void Delete<T>(T entity) where T : notnull;
                void Delete<T>(int id) where T : notnull;
                void Delete<T>(long id) where T : notnull;
                void Delete<T>(object id) where T : notnull;
                void Delete<T>(System.Guid id) where T : notnull;
                void Delete<T>(string id) where T : notnull;
                void DeleteWhere<T>(System.Linq.Expressions.Expression<System.Func<T, bool>> expression) where T : notnull;
                void DeleteObjects(System.Collections.Generic.IEnumerable<object> documents);
            }
            public interface IDocumentSession : IDocumentOperations
            {
                Events.IEventStoreOperations Events { get; }
                void SaveChanges();
                System.Threading.Tasks.Task SaveChangesAsync(System.Threading.CancellationToken token = default);
            }
            public static class MartenServiceCollectionExtensions
            {
                public static void AddMartenStore<T>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<StoreOptions> configure) where T : class, IDocumentStore { }
            }
        }
        namespace Marten
        {
            public static class QueryableExtensions
            {
                public static System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<T>> ToListAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<T> FirstAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<T?> FirstOrDefaultAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<T> SingleAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<T?> SingleOrDefaultAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<int> CountAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<long> LongCountAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
                public static System.Threading.Tasks.Task<bool> AnyAsync<T>(this System.Linq.IQueryable<T> query, System.Threading.CancellationToken token = default) => throw new System.NotSupportedException();
            }
        }
        namespace JasperFx.Events
        {
            public class StreamAction;
            public interface IEvent<T>;
            public interface IQueryEventStore
            {
                System.Threading.Tasks.Task<IEvent<T>?> LoadAsync<T>(System.Guid id, System.Threading.CancellationToken token = default) where T : class;
            }
        }
        namespace Marten.Events
        {
            public interface IEventStoreOperations : JasperFx.Events.IQueryEventStore
            {
                JasperFx.Events.StreamAction Append(System.Guid stream, params object[] events);
            }
        }
        namespace Wolverine.Marten
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
            public class MartenStoreAttribute(System.Type storeType) : System.Attribute;
        }
        """;

    protected GeneratedScreenplayDefinition Expected { get; private set; } = null!;

    /// <summary>Gets the real native Marten contribution composed with the native Wolverine contribution, Marten first.</summary>
    protected GeneratedScreenplayDefinition Composed { get; private set; } = null!;

    /// <summary>Gets the same composition with the Wolverine contribution first.</summary>
    protected GeneratedScreenplayDefinition ComposedReverse { get; private set; } = null!;

    /// <summary>Gets the public described roster, Marten first, admitting both real producers.</summary>
    protected GeneratedScreenplayDefinition PublicComposed { get; private set; } = null!;

    /// <summary>Gets the public described roster, Wolverine bridge first.</summary>
    protected GeneratedScreenplayDefinition PublicComposedReverse { get; private set; } = null!;

    protected GeneratedScreenplayDefinition Public { get; private set; } = null!;

    protected AdapterContribution Native { get; private set; } = null!;

    protected AdapterContribution NativeMarten { get; private set; } = null!;

    protected AdapterContribution Oracle { get; private set; } = null!;

    protected DotNetProjectCompilation ComparedProject { get; private set; } = null!;

    protected AdapterIdentity MartenIdentity { get; private set; } = null!;

    protected AdapterContributionAdmissionResult Admission { get; private set; } = null!;

    protected AdapterIdentity ExpectedIdentity { get; private set; } = null!;

    protected void Compare(string application)
    {
        var project = WithSourceContext(Project($"{Framework}\n{application}"));
        var context = new DotNetAnalysisContext([project]);
        var bridge = new real_wolverine_descriptor_bridge();
        Native = bridge.Analyze(context, new());
        Admission = AdapterContributionAdmission.Admit(bridge.Descriptor, Native);
        ExpectedIdentity = bridge.Identity;

        // TEST-ONLY oracle snapshot. The legacy oracle is not runner-admissible (its marten:/wolverine: fact ids lack the producer prefix), so its
        // unchanged contribution is wrapped into an admitted snapshot through the public AdapterRuns API and generated through the same snapshot
        // overload as the native side. This does not prove legacy runner admission; facts and diagnostics are not modified.
        var legacy = new CritterStackScreenplayAdapter();
        var oracle = legacy.Analyze(context, new());
        Expected = new ScreenplayDefinitionGenerator().Generate(
            LegacyOracleSnapshot(bridge.Descriptor with { Identity = legacy.Identity }, oracle),
            new() { Domain = "FactParity" });
        Public = new CritterStackScreenplayGenerator([bridge]).Generate([project], new CritterStackScreenplayOptions { Domain = "FactParity" });
    }

    protected void CompareWithMarten(string application) =>
        CompareWithMarten(Project($"{Framework}\n{MartenFramework}\n{application}"));

    /// <summary>Compares an application spread over several source files; the framework surface lives in its own file so the application files share coordinates.</summary>
    /// <param name="files">The application source files with the physical path each is parsed under.</param>
    protected void CompareWithMartenFiles(params (string Source, string Path)[] files) =>
        CompareWithMartenFiles(DotNetSourceDisplayRoot.Workspace, files);

    protected void CompareWithMartenFiles(DotNetSourceDisplayRoot displayRoot, params (string Source, string Path)[] files)
    {
        var project = Project(
        [
            CSharpSyntaxTree.ParseText($"{Framework}\n{MartenFramework}", path: "/workspace/FactParity/Framework.cs"),
            .. files.Select(file => CSharpSyntaxTree.ParseText(file.Source, path: file.Path))
        ]);
        var authored = project.AuthoredSyntaxTrees.Where(tree => !DotNetGeneratedSource.IsGenerated(tree)).ToHashSet();
        CompareWithMarten(WithDisplayRoot(project with { AuthoredSyntaxTrees = authored }, displayRoot));
    }

    void CompareWithMarten(DotNetProjectCompilation compilation)
    {
        var project = WithSourceContext(compilation);
        var context = new DotNetAnalysisContext([project]);
        var bridge = new real_wolverine_descriptor_bridge();
        var marten = new MartenScreenplayAdapter();
        ComparedProject = project;
        MartenIdentity = marten.Identity;
        NativeMarten = marten.Analyze(context, new());
        Native = bridge.Analyze(context, new());
        Admission = AdapterContributionAdmission.Admit(bridge.Descriptor, Native);
        ExpectedIdentity = bridge.Identity;

        // Oracle and every native composition run the SAME snapshot pipeline, so the SDK's fact-disposition diagnostics are accounted
        // identically on both sides and every diagnostic is compared in full.
        var adapterOptions = new DotNetAdapterOptions();
        var generation = new ScreenplayGenerationOptions { Domain = "FactParity" };
        var generator = new ScreenplayDefinitionGenerator();
        GeneratedScreenplayDefinition Snapshot(params DotNetAdapterRegistration[] roster) =>
            generator.Generate(DotNetAdapterRunner.Run(roster, context, adapterOptions), generation);

        // TEST-ONLY oracle snapshot (see Compare): the legacy contribution is wrapped unchanged through the shared helper.
        var legacy = new CritterStackScreenplayAdapter();
        Oracle = legacy.Analyze(context, adapterOptions);
        Expected = generator.Generate(LegacyOracleSnapshot(bridge.Descriptor with { Identity = legacy.Identity }, Oracle), generation);
        Composed = Snapshot(DotNetAdapterRegistration.For(marten), DotNetAdapterRegistration.For(bridge));
        ComposedReverse = Snapshot(DotNetAdapterRegistration.For(bridge), DotNetAdapterRegistration.For(marten));
        var options = new CritterStackScreenplayOptions { Domain = "FactParity" };
        PublicComposed = new CritterStackScreenplayGenerator([marten, bridge]).Generate([project], options);
        PublicComposedReverse = new CritterStackScreenplayGenerator([bridge, marten]).Generate([project], options);
        Public = PublicComposed;
    }

    /// <summary>Gets a value indicating whether the real runner admitted every record of the public result, including the Wolverine execution.</summary>
    protected bool IsAdmitted => Admitted(Public, ExpectedIdentity);

    protected bool Admitted(GeneratedScreenplayDefinition result) => Admitted(result, ExpectedIdentity);

    protected static bool Admitted(GeneratedScreenplayDefinition result, AdapterIdentity expected) =>
        result.AdapterRun is { Adapters.Length: > 0 } run
        && run.Adapters.Any(record => record.Descriptor.Identity == expected && record.Executed)
        && run.Adapters.All(record => record.Considered && record.Probed && record.Disposition == AdapterRunDisposition.Admitted && record.Execution is AdapterExecutionCompleted)
        && result.Diagnostics.All(diagnostic => diagnostic.Code != "DOTNETADAPTER007");

    /// <summary>Determines whether the result carries neither an unplaced-artifact nor an omitted-relationship diagnostic.</summary>
    /// <param name="result">The generated result to inspect.</param>
    /// <returns>True when neither diagnostic is present.</returns>
    protected static bool HasNoPlacementOrRelationshipDiagnostics(GeneratedScreenplayDefinition result) =>
        result.Diagnostics.All(diagnostic => diagnostic.Code is not ("GEN0004" or "GEN0039"));

    protected static AdapterRunSnapshot LegacyOracleSnapshot(AdapterDescriptor descriptor, AdapterContribution contribution) =>
        LegacyOracleSnapshot([(descriptor, contribution)]);

    protected static AdapterRunSnapshot LegacyOracleSnapshot(IEnumerable<(AdapterDescriptor Descriptor, AdapterContribution Contribution)> contributions) =>
        legacy_oracle_snapshot.Create(contributions);

    static DotNetProjectCompilation WithSourceContext(DotNetProjectCompilation project) => project.SourceContext is null
        ? WithDisplayRoot(project, DotNetSourceDisplayRoot.Workspace)
        : project;

    static DotNetProjectCompilation WithDisplayRoot(DotNetProjectCompilation project, DotNetSourceDisplayRoot displayRoot) => project with
    {
        SourceContext = DotNetSourcePaths.Create(
            $"{project.Name}/{project.Name}",
            new() { Version = 1, DisplayRoot = displayRoot, CasePolicy = DotNetSourcePathCasePolicy.Ordinal },
            [.. project.AuthoredSyntaxTrees.Select(tree => new DotNetSourceDocument
            {
                SyntaxTree = tree,
                ProjectRelativePath = Path.GetFileName(tree.FilePath),
                WorkspaceRelativePath = Path.GetRelativePath(project.SourceRoot, tree.FilePath).Replace('\\', '/')
            })])
    };

    // Same single exclusion as a_marten_concept_comparison (decision-gap10-enum-concept-kind.md, Revision 2026-11-24): native binds a concept-typed
    // member to Concept; the compatibility reader leaves TargetArtifactKind null. Only that field is excluded; HasOnlyTheDocumentedConceptKindDelta pins its shape.
    protected static string Artifacts(GeneratedScreenplayDefinition result) => JsonSerializer.Serialize(Definitions(result)
        .Select(definition => definition with
        {
            Properties = [.. definition.Properties.Select(property => property with { Type = property.Type with { TargetArtifactKind = null } })]
        })
        .ToArray());

    // Every artifact comparison runs the gap10 guard together with the exclusion above, as the Marten helper does.
    protected bool ArtifactsMatchWithOnlyTheDocumentedDelta() => ArtifactsMatchWithOnlyTheDocumentedDelta(Public, Expected);

    protected static bool ArtifactsMatchWithOnlyTheDocumentedDelta(GeneratedScreenplayDefinition actual, GeneratedScreenplayDefinition expected) =>
        Artifacts(actual) == Artifacts(expected) && HasOnlyTheDocumentedConceptKindDelta(actual, expected);

    protected static bool HasOnlyTheDocumentedConceptKindDelta(GeneratedScreenplayDefinition actual, GeneratedScreenplayDefinition expected) => Types(actual)
        .Zip(Types(expected), (native, compatibility) => compatibility.TargetArtifactKind is null
            ? native.TargetArtifactKind == (native.Subject is null ? null : ArtifactKind.Concept)
            : native.TargetArtifactKind == compatibility.TargetArtifactKind)
        .All(_ => _);

    protected static bool NativeBindsConceptWhereCompatibilityIsNull(GeneratedScreenplayDefinition actual, GeneratedScreenplayDefinition expected) => Types(actual)
        .Zip(Types(expected))
        .Any(pair => pair.First.TargetArtifactKind == ArtifactKind.Concept && pair.Second.TargetArtifactKind is null);

    static IEnumerable<ArtifactDefinition> Definitions(GeneratedScreenplayDefinition result) => result.Graph.Artifacts
        .SelectMany(artifact => artifact.Variants)
        .Select(variant => variant.Definition);

    static IEnumerable<TypeReferenceDefinition> Types(GeneratedScreenplayDefinition result) => Definitions(result)
        .SelectMany(definition => definition.Properties)
        .Select(property => property.Type);

    protected static string Relationships(GeneratedScreenplayDefinition result) =>
        JsonSerializer.Serialize(result.Graph.Relationships.SelectMany(relationship => relationship.Definitions));

    protected static string[] Diagnostics(GeneratedScreenplayDefinition result) => diagnostic_parity.Meaning(result.Diagnostics);

    protected IEnumerable<ArtifactDefinition> Commands(GeneratedScreenplayDefinition result) =>
        result.Graph.Artifacts.SelectMany(artifact => artifact.Variants).Select(variant => variant.Definition).Where(_ => _.Key.Kind == ArtifactKind.Command);

    protected string[] Identifiers(GeneratedScreenplayDefinition result) =>
        [.. Commands(result).Select(command => $"{command.Name}:{string.Join(',', command.Properties.Where(_ => _.IsIdentifier).Select(_ => _.Name))}")];

    protected bool Declares(GenerationFactCapability capability) =>
        Native.Facts.Any(fact => fact switch
        {
            ArtifactMemberDeclarationFact => capability == GenerationFactCapability.ArtifactMemberDeclaration,
            ArtifactMemberTypeUseFact => capability == GenerationFactCapability.ArtifactMemberTypeUse,
            ArtifactMemberRoleFact => capability == GenerationFactCapability.ArtifactMemberRole,
            TypeUseBindingPolicyFact => capability == GenerationFactCapability.TypeUseBindingPolicy,
            EntryPointFact => capability == GenerationFactCapability.EntryPoint,
            ArtifactDeclarationFact => capability == GenerationFactCapability.ArtifactDeclaration,
            ArtifactPlacementFact => capability == GenerationFactCapability.ArtifactPlacement,
            RelationshipFact => capability == GenerationFactCapability.Relationship,
            _ => false
        });

    /// <summary>Exposes the real Wolverine descriptor, identity and probe; analysis is the native production fragment only.</summary>
    protected sealed class real_wolverine_descriptor_bridge : IDescribedDotNetScreenplayAdapter, IDotNetScreenplayAdapter
    {
        readonly WolverineScreenplayAdapter _real = new();

        public AdapterIdentity Identity => _real.Identity;

        public AdapterDescriptor Descriptor => _real.Descriptor;

        public bool CanAnalyze(DotNetAnalysisContext context) => _real.CanAnalyze(context);

        public AdapterProbeResult Probe(DotNetAnalysisContext context) => _real.Probe(context);

        public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options) =>
            WolverineFacts.EmitNativeHttpCommands(context, options, Identity);
    }
}
