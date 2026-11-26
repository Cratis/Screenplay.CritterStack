// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Effect-free routes sharing one request type: the shared artifact is declared once, each route keeps its entry point, and the result matches the oracle.</summary>
public class when_effect_free_http_routes_share_a_request_type : given.a_native_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Rename(string Name, System.Guid AccountId);
            public static class AccountEndpoints
            {
                [Wolverine.Http.WolverinePut("/rename")]
                public static void Put(Rename command) { }

                [Wolverine.Http.WolverinePost("/rename")]
                public static void Post(Rename command) { }
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_emit_the_shared_command_once() => Commands(Public).Count().ShouldEqual(1);
    [Fact] void should_declare_the_shared_artifact_once() => Native.Facts.OfType<ArtifactDeclarationFact>().Count().ShouldEqual(1);
    [Fact] void should_not_repeat_any_fact_id() => Native.Facts.GroupBy(_ => _.Id).All(_ => _.Count() == 1).ShouldBeTrue();
    [Fact] void should_emit_both_entry_points() => Native.Facts.OfType<EntryPointFact>().Count().ShouldEqual(2);
    [Fact] void should_be_admitted_by_the_real_runner() => IsAdmitted.ShouldBeTrue();
    [Fact] void should_match_artifacts() => ArtifactsMatchWithOnlyTheDocumentedDelta().ShouldBeTrue();
    [Fact] void should_match_relationships() => Relationships(Public).ShouldEqual(Relationships(Expected));
    [Fact] void should_match_printed_source() => Public.Source.ShouldEqual(Expected.Source);
    [Fact] void should_match_status() => Public.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_match_full_diagnostics() => Diagnostics(Public).ShouldEqual(Diagnostics(Expected));
}
