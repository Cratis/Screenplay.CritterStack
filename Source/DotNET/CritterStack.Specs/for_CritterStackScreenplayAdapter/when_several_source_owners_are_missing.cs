// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_several_source_owners_are_missing : given.a_shared_source_placement_application
{
    IReadOnlyList<string> _ownersInArtifactOrder = null!;
    IReadOnlyList<string> _ownersAgainstArtifactOrder = null!;

    void Because()
    {
        var context = new DotNetAnalysisContext([Project]);
        var baseline = Adapter.Analyze(context, AdapterOptions);
        var command = baseline.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Command && _.Definition.Name == "SubmitOrder");
        var query = baseline.Facts.OfType<ArtifactFact>().Single(_ => _.Definition.Key.Kind == ArtifactKind.Query && _.Definition.Name == "GetOrder");
        var commandPlacement = CritterStackSourcePlacement.CompatibilityPlacement(Project, AdapterOptions, "SubmitOrder", "SubmitOrder", GenerationSliceKind.StateChange);
        var queryPlacement = CritterStackSourcePlacement.CompatibilityPlacement(Project, AdapterOptions, "OrderSummary", "GetOrder", GenerationSliceKind.StateView);
        _ownersInArtifactOrder = MissingOwnerSignatures(context, command, commandPlacement, "missing:a", query, queryPlacement, "missing:z");
        _ownersAgainstArtifactOrder = MissingOwnerSignatures(context, command, commandPlacement, "missing:z", query, queryPlacement, "missing:a");
    }

    [Fact]
    void should_report_the_missing_owners_ordered_by_owner_subject_when_it_agrees_with_artifact_order() => _ownersInArtifactOrder.ShouldEqual(
        "DOTNETSP0010|Error|Unsupported|missing:a|Application/Source/Orders/Submit/Submit.cs|10|5|Source owner 'missing:a' for artifact 'dotnet://Application/Application/Application.Orders.Submit.SubmitOrder' has no host-supplied source structure",
        "DOTNETSP0010|Error|Unsupported|missing:z|Application/Source/Orders/Summary/OrderEndpoints.cs|5|5|Source owner 'missing:z' for artifact 'dotnet://Application/Application/Application.Orders.Summary.OrderEndpoints#method:M%3AApplication.Orders.Summary.OrderEndpoints.GetOrder%28System.Guid%29:query' has no host-supplied source structure");

    [Fact]
    void should_report_the_missing_owners_ordered_by_owner_subject_when_it_disagrees_with_artifact_order() => _ownersAgainstArtifactOrder.ShouldEqual(
        "DOTNETSP0010|Error|Unsupported|missing:a|Application/Source/Orders/Summary/OrderEndpoints.cs|5|5|Source owner 'missing:a' for artifact 'dotnet://Application/Application/Application.Orders.Summary.OrderEndpoints#method:M%3AApplication.Orders.Summary.OrderEndpoints.GetOrder%28System.Guid%29:query' has no host-supplied source structure",
        "DOTNETSP0010|Error|Unsupported|missing:z|Application/Source/Orders/Submit/Submit.cs|10|5|Source owner 'missing:z' for artifact 'dotnet://Application/Application/Application.Orders.Submit.SubmitOrder' has no host-supplied source structure");

    IReadOnlyList<string> MissingOwnerSignatures(
        DotNetAnalysisContext context,
        ArtifactFact command,
        ArtifactPlacement commandPlacement,
        string commandOwner,
        ArtifactFact query,
        ArtifactPlacement queryPlacement,
        string queryOwner)
    {
        var diagnostics = new List<GenerationDiagnostic>();
        CritterStackSourcePlacement.Derive(
            context,
            AdapterOptions,
            [
                new CritterStackPlacementIntent("test:placement:command", command.Definition.Key, new SubjectId { Value = commandOwner }, commandPlacement, command.Evidence),
                new CritterStackPlacementIntent("test:placement:query", query.Definition.Key, new SubjectId { Value = queryOwner }, queryPlacement, query.Evidence)
            ],
            diagnostics);
        return [.. diagnostics.Select(_ => $"{_.Code}|{_.Severity}|{_.Outcome}|{_.Subject?.Value}|{_.Source?.Path}|{_.Source?.StartLine}|{_.Source?.StartColumn}|{_.Message}")];
    }
}
