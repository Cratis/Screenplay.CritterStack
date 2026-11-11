// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_wolverine_dcb_chain_competition : given.a_wolverine_dcb_competition_application
{
    const string Prefix = "wolverine:dcb:1:state:condition:0:tag:dotnet://Accounts/Accounts/Accounts.";

    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "Accounts" });

    [Fact] void should_succeed() => Assert.True(_result.IsSuccess);
    [Fact] void should_prefer_the_earlier_declared_companion_over_the_alphabetically_earlier_one() => Conditions("Ordered").ShouldContainOnly([Prefix + "AccountId:event:dotnet://Accounts/Accounts/Accounts.AccountCredited"]);
    [Fact] void should_prefer_the_earlier_declared_companion_regardless_of_companion_name() => Conditions("Reversed").ShouldContainOnly([Prefix + "AccountId:event:dotnet://Accounts/Accounts/Accounts.AccountOpened"]);
    [Fact] void should_prefer_the_companion_in_the_path_earlier_file_for_a_partial_class() => Conditions("Split").ShouldContainOnly([Prefix + "AccountId:event:dotnet://Accounts/Accounts/Accounts.AccountOpened"]);
    [Fact] void should_not_interpret_a_block_bodied_companion() => Conditions("BlockBody").ShouldBeEmpty();
    [Fact] void should_not_interpret_a_chain_without_a_head() => Conditions("HeadlessAnd").ShouldBeEmpty();
    [Fact] void should_not_interpret_an_array_typed_tag() => Conditions("ArrayTag").ShouldBeEmpty();
    [Fact] void should_not_interpret_an_array_typed_or_tag() => Conditions("ArrayOrTag").ShouldBeEmpty();
    [Fact] void should_not_interpret_an_array_typed_event_type() => Conditions("ArrayAndEvent").ShouldBeEmpty();
    [Fact] void should_interpret_a_chain_assigned_to_a_local() => Conditions("LocalQuery").ShouldContainOnly([Prefix + "CustomerId:event:dotnet://Accounts/Accounts/Accounts.AccountOpened"]);
    [Fact] void should_report_each_uninterpreted_companion_once() => Diagnostics(WolverineDiagnosticCodes.DcbQueryUnresolved).Count.ShouldEqual(5);
    [Fact] void should_report_one_boundary_loss_per_admitted_handler() => Diagnostics(WolverineDiagnosticCodes.DcbBoundaryOmitted).Count.ShouldEqual(14);
    [Fact] void should_anchor_the_boundary_loss_of_a_partial_handler_in_the_file_of_its_handle_method() => Diagnostics(WolverineDiagnosticCodes.DcbBoundaryOmitted).Any(_ => _.Source?.Path == "Accounts/CompanionZ.cs" && _.Message.Contains("'Split'", StringComparison.Ordinal)).ShouldBeTrue();
    [Fact] void should_read_the_state_without_a_condition_for_a_wrapper_return() => ReadDiscriminators("EmptyWrapper").ShouldContainOnly(["wolverine:dcb:1:state"]);
    [Fact] void should_mark_a_boundary_parameter_with_the_boundary_discriminator() => ReadDiscriminators("OrdinaryOnBoundary").ShouldContainOnly(["wolverine:dcb:1:boundary"]);
    [Fact] void should_capture_nothing_from_an_empty_wrapper() => Produced("EmptyWrapper").ShouldBeEmpty();
    [Fact] void should_capture_nothing_from_a_nested_wrapper() => Produced("NestedWrapper").ShouldBeEmpty();
    [Fact] void should_capture_every_event_of_an_event_collection_return() => Produced("CollectionWrapper").ShouldContainOnly(["CollectionDone"]);
    [Fact] void should_capture_both_tuple_siblings() => Produced("TupleSiblings").ShouldContainOnly(["TupleFirst", "TupleSecond"]);
    [Fact] void should_capture_distinct_boundary_appends_once_each() => Produced("OrdinaryOnBoundary").ShouldContainOnly(["BoundaryAppended", "BoundaryAppendedAlso"]);

    IReadOnlyList<string> ReadDiscriminators(string commandName) =>
    [
        .. _result.Graph.Relationships
            .Where(_ => _.Key.Kind == RelationshipKind.Reads && _.Key.Source == CommandSubject(commandName))
            .Select(_ => _.Key.Discriminator)
            .Order(StringComparer.Ordinal)
    ];

    IReadOnlyList<string> Conditions(string commandName) =>
        [.. ReadDiscriminators(commandName).Where(_ => _.Contains(":condition:", StringComparison.Ordinal))];

    IReadOnlyList<string> Produced(string commandName) =>
    [
        .. _result.Graph.Relationships
            .Where(_ => _.Key.Kind == RelationshipKind.Produces && _.Key.Source == CommandSubject(commandName))
            .Select(_ => _result.Graph.Artifacts.Single(artifact => artifact.Key.Subject == _.Key.Target).Variants[0].Definition.Name)
            .Order(StringComparer.Ordinal)
    ];

    SubjectId CommandSubject(string commandName) => _result.Graph.Artifacts.Single(_ =>
        _.Key.Kind == ArtifactKind.Command && _.Variants.Any(variant => variant.Definition.Name == commandName)).Key.Subject;

    IReadOnlyList<GenerationDiagnostic> Diagnostics(string code) => [.. _result.Diagnostics.Where(_ => _.Code == code)];
}
