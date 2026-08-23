// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_legacy_wolverine_dcb_evidence : given.a_legacy_wolverine_dcb_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "LegacyAccounts" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_admit_exact_legacy_boundary_attributes() => ArtifactNames(ArtifactKind.Command).ShouldContainOnly(["LegacyBoundaryChange", "LegacyChange"]);
    [Fact] void should_create_the_legacy_dcb_aggregate() => ArtifactNames(ArtifactKind.Aggregate).ShouldContainOnly(["AccountState"]);
    [Fact] void should_capture_the_legacy_declarative_return() => ArtifactNames(ArtifactKind.Event).ShouldContain("LegacyChanged");
    [Fact] void should_capture_the_exact_legacy_boundary_append() => ArtifactNames(ArtifactKind.Event).ShouldContain("LegacyAppended");
    [Fact] void should_capture_the_legacy_persistence_wrapper() => ArtifactNames(ArtifactKind.Event).ShouldContain("LegacyWrapped");
    [Fact] void should_not_capture_the_legacy_boundary_return() => ArtifactNames(ArtifactKind.Event).ShouldNotContain("LegacyBoundaryReturn");
    [Fact] void should_not_create_reads_to_explicit_query_events() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Reads && _.Key.Target == SubjectOf(ArtifactKind.Event, "LegacyOpened")).ShouldBeFalse();
    [Fact] void should_not_create_dcb_append_relationships() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Appends).ShouldBeFalse();
    [Fact] void should_report_one_boundary_loss_per_legacy_handler() => _result.Diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted).ShouldEqual(2);
    [Fact] void should_not_report_a_query_loss_for_bounded_legacy_queries() => _result.Diagnostics.Any(_ => _.Code == WolverineDiagnosticCodes.DcbQueryUnresolved).ShouldBeFalse();
    [Fact] void should_locate_legacy_diagnostics() => _result.Diagnostics.Where(_ => _.Code == WolverineDiagnosticCodes.DcbBoundaryOmitted).All(_ => _.Source?.Path == "LegacyAccounts/Dcb.cs").ShouldBeTrue();

    IReadOnlyList<string> ArtifactNames(ArtifactKind kind) =>
    [
        .. _result.Graph.Artifacts
            .Where(_ => _.Key.Kind == kind)
            .Select(_ => _.Variants[0].Definition.Name)
            .Order(StringComparer.Ordinal)
    ];

    SubjectId SubjectOf(ArtifactKind kind, string name) => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == kind && _.Variants.Any(variant => variant.Definition.Name == name)).Key.Subject;
}
