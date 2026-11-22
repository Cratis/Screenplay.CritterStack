// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_store_scope_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions
            {
                public string? EventModelName { get; set; }
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
            public interface IQuerySession { System.Linq.IQueryable<T> Query<T>(); }
            public interface IDocumentSession : IQuerySession { Marten.Events.IEventStoreOperations Events { get; } void Store<T>(T document); }
            public static class MartenServiceCollectionExtensions
            {
                public static void AddMarten(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<StoreOptions> configure) { }
                public static void AddMarten(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, StoreOptions options) { }
                public static void AddMartenStore<T>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<StoreOptions> configure) where T : class, IDocumentStore { }
            }
        }
        namespace Marten.Events { public interface IEventStoreOperations { void Append(System.Guid id, params object[] events); } }
        namespace Marten.Ancillary
        {
            public interface IQuerySession<T> : Marten.IQuerySession { }
            public interface IDocumentSession<T> : Marten.IDocumentSession, IQuerySession<T> { }
        }
        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public sealed class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
        }
        namespace Wolverine.Persistence
        {
            public sealed class EntityAttribute : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public sealed class StorageAttribute(System.Type storeType) : System.Attribute { }
        }
        namespace Wolverine
        {
            public class WolverineOptions;
            public abstract class Saga { public System.Guid Id { get; set; } public void MarkCompleted() { } }
            public class IPolicies
            {
                public void UseAncillaryStorageFromNamespaceContaining<T>(System.Type storeType) { }
                public void UseAncillaryStorageFromNamespace(string namespaceName, System.Type storeType) { }
                public void UseAncillaryStorageFromAssemblyContaining<T>(System.Type storeType) { }
                public void UseAncillaryStorageFromAssembly(System.Reflection.Assembly assembly, System.Type storeType) { }
            }
        }
        namespace Wolverine.Marten
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public class MartenStoreAttribute(System.Type storeType) : System.Attribute;
        }
        namespace Marten.Events.Projections
        {
            public class ProjectionOptions
            {
                public void CompositeProjectionFor(string name, System.Action<CompositeProjection>? configure = null) { }
                public void Add<T>() { }
                public void Add(object projection) { }
                public void Snapshot<T>() { }
            }
            public class CompositeProjection
            {
                internal CompositeProjection() { }
                public void Snapshot<T>(int stageNumber = 1) { }
                public void Add(object projection, int stageNumber = 1) { }
            }
        }
        namespace Marten.Events.Projections.Flattened
        {
            public class FlatTableProjection(string tableName, SchemaNameSource schemaNameSource);
            public enum SchemaNameSource { Explicit }
        }
        """;

    static readonly ConcurrentDictionary<string, Lazy<DotNetProjectCompilation>> _projects = new(StringComparer.Ordinal);

    protected static DotNetProjectCompilation ProjectFor(string source) => _projects.GetOrAdd(source, static text => new(() => CreateProject(text))).Value;

    protected static AdapterContribution Analyze(DotNetProjectCompilation project) => new MartenScreenplayAdapter().Analyze(
        new DotNetAnalysisContext([project]), new DotNetAdapterOptions());

    static DotNetProjectCompilation CreateProject(string source)
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(source, path: "/workspace/StoreScope/Configuration.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var frameworkCompilation = CSharpCompilation.Create("StoreScope.Framework", [framework], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var image = new MemoryStream();
        frameworkCompilation.Emit(image).Success.ShouldBeTrue();
        return new DotNetProjectCompilation
        {
            Name = "StoreScope",
            ProjectPath = "/workspace/StoreScope/StoreScope.csproj",
            SourceRoot = "/workspace",
            SourceContext = DotNetSourcePaths.Create(
                "StoreScope/StoreScope",
                new DotNetSourcePathPolicy
                {
                    DisplayRoot = DotNetSourceDisplayRoot.Workspace,
                    CasePolicy = DotNetSourcePathCasePolicy.Ordinal
                },
                [
                    new DotNetSourceDocument
                    {
                        SyntaxTree = application,
                        ProjectRelativePath = "Configuration.cs",
                        WorkspaceRelativePath = "StoreScope/Configuration.cs"
                    }
                ]),
            Compilation = CSharpCompilation.Create("StoreScope", [application], [.. references, MetadataReference.CreateFromImage(image.ToArray())], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { application }
        };
    }
}
