// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_FactParity;

/// <summary>Effect-free HTTP commands without a request type use direct parameter members and match the compatibility oracle.</summary>
public class when_effect_free_http_commands_use_parameters : given.a_native_http_command_comparison
{
    const string Application =
        """
        namespace FactParity
        {
            public enum Kind { A, B }
            public static class ArchiveEndpoint
            {
                [Wolverine.Http.WolverineDelete("/archive")]
                public static void Delete(string reason, System.Guid id, int? limit, string[] tags) { }
            }
            public static class OrderEndpoints
            {
                [Wolverine.Http.WolverinePost("/touch")]
                public static void Post(Kind kind, System.DateOnly date, System.Threading.CancellationToken token) { }
            }
        }
        """;

    void Because() => Compare(Application);

    [Fact] void should_emit_every_command() => Commands(Public).Count().ShouldEqual(2);
    [Fact] void should_emit_parameter_members() => Native.Facts.OfType<ArtifactMemberDeclarationFact>().ShouldNotBeEmpty();
    [Fact] void should_match_artifacts() => Artifacts(Public).ShouldEqual(Artifacts(Expected));
    [Fact] void should_match_relationships() => Relationships(Public).ShouldEqual(Relationships(Expected));
    [Fact] void should_match_identifier_flags() => Identifiers(Public).ShouldEqual(Identifiers(Expected));
    [Fact] void should_match_printed_source() => Public.Source.ShouldEqual(Expected.Source);
    [Fact] void should_match_status() => Public.IsSuccess.ShouldEqual(Expected.IsSuccess);
    [Fact] void should_match_full_diagnostics() => Diagnostics(Public).ShouldEqual(Diagnostics(Expected));
    [Fact] void should_be_admitted_by_the_real_descriptor() => Admission.Diagnostics.ShouldNotContain(_ => _.Code == AdapterContributionAdmissionDiagnosticCode.UndeclaredFactCapability);
    [Fact] void should_bind_only_subject_carrying_uses_to_concepts() => ConceptTargetsAreOnlyBoundSubjects(Public).ShouldBeTrue();
    [Fact] void should_not_emit_input_transfer() => Native.Facts.OfType<EntryPointInputTransferFact>().ShouldBeEmpty();
}
