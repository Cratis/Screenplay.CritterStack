// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_marten_ops_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public interface ISideEffect;
        }
        namespace Wolverine.Marten
        {
            public interface IMartenOp : Wolverine.ISideEffect;
            public interface IStartStream : IMartenOp;
            public class DocumentOp<T> : IMartenOp;
            public class StartStream<T> : IStartStream;
            public static class MartenOps
            {
                public static DocumentOp<T> Store<T>(T document, string? tenantId = null) => new();
                public static DocumentOp<T> Insert<T>(T document, string? tenantId = null) => new();
                public static DocumentOp<T> Update<T>(T document, string? tenantId = null) => new();
                public static DocumentOp<T> Delete<T>(T document, string? tenantId = null) => new();
                public static DocumentOp<T> Delete<T>(System.Guid id, string? tenantId = null) => new();
                public static DocumentOp<T> StoreMany<T>(params T[] documents) => new();
                public static DocumentOp<T> StoreMany<T>(string tenantId, params T[] documents) => new();
                public static StartStream<T> StartStream<T>(System.Guid streamId, params object[] events) => new();
                public static StartStream<T> StartStream<T>(System.Guid streamId, string tenantId, params object[] events) => new();
            }
        }
        """;

    const string ApplicationSource =
        """
        using System.Threading.Tasks;
        using Wolverine.Marten;
        namespace MartenOpsHandlers;
        public record StoreRequested(System.Guid Id);
        public record InsertRequested(System.Guid Id);
        public record UpdateRequested(System.Guid Id);
        public record DeleteRequested(System.Guid Id);
        public record StoreManyRequested(System.Guid Id);
        public record StartRequested(System.Guid Id);
        public record TenantStartRequested(System.Guid Id);
        public record UnknownRequested(System.Guid Id);
        public record UnreturnedRequested(System.Guid Id);
        public record Started(System.Guid Id);
        public class Document { public System.Guid Id { get; set; } }
        public class Aggregate { public System.Guid Id { get; set; } }
        public class CustomOp : IMartenOp;
        public static class StoreHandler
        {
            public static IMartenOp Handle(StoreRequested message)
            {
                var operation = MartenOps.Store(new Document { Id = message.Id });
                return operation;
            }
        }
        public static class InsertHandler
        {
            public static IMartenOp Handle(InsertRequested message) => MartenOps.Insert(new Document { Id = message.Id }, "tenant");
        }
        public static class UpdateHandler
        {
            public static Task<(IMartenOp, IMartenOp)> Handle(UpdateRequested message) => Task.FromResult<(IMartenOp, IMartenOp)>((MartenOps.Update(new Document { Id = message.Id }, "tenant"), MartenOps.Delete<Document>(message.Id, "tenant")));
        }
        public static class DeleteHandler
        {
            public static IMartenOp Handle(DeleteRequested message) => MartenOps.Delete(new Document { Id = message.Id });
        }
        public static class StoreManyHandler
        {
            public static (IMartenOp, IMartenOp) Handle(StoreManyRequested message) => (MartenOps.StoreMany(new Document { Id = message.Id }), MartenOps.StoreMany("tenant", new Document { Id = message.Id }));
        }
        public static class StartHandler
        {
            public static IMartenOp Handle(StartRequested message) => MartenOps.StartStream<Aggregate>(message.Id, new Started(message.Id));
        }
        public static class TenantStartHandler
        {
            public static async Task<IStartStream> Handle(TenantStartRequested message)
            {
                await Task.CompletedTask;
                return MartenOps.StartStream<Aggregate>(message.Id, "tenant", new Started(message.Id));
            }
        }
        public static class UnknownHandler
        {
            public static IMartenOp Handle(UnknownRequested message) => new CustomOp();
        }
        public static class UnreturnedHandler
        {
            public static IMartenOp Handle(UnreturnedRequested message)
            {
                var unused = MartenOps.Store(new Document { Id = message.Id });
                return new CustomOp();
            }
        }
        """;

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_));
        var compilation = CSharpCompilation.Create(
            "MartenOpsHandlers",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/MartenOpsHandlers/Handlers.cs")
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "MartenOpsHandlers",
            ProjectPath = "/workspace/MartenOpsHandlers/MartenOpsHandlers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
    }
}
