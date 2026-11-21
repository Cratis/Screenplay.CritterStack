// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public abstract class a_read_model_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public class WolverineOptions;
            public abstract class Saga;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
            public class DeciderFunctionAttribute : System.Attribute;
            public enum ModelConcurrencyStyle { Optimistic, Exclusive }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class WriteModelAttribute : System.Attribute
            {
                public WriteModelAttribute() { }
                public WriteModelAttribute(string? name) { }
                public string? VersionSource { get; set; }
                public ModelConcurrencyStyle LoadStyle { get; set; }
                public bool AlwaysEnforceConsistency { get; set; }
            }

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class ReadModelAttribute : System.Attribute
            {
                public ReadModelAttribute() { }
                public ReadModelAttribute(string? argumentName) { }
                public bool Required { get; set; } = true;
            }
        }

        namespace Wolverine.Marten
        {
            public enum ConcurrencyStyle { Optimistic, Exclusive }
            public class WriteAggregateAttribute : Wolverine.Persistence.EventSourcing.WriteModelAttribute
            {
                public WriteAggregateAttribute() { }
                public WriteAggregateAttribute(string? name) : base(name) { }
            }
            public class ReadAggregateAttribute : Wolverine.Persistence.EventSourcing.ReadModelAttribute
            {
                public ReadAggregateAttribute() { }
                public ReadAggregateAttribute(string? name) : base(name) { }
            }
            public class AggregateHandlerAttribute : Wolverine.Persistence.EventSourcing.DeciderFunctionAttribute
            {
                public AggregateHandlerAttribute() { }
                public AggregateHandlerAttribute(ConcurrencyStyle style) { }
                public bool AlwaysEnforceConsistency { get; set; }
            }
            public class ConsistentAggregateHandlerAttribute : AggregateHandlerAttribute
            {
                public ConsistentAggregateHandlerAttribute() => AlwaysEnforceConsistency = true;
                public ConsistentAggregateHandlerAttribute(ConcurrencyStyle style) : base(style) => AlwaysEnforceConsistency = true;
            }
            public class ConsistentAggregateAttribute : WriteAggregateAttribute
            {
                public ConsistentAggregateAttribute() => AlwaysEnforceConsistency = true;
                public ConsistentAggregateAttribute(string? name) : base(name) => AlwaysEnforceConsistency = true;
            }
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute;
            public class WolverinePostAttribute(string route) : WolverineHttpMethodAttribute(route);
        }

        namespace Wolverine.Http.Marten
        {
            public class AggregateAttribute : Wolverine.Marten.WriteAggregateAttribute;
            public class ConsistentAggregateAttribute : Wolverine.Marten.ConsistentAggregateAttribute
            {
                public ConsistentAggregateAttribute() { }
                public ConsistentAggregateAttribute(string? name) : base(name) { }
            }
        }

        namespace JasperFx.Events
        {
            public interface IEventStream<T>
            {
                T? Aggregate { get; }
                void AppendOne(object @event);
                void AppendMany(params object[] events);
            }
        }

        namespace JasperFx.Events.Aggregation
        {
            [System.AttributeUsage(System.AttributeTargets.Property)]
            public class NaturalKeyAttribute : System.Attribute;
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class BoundaryAggregateAttribute : System.Attribute;
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
    ];

    protected DotNetProjectCompilation Project = null!;
    protected GeneratedScreenplayDefinition Result = null!;
    protected AdapterContribution Contribution = null!;

    protected abstract string ApplicationSource { get; }

    protected virtual string GeneratedSource => string.Empty;

    void Establish()
    {
        var framework = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var application = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Aggregates/Handlers.cs");
        var generated = CSharpSyntaxTree.ParseText(GeneratedSource, path: "/workspace/Aggregates/Generated.g.cs");
        Project = new DotNetProjectCompilation
        {
            Name = "Aggregates",
            ProjectPath = "/workspace/Aggregates/Aggregates.csproj",
            SourceRoot = "/workspace",
            Compilation = CSharpCompilation.Create(
                "Aggregates",
                [framework, application, generated],
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)),
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { framework, application }
        };
    }

    protected void Generate()
    {
        Contribution = new CritterStackScreenplayAdapter().Analyze(new([Project]), new());
        Result = new CritterStackScreenplayGenerator().Generate([Project], new CritterStackScreenplayOptions { Domain = "Aggregates" });
    }
}
