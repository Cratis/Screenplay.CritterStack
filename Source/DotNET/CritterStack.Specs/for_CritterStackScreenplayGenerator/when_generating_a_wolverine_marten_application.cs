// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_generating_a_wolverine_marten_application : given.a_wolverine_marten_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        [Project],
        new CritterStackScreenplayOptions { Domain = "Helpdesk" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_generate_log_incident() => _result.Source.ShouldContain("command LogIncident");
    [Fact] void should_generate_categorise_incident() => _result.Source.ShouldContain("command CategoriseIncident");
    [Fact] void should_generate_close_incident() => _result.Source.ShouldContain("command CloseIncident");
    [Fact] void should_generate_archive_incident() => _result.Source.ShouldContain("command ArchiveIncident");
    [Fact] void should_generate_the_logged_event() => _result.Source.ShouldContain("event IncidentLogged");
    [Fact] void should_generate_the_categorised_event() => _result.Source.ShouldContain("event IncidentCategorised");
    [Fact] void should_generate_the_closed_event() => _result.Source.ShouldContain("event IncidentClosed");
    [Fact] void should_generate_the_external_archived_event() => _result.Source.ShouldContain("event Archived");
    [Fact] void should_not_treat_updated_aggregate_as_an_event() => _result.Source.ShouldNotContain("event UpdatedAggregate");
    [Fact] void should_generate_get_incident_as_a_query() => _result.Source.ShouldContain("query GetIncident => Incident?");
    [Fact] void should_record_the_document_delete() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Deletes).ShouldBeTrue();
    [Fact] void should_record_the_outgoing_message() => _result.Graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Cascades).ShouldBeTrue();
    [Fact] void should_report_delayed_delivery_as_language_loss() => _result.Diagnostics.Select(_ => _.Code).ShouldContain(WolverineDiagnosticCodes.DelayedMessageOmitted);
    [Fact] void should_use_project_qualified_subject_ids() => _result.Graph.Artifacts.All(_ => _.Key.Subject.Value.StartsWith("dotnet://IncidentService/", StringComparison.Ordinal)).ShouldBeTrue();
}
