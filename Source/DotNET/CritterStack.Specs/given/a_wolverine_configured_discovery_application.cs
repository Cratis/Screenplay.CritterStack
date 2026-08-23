// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_configured_discovery_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Core.TypeScanning
        {
            public class TypeQuery
            {
                public CompositeTypeFilter Includes { get; } = new();
                public CompositeTypeFilter Excludes { get; } = new();
            }

            public class CompositeTypeFilter
            {
                public void WithNameSuffix(string suffix) { }
            }
        }

        namespace Wolverine.Attributes
        {
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
        }

        namespace Wolverine.Configuration
        {
            public class HandlerDiscovery
            {
                public HandlerDiscovery DisableConventionalDiscovery(bool value = true) => this;
                public HandlerDiscovery IncludeType<T>() => this;
                public HandlerDiscovery IncludeType(System.Type type) => this;
                public HandlerDiscovery IncludeAssembly(System.Reflection.Assembly assembly) => this;
                public HandlerDiscovery CustomizeHandlerDiscovery(System.Action<JasperFx.Core.TypeScanning.TypeQuery> configure) => this;
                public void IgnoreAssembly(System.Reflection.Assembly assembly) { }
            }
        }

        namespace Wolverine
        {
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
            public abstract class Saga;
            public interface IWolverineHandler;
            public class WolverineOptions
            {
                public Wolverine.Configuration.HandlerDiscovery Discovery { get; } = new();
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace ConfiguredDiscovery;

        public record IncludedTrigger(System.Guid Id);
        public record IncludedCascade(System.Guid Id);
        public record CurrentExplicitTrigger(System.Guid Id);
        public record CurrentExplicitCascade(System.Guid Id);
        public record LegacyExplicitTrigger(System.Guid Id);
        public record LegacyExplicitCascade(System.Guid Id);
        public record SuppressedSuffixTrigger(System.Guid Id);
        public record SuppressedSuffixCascade(System.Guid Id);
        public record SuppressedConsumerTrigger(System.Guid Id);
        public record SuppressedConsumerCascade(System.Guid Id);
        public record SuppressedInterfaceTrigger(System.Guid Id);
        public record SuppressedInterfaceCascade(System.Guid Id);
        public record SuppressedCurrentTypeTrigger(System.Guid Id);
        public record SuppressedCurrentTypeCascade(System.Guid Id);
        public record SuppressedLegacyTypeTrigger(System.Guid Id);
        public record SuppressedLegacyTypeCascade(System.Guid Id);
        public record SuppressedMethodTrigger(System.Guid Id);
        public record SuppressedMethodCascade(System.Guid Id);
        public record CurrentIgnoredTrigger(System.Guid Id);
        public record CurrentIgnoredCascade(System.Guid Id);
        public record LegacyIgnoredTrigger(System.Guid Id);
        public record LegacyIgnoredCascade(System.Guid Id);
        public record CurrentMethodIgnoredTrigger(System.Guid Id);
        public record CurrentMethodIgnoredCascade(System.Guid Id);
        public record LegacyMethodIgnoredTrigger(System.Guid Id);
        public record LegacyMethodIgnoredCascade(System.Guid Id);
        public record MiddlewareTrigger(System.Guid Id);
        public record MiddlewareCascade(System.Guid Id);
        public record CompoundTrigger(System.Guid Id);
        public record BeginIncludedSaga(System.Guid SagaId);
        public record IncludedSagaTrigger(System.Guid SagaId);
        public record BeginSuppressedSaga(System.Guid SagaId);
        public record SuppressedSagaTrigger(System.Guid SagaId);

        public static class Configuration
        {
            public static void Configure(Wolverine.WolverineOptions options)
            {
                options.Discovery
                    .DisableConventionalDiscovery()
                    .IncludeType<IncludedHandler>()
                    .IncludeType(typeof(CurrentExplicitActions));
                options.Discovery.IncludeType<LegacyExplicitActions>();
                options.Discovery.IncludeType<CurrentIgnoredHandler>();
                options.Discovery.IncludeType<LegacyIgnoredHandler>();
                options.Discovery.IncludeType<CurrentMethodIgnoredActions>();
                options.Discovery.IncludeType<LegacyMethodIgnoredActions>();
                options.Discovery.IncludeType<CompoundActions>();
                options.Discovery.IncludeType<IncludedSaga>();
                options.Discovery.IncludeAssembly(typeof(Configuration).Assembly);
            }
        }

        public class IncludedHandler
        {
            public static IncludedCascade Handle(IncludedTrigger message) => new(message.Id);
        }

        public static class CurrentExplicitActions
        {
            [Wolverine.Attributes.WolverineHandler]
            public static CurrentExplicitCascade Process(CurrentExplicitTrigger message) => new(message.Id);
        }

        public class LegacyExplicitActions
        {
            [Wolverine.WolverineHandler]
            public static LegacyExplicitCascade Process(LegacyExplicitTrigger message) => new(message.Id);
        }

        public static class SuppressedSuffixHandler
        {
            public static SuppressedSuffixCascade Handle(SuppressedSuffixTrigger message) => new(message.Id);
        }

        public static class SuppressedConsumer
        {
            public static SuppressedConsumerCascade Handle(SuppressedConsumerTrigger message) => new(message.Id);
        }

        public class SuppressedInterfaceWorker : Wolverine.IWolverineHandler
        {
            public SuppressedInterfaceCascade Handle(SuppressedInterfaceTrigger message) => new(message.Id);
        }

        [Wolverine.Attributes.WolverineHandler]
        public static class SuppressedCurrentType
        {
            public static SuppressedCurrentTypeCascade Handle(SuppressedCurrentTypeTrigger message) => new(message.Id);
        }

        [Wolverine.WolverineHandler]
        public static class SuppressedLegacyType
        {
            public static SuppressedLegacyTypeCascade Handle(SuppressedLegacyTypeTrigger message) => new(message.Id);
        }

        public static class SuppressedMethodActions
        {
            [Wolverine.Attributes.WolverineHandler]
            public static SuppressedMethodCascade Process(SuppressedMethodTrigger message) => new(message.Id);
        }

        [Wolverine.Attributes.WolverineIgnore]
        public class CurrentIgnoredHandler
        {
            public static CurrentIgnoredCascade Handle(CurrentIgnoredTrigger message) => new(message.Id);
        }

        [Wolverine.WolverineIgnore]
        public class LegacyIgnoredHandler
        {
            public static LegacyIgnoredCascade Handle(LegacyIgnoredTrigger message) => new(message.Id);
        }

        public class CurrentMethodIgnoredActions
        {
            [Wolverine.Attributes.WolverineHandler]
            [Wolverine.Attributes.WolverineIgnore]
            public static CurrentMethodIgnoredCascade Process(CurrentMethodIgnoredTrigger message) => new(message.Id);
        }

        public class LegacyMethodIgnoredActions
        {
            [Wolverine.WolverineHandler]
            [Wolverine.WolverineIgnore]
            public static LegacyMethodIgnoredCascade Process(LegacyMethodIgnoredTrigger message) => new(message.Id);
        }

        public class CompoundActions
        {
            public static MiddlewareCascade Before(MiddlewareTrigger message) => new(message.Id);
            public static void Handle(CompoundTrigger message) { }
        }

        public sealed class IncludedSaga : Wolverine.Saga
        {
            public static IncludedSaga Start(BeginIncludedSaga message) => new();
            public void Handle(IncludedSagaTrigger message) { }
        }

        public sealed class SuppressedSaga : Wolverine.Saga
        {
            public static SuppressedSaga Start(BeginSuppressedSaga message) => new();
            public void Handle(SuppressedSagaTrigger message) { }
        }
        """;

    const string UnresolvedApplicationSource =
        """
        namespace UnresolvedDiscovery;

        public record ConventionalTrigger(System.Guid Id);
        public record ConventionalCascade(System.Guid Id);
        public record ExplicitTrigger(System.Guid Id);
        public record ExplicitCascade(System.Guid Id);

        public static class Configuration
        {
            public static void Configure(Wolverine.WolverineOptions options)
            {
                options.Discovery.CustomizeHandlerDiscovery(query =>
                    query.Excludes.WithNameSuffix("Handler"));
                options.Discovery.IncludeAssembly(typeof(string).Assembly);
                options.Discovery.IncludeType<ExplicitActions>();
            }
        }

        public static class ConventionalHandler
        {
            public static ConventionalCascade Handle(ConventionalTrigger message) => new(message.Id);
        }

        public class ExplicitActions
        {
            [Wolverine.Attributes.WolverineHandler]
            public static ExplicitCascade Process(ExplicitTrigger message) => new(message.Id);
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected DotNetProjectCompilation UnresolvedProject = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "ConfiguredDiscovery",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/ConfiguredDiscovery/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "ConfiguredDiscovery",
            ProjectPath = "/workspace/ConfiguredDiscovery/ConfiguredDiscovery.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };

        var unresolvedCompilation = CSharpCompilation.Create(
            "UnresolvedDiscovery",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(UnresolvedApplicationSource, path: "/workspace/UnresolvedDiscovery/Handlers.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        UnresolvedProject = new DotNetProjectCompilation
        {
            Name = "UnresolvedDiscovery",
            ProjectPath = "/workspace/UnresolvedDiscovery/UnresolvedDiscovery.csproj",
            SourceRoot = "/workspace",
            Compilation = unresolvedCompilation,
            AuthoredSyntaxTrees = unresolvedCompilation.SyntaxTrees.ToHashSet()
        };
    }
}
