// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>
/// A complete delete endpoint beside delete endpoints that also publish, read, store or delete with an unsupported API: only the complete
/// unit is emitted; every other endpoint gets one internal omission and no partial Wolverine command or edge. No parity is claimed for them.
/// </summary>
public class when_http_delete_commands_have_other_consequences : given.a_native_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Document(System.Guid Id);
            public sealed record Remove(System.Guid Id);
            public sealed record RemoveAndNotify(System.Guid Id);
            public sealed record RemoveAndRead(System.Guid Id);
            public sealed record RemoveAndStore(System.Guid Id);
            public sealed record RemoveWhere(System.Guid Id);
            public sealed record Notified(System.Guid Id);
            public static class Endpoints
            {
                [Wolverine.Http.WolverineDelete("/remove")]
                public static void Delete(Remove command, Marten.IDocumentSession session) => session.Delete<Document>(command.Id);
                [Wolverine.Http.WolverineDelete("/notify")]
                public static async System.Threading.Tasks.ValueTask DeleteNotify(RemoveAndNotify command, Marten.IDocumentSession session, Wolverine.IMessageBus bus)
                {
                    session.Delete<Document>(command.Id);
                    await bus.PublishAsync(new Notified(command.Id));
                }
                [Wolverine.Http.WolverineDelete("/read")]
                public static void DeleteRead(RemoveAndRead command, Marten.IDocumentSession session)
                {
                    session.Load<Document>(command.Id);
                    session.Delete<Document>(command.Id);
                }
                [Wolverine.Http.WolverineDelete("/store")]
                public static void DeleteStore(RemoveAndStore command, Marten.IDocumentSession session)
                {
                    session.Delete<Document>(command.Id);
                    session.Store(new Document(command.Id));
                }
                [Wolverine.Http.WolverineDelete("/where")]
                public static void DeleteWhere(RemoveWhere command, Marten.IDocumentSession session) => session.DeleteWhere<Document>(document => document.Id == command.Id);
            }
        }
        """;

    void Because() => CompareWithMarten(Application);

    [Fact] void should_emit_only_the_complete_delete_command() => Commands(PublicComposed).Select(_ => _.Name).ToArray().ShouldEqual(["Remove"]);
    [Fact] void should_emit_only_the_complete_commands_deletes() => Native.Facts.OfType<RelationshipFact>().Count(_ => _.Definition.Key.Kind == RelationshipKind.Deletes).ShouldEqual(1);
    [Fact]
    void should_report_each_other_endpoint_once() =>
        Native.Diagnostics.Count(_ => _.Code == WolverineFacts.NativeHttpCommandOmittedCode && _.Outcome == GenerationDiagnosticOutcome.Unsupported).ShouldEqual(4);
    [Fact] void should_not_emit_partial_facts_for_unsupported_endpoints() => Native.Facts.Any(_ => _.Subject.Value.Contains("DeleteNotify") || _.Subject.Value.Contains("DeleteRead") || _.Subject.Value.Contains("DeleteStore") || _.Subject.Value.Contains("DeleteWhere")).ShouldBeFalse();
    [Fact] void should_still_be_admitted_by_the_real_descriptor() => IsAdmitted.ShouldBeTrue();
    [Fact] void should_not_claim_parity_with_the_compatibility_commands() => Commands(Expected).Count().ShouldBeGreaterThan(1);
}
