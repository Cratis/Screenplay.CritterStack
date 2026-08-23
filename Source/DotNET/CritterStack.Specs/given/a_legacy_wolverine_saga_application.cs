// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_legacy_wolverine_saga_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine.Configuration
        {
            public class HandlerDiscovery;
        }

        namespace Wolverine.Persistence.Sagas
        {
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class SagaIdentityAttribute : System.Attribute;
        }

        namespace Wolverine
        {
            public class WolverineOptions;
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
            public abstract class Saga
            {
                protected void MarkCompleted() { }
                public bool IsCompleted() => false;
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace LegacyOrders;

        public record LegacyMessage([property: Wolverine.Persistence.Sagas.SagaIdentity] System.Guid WorkflowId);

        public sealed class LegacyWorkflow : Wolverine.Saga
        {
            public System.Guid Id { get; set; }

            public static LegacyWorkflow Start(LegacyMessage message) => new() { Id = message.WorkflowId };
            public static System.Threading.Tasks.Task<LegacyWorkflow> StartAsync(LegacyMessage message) => System.Threading.Tasks.Task.FromResult(new LegacyWorkflow { Id = message.WorkflowId });
            public static LegacyWorkflow Starts(LegacyMessage message) => new() { Id = message.WorkflowId };
            public static System.Threading.Tasks.Task<LegacyWorkflow> StartsAsync(LegacyMessage message) => System.Threading.Tasks.Task.FromResult(new LegacyWorkflow { Id = message.WorkflowId });
            public void StartOrHandle(LegacyMessage message) { }
            public System.Threading.Tasks.Task StartOrHandleAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void StartsOrHandles(LegacyMessage message) { }
            public System.Threading.Tasks.Task StartsOrHandlesAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Orchestrate(LegacyMessage message) { }
            public System.Threading.Tasks.Task OrchestrateAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Orchestrates(LegacyMessage message) { }
            public System.Threading.Tasks.Task OrchestratesAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Handle(LegacyMessage message) { }
            public System.Threading.Tasks.Task HandleAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Handles(LegacyMessage message) { }
            public System.Threading.Tasks.Task HandlesAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Consume(LegacyMessage message) { }
            public System.Threading.Tasks.Task ConsumeAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Consumes(LegacyMessage message) { }
            public System.Threading.Tasks.Task ConsumesAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public static void NotFound(LegacyMessage message) { }
            public static System.Threading.Tasks.Task NotFoundAsync(LegacyMessage message) => System.Threading.Tasks.Task.CompletedTask;
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/LegacyFramework.cs");
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/LegacyOrders/Sagas.cs");
        var compilation = CSharpCompilation.Create(
            "LegacyOrders",
            [frameworkTree, applicationTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "LegacyOrders",
            ProjectPath = "/workspace/LegacyOrders/LegacyOrders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
    }
}
