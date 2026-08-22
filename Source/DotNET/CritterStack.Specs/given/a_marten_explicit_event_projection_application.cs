// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_explicit_event_projection_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Events
        {
            public interface IEvent
            {
                object Data { get; }
            }
        }

        namespace JasperFx.Events.Projections
        {
            public interface IProjection;
            public class AsyncOptions
            {
                public void DeleteViewTypeOnTeardown<T>() { }
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public interface IDocumentOperations
            {
                void Store<T>(params T[] documents);
                void Insert<T>(params T[] documents);
                void Update<T>(params T[] documents);
                void Delete<T>(System.Guid id);
                void DeleteWhere<T>(System.Linq.Expressions.Expression<System.Func<T, bool>> predicate);
            }
            public class StoreOptions;
        }

        namespace Marten.Events.Projections
        {
            public abstract class EventProjection : JasperFx.Events.Projections.IProjection
            {
                public JasperFx.Events.Projections.AsyncOptions Options { get; } = new();
                public virtual System.Threading.Tasks.ValueTask ApplyAsync(
                    Marten.IDocumentOperations operations,
                    JasperFx.Events.IEvent e,
                    System.Threading.CancellationToken cancellation) => default;
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace Imports;

        public record Imported(System.Guid Id);
        public record Removed(System.Guid Id);
        public record ArbitraryValue(System.Guid Id);

        public class ImportView
        {
            public System.Guid Id { get; set; }
        }

        public class ImportStatus
        {
            public System.Guid Id { get; set; }
        }

        public class ImportAudit
        {
            public System.Guid Id { get; set; }
        }

        public class TeardownOnly
        {
            public System.Guid Id { get; set; }
        }

        public class HiddenDocument
        {
            public System.Guid Id { get; set; }
        }

        public partial class ImportProjection : Marten.Events.Projections.EventProjection
        {
            public ImportProjection()
            {
                Options.DeleteViewTypeOnTeardown<ImportView>();
                Options.DeleteViewTypeOnTeardown<TeardownOnly>();
            }

            public override System.Threading.Tasks.ValueTask ApplyAsync(
                Marten.IDocumentOperations operations,
                JasperFx.Events.IEvent e,
                System.Threading.CancellationToken cancellation)
            {
                switch (e.Data)
                {
                    case Imported imported:
                        operations.Store(new ImportView { Id = imported.Id });
                        operations.Update(new ImportStatus { Id = imported.Id });
                        break;
                    case Removed removed:
                        operations.DeleteWhere<ImportView>(_ => true);
                        operations.Delete<ImportAudit>(removed.Id);
                        break;
                }

                return default;
            }

            public void Arbitrary(object value, Marten.IDocumentOperations operations)
            {
                switch (value)
                {
                    case ArbitraryValue:
                        operations.Insert(new HiddenDocument());
                        break;
                }
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "Imports",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Imports/ImportProjection.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var project = new DotNetProjectCompilation
        {
            Name = "Imports",
            ProjectPath = "/workspace/Imports/Imports.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
