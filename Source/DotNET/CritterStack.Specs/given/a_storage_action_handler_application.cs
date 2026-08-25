// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_storage_action_handler_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;

            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine.Persistence
        {
            public interface IStorageAction<T>;

            public class UnitOfWork<T> : IStorageAction<T>
            {
                public void Store(T entity) { }
                public void Insert(T entity) { }
                public void Update(T entity) { }
                public void Delete(T entity) { }
            }

            public static class Storage
            {
                public static IStorageAction<T> Store<T>(T entity) => default!;
                public static IStorageAction<T> Insert<T>(T entity) => default!;
                public static IStorageAction<T> Update<T>(T entity) => default!;
                public static IStorageAction<T> Delete<T>(T entity) => default!;
                public static IStorageAction<T> Nothing<T>() => default!;
                public static IStorageAction<T> StartStream<T>() => default!;
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace StorageActionHandlers;

        public record ManifestPushed(string Id);
        public record CountsChanged(string Id, int Count);
        public record ManifestRemoved(string Id);
        public record ManifestUpdated(string Id);
        public record CustomStorageRequested(string Id);
        public record StreamRequested(string Id);
        public record MixedStorageActionsRequested(string Id);
        public record CountsRecalculated(string Id);

        public class ManifestDocument
        {
            public string Id { get; set; } = string.Empty;
        }

        public class CountState
        {
            public string Id { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        public sealed class CustomManifestStorageAction : Wolverine.Persistence.IStorageAction<ManifestDocument>;

        public static class ManifestPushedHandler
        {
            public static Wolverine.Persistence.IStorageAction<ManifestDocument> Handle(ManifestPushed message) =>
                Wolverine.Persistence.Storage.Store(new ManifestDocument { Id = message.Id });
        }

        public static class CountsChangedHandler
        {
            public static async System.Threading.Tasks.Task<(Wolverine.OutgoingMessages, Wolverine.Persistence.UnitOfWork<CountState>)> HandleAsync(CountsChanged message)
            {
                await System.Threading.Tasks.Task.CompletedTask;
                Wolverine.OutgoingMessages outgoing = [new CountsRecalculated(message.Id)];
                var actions = new Wolverine.Persistence.UnitOfWork<CountState>();
                actions.Update(new CountState { Id = message.Id, Count = message.Count });
                return (outgoing, actions);
            }
        }

        public static class ManifestRemovedHandler
        {
            public static Wolverine.Persistence.IStorageAction<ManifestDocument> Handle(ManifestRemoved message) =>
                Wolverine.Persistence.Storage.Delete(new ManifestDocument { Id = message.Id });
        }

        public static class ManifestUpdatedHandler
        {
            public static Wolverine.Persistence.IStorageAction<ManifestDocument> Handle(ManifestUpdated message) =>
                Wolverine.Persistence.Storage.Update(new ManifestDocument { Id = message.Id });
        }

        public static class CustomStorageRequestedHandler
        {
            public static CustomManifestStorageAction Handle(CustomStorageRequested message) => new();
        }

        public static class StreamRequestedHandler
        {
            public static Wolverine.Persistence.IStorageAction<ManifestDocument> Handle(StreamRequested message) =>
                Wolverine.Persistence.Storage.StartStream<ManifestDocument>();
        }

        public static class MixedStorageActionsRequestedHandler
        {
            public static (
                Wolverine.Persistence.IStorageAction<ManifestDocument>,
                Wolverine.Persistence.IStorageAction<ManifestDocument>,
                Wolverine.Persistence.IStorageAction<ManifestDocument>) Handle(MixedStorageActionsRequested message) =>
                (
                    Wolverine.Persistence.Storage.Store(new ManifestDocument { Id = message.Id }),
                    Wolverine.Persistence.Storage.Delete(new ManifestDocument { Id = message.Id }),
                    Wolverine.Persistence.Storage.Nothing<ManifestDocument>());
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "StorageActionHandlers",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/StorageActionHandlers/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "StorageActionHandlers",
            ProjectPath = "/workspace/StorageActionHandlers/StorageActionHandlers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
