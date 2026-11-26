// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Effect-free HTTP commands with a request record match the compatibility oracle through the public described roster.</summary>
public class when_effect_free_http_commands_use_request_records : given.a_native_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public sealed record Register(System.Guid Id, string Name, int? Age, string[] Tags);
            public sealed record Rename(string Name, System.Guid AccountId);
            public sealed record Remove(string Reason);
            public static class RegisterEndpoint
            {
                [Wolverine.Http.WolverinePost("/register")]
                public static void Post(Register command) { }
            }
            public static class AccountEndpoints
            {
                [Wolverine.Http.WolverinePut("/rename")]
                public static void Put(Rename command) { }
                [Wolverine.Http.WolverineDelete("/remove")]
                public static string Delete(Remove command) => "removed";
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_emit_every_command() => Commands(Public).Count().ShouldEqual(3);
    [Fact] void should_emit_identifier_roles_from_wolverine_evidence() => Native.Facts.OfType<ArtifactMemberRoleFact>().ShouldNotBeEmpty();
    [Fact] void should_match_artifacts() => Artifacts(Public).ShouldEqual(Artifacts(Expected));
    [Fact] void should_match_relationships() => Relationships(Public).ShouldEqual(Relationships(Expected));
    [Fact] void should_match_identifier_flags() => Identifiers(Public).ShouldEqual(Identifiers(Expected));
    [Fact] void should_match_printed_source() => Public.Source.ShouldEqual(Expected.Source);
    [Fact] void should_match_status() => Public.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_match_full_diagnostics() => Diagnostics(Public).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_be_admitted_by_the_real_descriptor() => Admission.Diagnostics.ShouldNotContain(_ => _.Code == AdapterContributionAdmissionDiagnosticCode.UndeclaredFactCapability);
    [Fact]
    void should_declare_every_emitted_kind_nonvacuously() =>
        new[]
        {
            GenerationFactCapability.ArtifactDeclaration, GenerationFactCapability.ArtifactMemberDeclaration,
            GenerationFactCapability.ArtifactMemberTypeUse, GenerationFactCapability.ArtifactMemberRole,
            GenerationFactCapability.EntryPoint, GenerationFactCapability.ArtifactPlacement
        }.All(Declares).ShouldBeTrue();
}
