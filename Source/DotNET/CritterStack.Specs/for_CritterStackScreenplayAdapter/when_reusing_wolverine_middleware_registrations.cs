// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_reusing_wolverine_middleware_registrations : given.a_wolverine_attribute_middleware_application
{
    const string Source =
        """
        namespace SnapshotMiddleware;
        public record Process;
        public record Create;
        public static class ProcessHandler { public static void Handle(Process message) { } }
        public static class OtherHandler { public static void Handle(Process message) { } }
        public static class CreateEndpoint { public static void Post(Create message) { } }
        public static class OtherEndpoint { public static void Post(Create message) { } }
        public class First;
        public class Second;
        public class Http;
        public class Configuration;
        """;

    DotNetProjectCompilation _project = null!;
    WolverineMiddlewareRegistrations _snapshot = null!;
    readonly List<WolverineMiddlewareRegistration> _registrations = [];
    IReadOnlyList<WolverineMiddlewareApplication>[] _handlers = [];
    IReadOnlyList<WolverineMiddlewareApplication>[] _endpoints = [];
    IReadOnlyList<GenerationDiagnostic> _diagnostics = [];
    int _enumerations;
    int _enumerationsAtConstruction;

    void Establish()
    {
        _project = CreateProject(Source);
        var owner = Type("Configuration");
        _registrations.AddRange(
        [
            new(Type("First"), owner, false, true, Range(11)),
            new(null, owner, false, false, Range(13)),
            new(Type("Http"), owner, true, true, Range(14)),
            new(Type("Second"), owner, false, true, Range(12)),
            new(Type("First"), owner, false, true, Range(15))
        ]);
        _snapshot = new WolverineMiddlewareRegistrations(EnumerateOnce());
        _enumerationsAtConstruction = _enumerations;
        _registrations.Clear();
    }

    void Because()
    {
        _handlers = [ApplicationsFor("ProcessHandler", false), ApplicationsFor("OtherHandler", false), ApplicationsFor("ProcessHandler", false)];
        _endpoints = [ApplicationsFor("CreateEndpoint", true), ApplicationsFor("OtherEndpoint", true), ApplicationsFor("CreateEndpoint", true)];
        _diagnostics = WolverinePolicyDiscovery.Discover(_project, new CritterStackSubjectResolver(null), _snapshot);
        _ = _snapshot.Unresolved.ToArray();
    }

    [Fact] void should_compile_the_fixture() => _project.Compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    [Fact] void should_materialize_during_construction() => _enumerationsAtConstruction.ShouldEqual(1);
    [Fact] void should_enumerate_the_input_only_once() => _enumerations.ShouldEqual(1);
    [Fact] void should_keep_resolved_handler_registrations_in_order() => _handlers.ShouldEachConformTo(_ => string.Join(',', _.Select(application => application.Type.Name)) == "First,Second,First");
    [Fact] void should_keep_each_handler_registration_source() => _handlers.ShouldEachConformTo(_ => string.Join(',', _.Select(application => application.Source.StartLine)) == "11,12,15");
    [Fact] void should_not_deduplicate_the_registration_input() => _handlers.ShouldEachConformTo(_ => _.Count == 3);
    [Fact] void should_keep_http_registrations_off_handlers() => _handlers.ShouldEachConformTo(_ => _.All(application => application.Type.Name != "Http"));
    [Fact] void should_keep_handler_registrations_off_endpoints() => _endpoints.ShouldEachConformTo(_ => _.Count == 1 && _[0].Type.Name == "Http");
    [Fact] void should_keep_each_http_registration_source() => _endpoints.ShouldEachConformTo(_ => _[0].Source.StartLine == 14);
    [Fact] void should_preserve_unresolved_entries() => _snapshot.Unresolved.Single().Source.ShouldEqual(Range(13));
    [Fact] void should_preserve_unresolved_owner_symbols() => SymbolEqualityComparer.Default.Equals(_snapshot.Unresolved.Single().Owner, Type("Configuration")).ShouldBeTrue();
    [Fact] void should_diagnose_an_unresolved_registration_once() => _diagnostics.Count(_ => _.Code == WolverineDiagnosticCodes.HandlerDiscoveryConfigurationUnresolved).ShouldEqual(1);
    [Fact] void should_keep_the_unresolved_diagnostic_source() => _diagnostics.Single().Source.ShouldEqual(Range(13));
    [Fact] void should_keep_the_unresolved_diagnostic_subject() => _diagnostics.Single().Subject.Value.ShouldContain("/SnapshotMiddleware.Configuration");
    [Fact] void should_keep_the_unresolved_diagnostic_outcome() => _diagnostics.Single().Outcome.ShouldEqual(GenerationDiagnosticOutcome.Unknown);

    static SourceRange Range(int line) => new() { Path = "MiddlewareApplication/Handlers.cs", StartLine = line, StartColumn = 1, EndLine = line, EndColumn = 2 };

    INamedTypeSymbol Type(string name) => _project.Compilation.GetTypeByMetadataName($"SnapshotMiddleware.{name}")!;

    IReadOnlyList<WolverineMiddlewareApplication> ApplicationsFor(string container, bool isHttpEndpoint) =>
        WolverinePolicyDiscovery.MiddlewareFor(Type(container).GetMembers().OfType<IMethodSymbol>().First(_ => _.MethodKind == MethodKind.Ordinary), _project, _snapshot, isHttpEndpoint);

    IEnumerable<WolverineMiddlewareRegistration> EnumerateOnce()
    {
        if (++_enumerations != 1)
        {
            throw new InvalidOperationException("The registration source must only be enumerated once");
        }
        foreach (var registration in _registrations)
        {
            yield return registration;
        }
    }
}
