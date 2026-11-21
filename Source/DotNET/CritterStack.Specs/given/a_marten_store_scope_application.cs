// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_store_scope_application : Specification
{
    const string FrameworkSource =
        """
        namespace Microsoft.Extensions.DependencyInjection { public interface IServiceCollection; }
        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions
            {
                public string? EventModelName { get; set; }
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
            public static class MartenServiceCollectionExtensions
            {
                public static void AddMartenStore<T>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<StoreOptions> configure) where T : class, IDocumentStore { }
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
        return new DotNetProjectCompilation
        {
            Name = "StoreScope",
            ProjectPath = "/workspace/StoreScope/StoreScope.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("StoreScope", [framework, application], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { application }
        };
    }
}
