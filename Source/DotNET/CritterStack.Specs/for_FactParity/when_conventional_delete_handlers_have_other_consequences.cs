// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Mixed effects stay on the compatibility path; the native fragment must not emit partial commands.</summary>
public class when_conventional_delete_handlers_have_other_consequences : given.a_document_delete_handler_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record Cascaded(System.Guid Id);
            public sealed record Recorded(System.Guid Id);
            public sealed record RemoveAndCascade(System.Guid Id);
            public sealed record RemoveAndSend(System.Guid Id);
            public sealed record RemoveAndAppend(System.Guid Id);
            public sealed record RemoveWithStage(System.Guid Id);
            public sealed record RemoveAndStore(System.Guid Id);

            public static class CascadeHandler
            {
                public static Cascaded Handle(RemoveAndCascade command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    return new Cascaded(command.Id);
                }
            }
            public static class SendHandler
            {
                public static async System.Threading.Tasks.ValueTask Handle(RemoveAndSend command, Marten.IDocumentSession session, Wolverine.IMessageBus bus)
                {
                    session.Delete<Document>(command.Id);
                    await bus.SendAsync(new Cascaded(command.Id));
                }
            }
            public static class AppendHandler
            {
                public static void Handle(RemoveAndAppend command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Events.Append(command.Id, new Recorded(command.Id));
                }
            }
            public static class StagedHandler
            {
                public static void Handle(RemoveWithStage command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
                public static Cascaded After(RemoveWithStage command) => new Cascaded(command.Id);
            }
            public static class StoreHandler
            {
                public static void Handle(RemoveAndStore command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Store(new Document(command.Id));
                }
            }
        }
        """;

    void Because() => CompareHandlers(Application);

    [Fact]
    void should_leave_all_five_commands_on_the_compatibility_path() => Commands(Expected).Select(command => command.Name).Order().ToArray()
        .ShouldEqual(["RemoveAndAppend", "RemoveAndCascade", "RemoveAndSend", "RemoveAndStore", "RemoveWithStage"]);
    [Fact] void should_emit_no_partial_native_facts() => Native.Facts.ShouldBeEmpty();
    [Fact] void should_emit_no_partial_native_diagnostics() => Native.Diagnostics.ShouldBeEmpty();
    [Fact] void should_emit_no_partial_commands_in_either_order() => Composites.SelectMany(Commands).ShouldBeEmpty();
    [Fact] void should_be_admitted_in_both_orders() => AllAdmitted().ShouldBeTrue();
}
