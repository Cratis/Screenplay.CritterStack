// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_fetch_many_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine { public class WolverineOptions; }
        namespace JasperFx.Events
        {
            public interface IEventStream<T>
            {
                T? Aggregate { get; }
                void AppendOne(object value);
                void AppendMany(params object[] values);
            }
            public interface IEventStoreOperations
            {
                System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IEventStream<T>>> FetchManyForWriting<T>(System.Collections.Generic.IReadOnlyList<System.Guid> ids, System.Threading.CancellationToken cancellation = default) where T : class;
                System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<IEventStream<T>>> FetchManyForWriting<T>(System.Collections.Generic.IReadOnlyList<string> keys, System.Threading.CancellationToken cancellation = default) where T : class;
                System.Threading.Tasks.Task<IEventStream<T>> FetchForWriting<T>(System.Guid id) where T : class;
            }
        }
        namespace Marten.Events { public interface IEventStoreOperations : JasperFx.Events.IEventStoreOperations; }
        namespace Marten
        {
            public interface IDocumentSession { Marten.Events.IEventStoreOperations Events { get; } }
        }
        """;

    const string ApplicationSource =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        namespace Batches;
        public class Account;
        public record Move(Guid FromId, Guid ToId);
        public record Debit;
        public record Credit;
        public record Moved;
        public record FollowUp;
        public record Cancel(IReadOnlyList<Guid> Ids);
        public record Cancelled;
        public record Close(string FromId, string ToId);
        public record Closed;
        public record Inspect(Guid Id);
        public record Empty;
        public record Repeated(Guid Id);
        public record RepeatAppended;
        public record Several(Guid FromId, Guid ToId);
        public record SeveralAppended;
        public record Overload(Guid Id);
        public record OverloadedAppended;
        public record Unresolved(Guid Id, int Index);
        public record Unproven;
        public record Opaque;
        public record Reassigned(Guid Id);
        public record ReassignedEvent;
        public record IndexedReassigned(Guid Id);
        public record ReassignedIndexedEvent;
        public record Single(Guid Id);
        public record SingleEvent;
        public record Impostor(Guid Id);
        public record ImpostorEvent;
        public record ForeignIdentity(Guid OwnId);
        public record OtherIds(Guid Id);
        public record ForeignEvent;
        public record EscapedEvent;
        public static class MoveHandler
        {
            public static async Task<FollowUp> Handle(Move command, Marten.IDocumentSession session, CancellationToken cancellation)
            {
                var streams = await session.Events.FetchManyForWriting<Account>(cancellation: cancellation, ids: new[] { command.FromId, command.ToId }).ConfigureAwait(false);
                streams[0].AppendOne(new Debit());
                var destination = streams[1];
                destination.AppendOne(new Credit());
                streams[0].AppendOne(new Moved());
                streams[1].AppendOne(new Moved());
                return new();
            }
        }
        public static class CancelHandler
        {
            public static async Task Handle(Cancel command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>(command.Ids);
                foreach (var stream in streams) { stream.AppendOne(new Cancelled()); }
            }
        }
        public static class CloseHandler
        {
            public static async Task Handle(Close command, JasperFx.Events.IEventStoreOperations events)
            {
                var streams = await events.FetchManyForWriting<Account>(keys: [command.FromId, command.ToId]);
                foreach (var stream in streams) { stream.AppendMany(new Closed()); }
            }
        }
        public static class InspectHandler
        {
            public static async Task Handle(Inspect command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
            }
        }
        public static class EmptyHandler
        {
            public static async Task Handle(Empty command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>(Array.Empty<Guid>());
            }
        }
        public static class RepeatedHandler
        {
            public static async Task Handle(Repeated command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id, command.Id]);
                streams[0].AppendOne(new RepeatAppended());
            }
        }
        public static class SeveralHandler
        {
            public static async Task Handle(Several command, Marten.IDocumentSession session)
            {
                var first = await session.Events.FetchManyForWriting<Account>([command.FromId]);
                var second = await session.Events.FetchManyForWriting<Account>([command.ToId]);
                first[0].AppendOne(new SeveralAppended());
                second[0].AppendOne(new SeveralAppended());
            }
        }
        public static class OverloadHandler
        {
            public static async Task Handle(Overload command, Marten.IDocumentSession session, int unused)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                streams[0].AppendOne(new OverloadedAppended());
            }
            public static async Task Handle(Overload command, Marten.IDocumentSession session, string unused)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                streams[0].AppendOne(new OverloadedAppended());
            }
        }
        public static class UnresolvedHandler
        {
            public static async Task Handle(Unresolved command, Marten.IDocumentSession session, object payload)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                streams[command.Index].AppendOne(new Unproven());
                var alias = streams;
                streams[0].AppendOne(new EscapedEvent());
                alias[0].AppendOne(new Unproven());
                var slot = streams[0];
                var slotAlias = slot;
                slotAlias.AppendOne(new Unproven());
                streams[0].AppendOne(payload);
                streams[0].AppendOne(Create());
                void Callback() => streams[0].AppendOne(new Unproven());
                Callback();
            }
            static Opaque Create() => new();
        }
        public static class ForeignIdentityHandler
        {
            public static async Task Handle(ForeignIdentity command, OtherIds other, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([other.Id]);
                streams[0].AppendOne(new ForeignEvent());
            }
        }
        public static class ReassignedHandler
        {
            public static async Task Handle(Reassigned command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                streams = await session.Events.FetchManyForWriting<Account>([Guid.NewGuid()]);
                streams[0].AppendOne(new ReassignedEvent());
            }
        }
        public static class IndexedReassignedHandler
        {
            public static async Task Handle(IndexedReassigned command, Marten.IDocumentSession session)
            {
                var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                var stream = streams[0];
                stream = null!;
                stream.AppendOne(new ReassignedIndexedEvent());
            }
        }
        public static class SingleHandler
        {
            public static async Task Handle(Single command, Marten.IDocumentSession session)
            {
                var stream = await session.Events.FetchForWriting<Account>(command.Id);
                stream.AppendOne(new SingleEvent());
            }
        }
        public interface IFakeStore
        {
            Task<IReadOnlyList<JasperFx.Events.IEventStream<T>>> FetchManyForWriting<T>(IReadOnlyList<Guid> ids);
        }
        public static class ImpostorHandler
        {
            public static async Task Handle(Impostor command, IFakeStore store)
            {
                var streams = await store.FetchManyForWriting<Account>([command.Id]);
                streams[0].AppendOne(new ImpostorEvent());
            }
        }
        """;

    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Batches/Handlers.cs");
        var generated = CSharpSyntaxTree.ParseText(
            """
            namespace Batches;
            public record GeneratedCommand(System.Guid Id);
            public record GeneratedEvent;
            public static class GeneratedHandler
            {
                public static async System.Threading.Tasks.Task Handle(GeneratedCommand command, Marten.IDocumentSession session)
                {
                    var streams = await session.Events.FetchManyForWriting<Account>([command.Id]);
                    streams[0].AppendOne(new GeneratedEvent());
                }
            }
            """,
            path: "/workspace/Batches/Unlisted.cs");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        Project = new DotNetProjectCompilation
        {
            Name = "Batches",
            ProjectPath = "/workspace/Batches/Batches.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create("Batches", [framework, application, generated], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application }
        };
    }
}
