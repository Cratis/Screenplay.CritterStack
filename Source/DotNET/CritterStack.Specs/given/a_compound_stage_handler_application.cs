// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_compound_stage_handler_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;

            public enum HandlerContinuation
            {
                Continue,
                Stop
            }

            public enum RequirementResult
            {
                Continue,
                Stop
            }

            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine.Persistence
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class QueryableAttribute : System.Attribute;
        }

        namespace Wolverine.Attributes
        {
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public class WolverineAfterCommitAttribute : System.Attribute;
        }
        """;

    const string ApplicationSource =
        """
        namespace CompoundStageHandlers;

        public record InspectionRequested(string Id);
        public record InspectionLookup(string Id, bool IsAllowed);
        public record InspectionRefused(string Id, string Reason);
        public record InspectionRecord(string Id);

        public static class InspectionRequestedHandler
        {
            public static System.Threading.Tasks.Task<InspectionLookup> LoadAsync(InspectionRequested message) =>
                System.Threading.Tasks.Task.FromResult(new InspectionLookup(message.Id, false));

            public static (Wolverine.HandlerContinuation, Wolverine.OutgoingMessages) Before(InspectionRequested message) =>
                (Wolverine.HandlerContinuation.Stop, [new InspectionRefused(message.Id, "Inspection is not allowed")]);

            public static void Handle(
                [Wolverine.Persistence.Queryable] System.Linq.IQueryable<InspectionRecord> records,
                InspectionRequested message,
                InspectionLookup lookup) { }
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
            "CompoundStageHandlers",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/CompoundStageHandlers/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "CompoundStageHandlers",
            ProjectPath = "/workspace/CompoundStageHandlers/CompoundStageHandlers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
