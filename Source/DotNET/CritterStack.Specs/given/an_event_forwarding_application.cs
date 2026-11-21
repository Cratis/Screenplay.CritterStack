// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class an_event_forwarding_application : Specification
{
    // Wolverine V6.64.2: MartenIntegration.cs and WolverineOptionsMartenExtensions.cs.
    const string FrameworkSource =
        """
        namespace Wolverine { public class WolverineOptions; }
        namespace Marten
        {
            public static class MartenServiceCollectionExtensions
            {
                public class MartenConfigurationExpression;
            }
        }
        namespace Marten.Events { public interface IEvent<T> { T Data { get; } } }
        namespace Wolverine.Marten
        {
            public interface IEventForwarding { EventForwardingTransform<T> SubscribeToEvent<T>() where T : notnull; }
            public class MartenIntegration : IEventForwarding
            {
                public bool UseFastEventForwarding { get; set; }
                public EventForwardingTransform<T> SubscribeToEvent<T>() where T : notnull => new();
            }
            public class EventForwardingTransform<T> where T : notnull
            {
                public void TransformedTo<TDestination>(System.Func<global::Marten.Events.IEvent<T>, TDestination> transform) { }
            }
            public static class WolverineOptionsMartenExtensions
            {
                public static global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression IntegrateWithWolverine(
                    this global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression expression,
                    System.Action<MartenIntegration>? configure = null) => expression;
                public static global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression PublishEventsToWolverine(
                    this global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression expression,
                    string subscriptionName, System.Action<Subscriptions.IPublishingRelay>? configure = null) => expression;
                public static global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression ProcessEventsWithWolverineHandlersInStrictOrder(
                    this global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression expression,
                    string subscriptionName, System.Action<object>? configure = null) => expression;
                public static global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression SubscribeToEvents(
                    this global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression expression,
                    Subscriptions.IWolverineSubscription subscription) => expression;
            }
        }
        namespace Wolverine.Marten.Subscriptions
        {
            public interface IPublishingRelay { void PublishEvent<T>(); }
            public interface IWolverineSubscription;
        }
        """;

    const string ApplicationSource =
        """
        using Wolverine.Marten;
        namespace Forwarding;
        public record IncidentCategorised(string Id);
        public record TryAssignPriority(string Id);
        public class CustomSubscription : Wolverine.Marten.Subscriptions.IWolverineSubscription;
        public class Unrelated { public void PublishEventsToWolverine(string name) { } }
        public static class Configuration
        {
            public static void Configure(Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression expression)
            {
                expression.IntegrateWithWolverine();
                expression.IntegrateWithWolverine(o => o.UseFastEventForwarding = false);
                new Unrelated().PublishEventsToWolverine("not forwarding");
                expression.IntegrateWithWolverine(o =>
                {
                    o.UseFastEventForwarding = true;
                    o.SubscribeToEvent<IncidentCategorised>().TransformedTo(e => new TryAssignPriority(e.Data.Id));
                });
                expression.PublishEventsToWolverine("relay", relay => relay.PublishEvent<IncidentCategorised>());
                expression.ProcessEventsWithWolverineHandlersInStrictOrder("ordered");
                expression.SubscribeToEvents(new CustomSubscription());
            }
        }
        """;

    protected DotNetProjectCompilation Project = null!;
    protected AdapterContribution Contribution = null!;

    void Establish()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Select(_ => MetadataReference.CreateFromFile(_));
        var compilation = CSharpCompilation.Create(
            "Forwarding",
            [CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
             CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Forwarding/Configuration.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new()
        {
            Name = "Forwarding",
            ProjectPath = "/workspace/Forwarding/Forwarding.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());
    }
}
