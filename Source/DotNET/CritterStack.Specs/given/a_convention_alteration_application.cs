// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_convention_alteration_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public interface IWolverineExtension;
            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Configuration
        {
            public interface IHandlerPolicy;
            public interface IWolverinePolicy;
            public interface IWolverineReturnType;

            public class HandlerDiscovery
            {
                public void CustomizeMessageDiscovery(System.Action<object> configure) { }
            }
        }

        namespace Wolverine.Attributes
        {
            public abstract class ModifyHandlerChainAttribute : System.Attribute;
        }

        namespace Marten
        {
            public class StoreOptions;
            public interface IConfigureMarten;
            public interface IAsyncConfigureMarten;
            public interface IDocumentPolicy;
        }

        namespace Marten.Events.Projections
        {
            public abstract class ProjectionDocumentPolicy;
        }
        """;

    const string ApplicationSource =
        """
        namespace ConventionAlterations;

        public record PolicyTrigger(string Id);
        public record PolicyResult(string Id);

        public static class PolicyTriggerHandler
        {
            public static Wolverine.OutgoingMessages Handle(PolicyTrigger message) => [new PolicyResult(message.Id)];
        }
        """;

    const string AlterationSource =
        """
        namespace ConventionAlterations;

        public class CustomHandlerPolicy : Wolverine.Configuration.IHandlerPolicy;
        public class CustomPolicyExtension : Wolverine.Configuration.IWolverinePolicy, Wolverine.IWolverineExtension;
        public class CustomChainAttribute : Wolverine.Attributes.ModifyHandlerChainAttribute;
        public class CustomMartenConfiguration : Marten.IConfigureMarten, Marten.IAsyncConfigureMarten;
        public class CustomDocumentPolicy : Marten.IDocumentPolicy;
        public class CustomProjectionDocumentPolicy : Marten.Events.Projections.ProjectionDocumentPolicy;

        public static class Configuration
        {
            public static void Configure(Wolverine.Configuration.HandlerDiscovery discovery) =>
                discovery.CustomizeMessageDiscovery(_ => { });
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation BaselineProject = null!;
    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution BaselineContribution = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        BaselineProject = CreateProject(includeAlterations: false);
        Project = CreateProject(includeAlterations: true);
        var adapter = new CritterStackScreenplayAdapter();
        BaselineContribution = adapter.Analyze(new DotNetAnalysisContext([BaselineProject]), new DotNetAdapterOptions());
        Contribution = adapter.Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
    }

    static DotNetProjectCompilation CreateProject(bool includeAlterations)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/ConventionAlterations/Handlers.cs")
        };
        if (includeAlterations)
        {
            trees.Add(CSharpSyntaxTree.ParseText(AlterationSource, path: "/workspace/ConventionAlterations/Policies.cs"));
        }

        var compilation = CSharpCompilation.Create(
            "ConventionAlterations",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = "ConventionAlterations",
            ProjectPath = "/workspace/ConventionAlterations/ConventionAlterations.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
