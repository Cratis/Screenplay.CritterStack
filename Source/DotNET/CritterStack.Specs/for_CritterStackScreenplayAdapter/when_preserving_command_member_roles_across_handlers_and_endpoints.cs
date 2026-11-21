// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_preserving_command_member_roles_across_handlers_and_endpoints : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace CommandMembers;
        public enum Mode { Fast, Slow }
        public record Process(System.Guid Id, string Title, Mode Mode, int? Optional, System.Collections.Generic.List<string> Tags);
        public record Create(System.Guid Id, string Title, Mode Mode, int? Optional, System.Collections.Generic.List<string> Tags);
        public record Plain(string Title, int Count);
        public static class ProcessHandler { public static void Handle(Process message) { } }
        public static class PlainHandler { public static void Handle(Plain message) { } }
        public static class CreateEndpoint
        {
            [Wolverine.Http.WolverinePost("/create")]
            public static void Post(Create message) { }
        }
        """;

    DotNetProjectCompilation _project = null!;
    AdapterContribution _contribution = null!;

    void Establish() => _project = CreateProject(Source);

    void Because() => _contribution = Analyze(_project);

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_keep_the_declaration_order_for_a_handler_command() => Names("Process").ShouldContainOnly("id", "title", "mode", "optional", "tags");
    [Fact] void should_keep_the_declaration_order_for_an_endpoint_command() => Names("Create").ShouldContainOnly("id", "title", "mode", "optional", "tags");
    [Fact] void should_flag_the_conventional_identifier_for_a_handler_command() => Identifiers("Process").ShouldEqual("id");
    [Fact] void should_flag_the_conventional_identifier_for_an_endpoint_command() => Identifiers("Create").ShouldEqual("id");
    [Fact] void should_flag_no_identifier_without_a_matching_member() => Identifiers("Plain").ShouldEqual(string.Empty);
    [Fact] void should_keep_the_non_identifier_members_of_a_command_without_identifier() => Names("Plain").ShouldContainOnly("title", "count");

    IReadOnlyList<PropertyDefinition> Properties(string command) => _contribution.Facts
        .OfType<ArtifactFact>()
        .Single(_ => _.Definition.Key.Kind == ArtifactKind.Command && _.Definition.Name == command)
        .Definition.Properties;

    IEnumerable<string> Names(string command) => Properties(command).Select(_ => _.Name);

    string Identifiers(string command) => string.Join(",", Properties(command).Where(_ => _.IsIdentifier).Select(_ => _.Name));
}
