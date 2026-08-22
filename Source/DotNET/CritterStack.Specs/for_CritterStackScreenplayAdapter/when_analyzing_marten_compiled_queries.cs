// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_analyzing_marten_compiled_queries : given.a_marten_compiled_query_application
{
    ResolvedApplicationGraph _graph = null!;

    void Because() => _graph = new GenerationResolver().Resolve([Contribution]);

    [Fact] void should_link_the_session_compiled_query_to_the_proven_http_query() => CompiledRead("Search").Key.Target.Value.EndsWith("/Students.Student", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_link_the_batch_compiled_query_to_the_proven_http_query() => CompiledRead("First").Key.Target.Value.EndsWith("/Students.Student", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_keep_the_call_site_as_relationship_evidence() => CompiledRead("Search").Evidence.Single().Explanation.ShouldContain("StudentsByName");
    [Fact] void should_resolve_the_exposed_output_shape() => ReturnedModel("Search").ShouldEqual("StudentResult");
    [Fact] void should_resolve_the_bound_compiled_query_document_shapes() => Documents.Select(_ => _.Variants.Single().Definition.Name).ShouldContainOnly("Student", "OtherStudent");
    [Fact] void should_bind_a_multi_interface_plan_to_the_other_document_interface_selected_by_the_invocation() => CompiledRead("MultiDocument").Key.Target.Value.EndsWith("/Students.OtherStudent", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_not_bind_the_other_document_invocation_to_the_first_implemented_interface() => CompiledRead("MultiDocument").Key.Target.Value.EndsWith("/Students.Student", StringComparison.Ordinal).ShouldBeFalse();
    [Fact] void should_bind_the_same_multi_interface_plan_to_the_student_interface_selected_by_another_invocation() => CompiledRead("MultiDocumentStudent").Key.Target.Value.EndsWith("/Students.Student", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_not_bind_the_student_invocation_to_the_other_implemented_interface() => CompiledRead("MultiDocumentStudent").Key.Target.Value.EndsWith("/Students.OtherStudent", StringComparison.Ordinal).ShouldBeFalse();
    [Fact] void should_mark_the_conventional_document_identity_without_treating_it_as_an_event_source() => Document("Student").Properties.Single(_ => _.Name == "id").IsIdentifier.ShouldBeTrue();
    [Fact] void should_preserve_public_readable_plan_parameters() => Query("Search").Properties.Select(_ => _.Name).ShouldContainOnly("name", "page");
    [Fact] void should_exclude_the_exact_marten_ignore_member() => Query("Search").Properties.Any(_ => _.Name == "internalToken").ShouldBeFalse();
    [Fact] void should_exclude_write_only_plan_properties() => Query("Search").Properties.Any(_ => _.Name == "writeOnly").ShouldBeFalse();
    [Fact] void should_exclude_indexers_from_plan_parameters() => Query("MultiDocument").Properties.ShouldBeEmpty();
    [Fact] void should_link_a_compiled_query_in_a_directly_called_local_function() => CompiledRead("CalledLocal").Key.Target.Value.EndsWith("/Students.Student", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_link_a_compiled_query_in_an_immediately_invoked_lambda() => CompiledRead("CalledLambda").Key.Target.Value.EndsWith("/Students.Student", StringComparison.Ordinal).ShouldBeTrue();
    [Fact] void should_not_link_calls_in_unproven_nested_executable_scopes() => ReadsFrom("Nested").ShouldBeEmpty();
    [Fact] void should_diagnose_each_unproven_nested_compiled_query_flow() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved).ShouldEqual(2);
    [Fact] void should_not_diagnose_proven_nested_compiled_query_flow() => Contribution.Diagnostics.Any(_ => _.Code == MartenDiagnosticCodes.CompiledQueryFlowUnresolved && (_.Subject == QuerySubject("CalledLocal") || _.Subject == QuerySubject("CalledLambda"))).ShouldBeFalse();
    [Fact] void should_not_link_a_general_query_plan_without_a_resolvable_document_shape() => ReadsFrom("General").ShouldBeEmpty();
    [Fact] void should_not_link_a_compiled_query_call_outside_a_proven_entry_point() => _graph.Relationships.Any(_ => _.Key.Kind == RelationshipKind.Reads && _.Key.Source.Value.Contains("StudentHelper", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_link_a_same_named_non_marten_method() => ReadsFrom("Unrelated").ShouldBeEmpty();
    [Fact] void should_not_create_artifacts_for_unused_compiled_plans() => _graph.Artifacts.Any(_ => _.Key.Subject.Value.Contains("UnusedStudentPlan", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_create_artifacts_for_compiled_query_plan_types() => _graph.Artifacts.Any(_ => _.Key.Subject.Value.Contains("StudentsByName", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_not_activate_generated_marten_handlers() => _graph.Artifacts.Any(_ => _.Key.Subject.Value.Contains("CompiledQueryHandler", StringComparison.Ordinal)).ShouldBeFalse();
    [Fact] void should_keep_ordinary_documents_out_of_projection_artifacts() => _graph.Artifacts.Any(_ => (_.Key.Kind == ArtifactKind.Projection || _.Key.Kind == ArtifactKind.Reducer) && _.Variants.Single().Definition.Name == "Student").ShouldBeFalse();
    [Fact] void should_report_each_ordinary_document_language_gap() => Contribution.Diagnostics.Count(_ => _.Code == MartenDiagnosticCodes.DocumentModelOmitted).ShouldEqual(2);

    IReadOnlyList<ResolvedArtifact> Documents => [.. _graph.Artifacts.Where(_ => _.Key.Kind == ArtifactKind.Document)];

    ArtifactDefinition Document(string name) => Documents
        .Single(_ => _.Variants.Single().Definition.Name == name)
        .Variants.Single().Definition;

    ArtifactDefinition Query(string name) => _graph.Artifacts
        .Single(_ => _.Key.Kind == ArtifactKind.Query && _.Variants.Single().Definition.Name == name)
        .Variants.Single().Definition;

    ResolvedRelationship CompiledRead(string queryName) => ReadsFrom(queryName).Single();

    IReadOnlyList<ResolvedRelationship> ReadsFrom(string queryName)
    {
        var subject = QuerySubject(queryName);
        return [.. _graph.Relationships.Where(_ => _.Key.Kind == RelationshipKind.Reads && _.Key.Source == subject)];
    }

    SubjectId QuerySubject(string queryName) => _graph.Artifacts
        .Single(_ => _.Key.Kind == ArtifactKind.Query && _.Variants.Single().Definition.Name == queryName)
        .Key.Subject;

    string ReturnedModel(string queryName)
    {
        var subject = QuerySubject(queryName);
        var target = _graph.Relationships.Single(_ => _.Key.Kind == RelationshipKind.Returns && _.Key.Source == subject).Key.Target;
        return _graph.Artifacts.Single(_ => _.Key.Subject == target && _.Key.Kind == ArtifactKind.ReadModel).Variants.Single().Definition.Name;
    }
}
