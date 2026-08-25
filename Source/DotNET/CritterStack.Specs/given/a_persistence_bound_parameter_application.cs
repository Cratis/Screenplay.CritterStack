// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_persistence_bound_parameter_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
        }

        namespace Wolverine.Persistence
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class EntityAttribute(string? key = null) : System.Attribute
            {
                public bool Required { get; set; } = true;
            }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class FirstOrDefaultAttribute : System.Attribute;

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class QueryableAttribute : System.Attribute;
        }
        """;

    const string ApplicationSource =
        """
        namespace PersistenceBoundParameters;

        public record PropsReported(string ServiceName);
        public record OverridesReported(string ServiceName);
        public record DefaultsRequested(string ServiceName);
        public record HeartbeatsRequested(string ServiceName);

        public class ServiceSummary
        {
            public string ServiceName { get; set; } = string.Empty;
        }

        public class Overrides
        {
            public string ServiceName { get; set; } = string.Empty;
        }

        public class Defaults
        {
            public string ServiceName { get; set; } = string.Empty;
        }

        public class Heartbeat
        {
            public string ServiceName { get; set; } = string.Empty;
        }

        public static class PropsReportedHandler
        {
            public static void Handle(PropsReported message, [Wolverine.Persistence.Entity("ServiceName")] ServiceSummary service) { }
        }

        public static class OverridesReportedHandler
        {
            public static void Handle(OverridesReported message, [Wolverine.Persistence.Entity("ServiceName", Required = false)] Overrides existing) { }
        }

        public static class DefaultsRequestedHandler
        {
            public static void Handle(DefaultsRequested message, [Wolverine.Persistence.FirstOrDefault] Defaults? defaults) { }
        }

        public static class HeartbeatsRequestedHandler
        {
            public static void Handle([Wolverine.Persistence.Queryable] System.Linq.IQueryable<Heartbeat> heartbeats, HeartbeatsRequested message) { }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "PersistenceBoundParameters",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/PersistenceBoundParameters/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "PersistenceBoundParameters",
            ProjectPath = "/workspace/PersistenceBoundParameters/PersistenceBoundParameters.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
