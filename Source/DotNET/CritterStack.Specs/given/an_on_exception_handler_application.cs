// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_on_exception_handler_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;

            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Marten
        {
            public interface IDocumentSession { Events.IEventStore Events { get; } }
        }

        namespace Marten.Events
        {
            public interface IEventStore { void Append(string id, params object[] events); }
        }
        """;

    protected virtual string ApplicationSource =>
        """
        namespace OnExceptionHandlers;

        public record ShipOrder(string Id);
        public record CancelOrder(string Id);
        public record OrderShipped(string Id);
        public record ShipmentFailed(string Id);
        public record ShipmentTimedOut(string Id);
        public record ShipmentCompensated(string Id);
        public record CancellationFailed(string Id);
        public record ShipmentNoted(string Id);

        public static class ShipOrderHandler
        {
            public static Wolverine.OutgoingMessages Handle(ShipOrder message) => [new OrderShipped(message.Id)];

            public static Wolverine.OutgoingMessages OnException(ShipOrder message, System.InvalidOperationException exception) =>
                [new ShipmentFailed(message.Id)];

            public static ShipmentTimedOut OnException(System.TimeoutException exception, ShipOrder message) =>
                new(message.Id);

            public static Wolverine.OutgoingMessages OnException(ShipOrder message) => [new ShipmentNoted(message.Id)];

            public static Wolverine.OutgoingMessages OnExceptionally(ShipOrder message, System.InvalidOperationException exception) =>
                [new ShipmentCompensated(message.Id)];

            public static Wolverine.OutgoingMessages OnException(CancelOrder message, System.InvalidOperationException exception) =>
                [new CancellationFailed(message.Id)];

            public static System.Threading.Tasks.Task<ShipmentRejected> OnExceptionAsync(System.IServiceProvider services, System.ArgumentException exception, ShipOrder message) =>
                System.Threading.Tasks.Task.FromResult(new ShipmentRejected(message.Id));

            public static Wolverine.OutgoingMessages OnExceptionAsync(System.ArgumentException exception, CancelOrder message) =>
                [new CancellationFailed(message.Id)];
        }

        public record ShipmentRejected(string Id);
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "OnExceptionHandlers",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/OnExceptionHandlers/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "OnExceptionHandlers",
            ProjectPath = "/workspace/OnExceptionHandlers/OnExceptionHandlers.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([Project]),
            new DotNetAdapterOptions());
    }
}
