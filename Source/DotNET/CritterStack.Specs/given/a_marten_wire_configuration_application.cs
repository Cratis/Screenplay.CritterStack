// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_wire_configuration_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx.Events
        {
            public enum EventAppendMode
            {
                Quick,
                QuickWithServerTimestamps,
                Rich
            }

            public enum StreamIdentity
            {
                AsGuid,
                AsString
            }

            public interface IEventBinarySerializer;
        }

        namespace Marten
        {
            public class StoreOptions
            {
                public Marten.Events.IEventStoreOptions Events { get; } = default!;
                public void RegisterValueType<T>() { }
                public void RegisterValueType(System.Type type) { }
            }
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                JasperFx.Events.EventAppendMode AppendMode { get; set; }
                JasperFx.Events.StreamIdentity StreamIdentity { get; set; }
                void UseBinarySerializer<T>(JasperFx.Events.IEventBinarySerializer serializer);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace MartenWireConfiguration;

        public record AlertId(string Value);
        public record OtherId(string Value);
        public record WireEvent(string Id);
        public class CustomBinarySerializer : JasperFx.Events.IEventBinarySerializer;
        """;

    const string ConfigurationSource =
        """
        namespace MartenWireConfiguration;

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.RegisterValueType<AlertId>();
                options.RegisterValueType(typeof(OtherId));
                options.Events.UseBinarySerializer<WireEvent>(new CustomBinarySerializer());
                options.Events.AppendMode = JasperFx.Events.EventAppendMode.Quick;
                options.Events.StreamIdentity = JasperFx.Events.StreamIdentity.AsString;
            }
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
        BaselineProject = CreateProject(includeConfiguration: false);
        Project = CreateProject(includeConfiguration: true);
        var adapter = new CritterStackScreenplayAdapter();
        BaselineContribution = adapter.Analyze(new DotNetAnalysisContext([BaselineProject]), new DotNetAdapterOptions());
        Contribution = adapter.Analyze(new DotNetAnalysisContext([Project]), new DotNetAdapterOptions());
    }

    static DotNetProjectCompilation CreateProject(bool includeConfiguration)
    {
        var trees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/MartenWireConfiguration/Types.cs")
        };
        if (includeConfiguration)
        {
            trees.Add(CSharpSyntaxTree.ParseText(ConfigurationSource, path: "/workspace/MartenWireConfiguration/Configuration.cs"));
        }

        var compilation = CSharpCompilation.Create(
            "MartenWireConfiguration",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = "MartenWireConfiguration",
            ProjectPath = "/workspace/MartenWireConfiguration/MartenWireConfiguration.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
