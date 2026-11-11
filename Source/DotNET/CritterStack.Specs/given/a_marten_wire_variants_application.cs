// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Binary serializer and wire-setting declarations with resolved and unresolved serializers and named, constant and
/// computed enum values, written as direct, static-call and compound forms, in a deliberate source order.
/// </summary>
public class a_marten_wire_variants_application : Specification
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
            public class StoreOptions;
        }

        namespace Marten.Events
        {
            public interface IEventStoreOptions
            {
                JasperFx.Events.EventAppendMode AppendMode { get; set; }
                JasperFx.Events.StreamIdentity StreamIdentity { get; set; }
                bool EnableSideEffectsOnInlineProjections { get; set; }
                void UseBinarySerializer<T>(JasperFx.Events.IEventBinarySerializer serializer);
                void UseBinarySerializer<T>();
            }
        }
        """;

    const string VariantsSource =
        """
        using JasperFx.Events;

        namespace WireVariants;

        public sealed record FirstEvent;
        public sealed record SecondEvent;
        public sealed record ThirdEvent;
        public sealed record FourthEvent;
        public sealed class KnownSerializer : IEventBinarySerializer;
        public sealed class OtherSerializer : IEventBinarySerializer;

        public static class Configuration
        {
            public const EventAppendMode ConstantMode = EventAppendMode.Rich;

            public static void Configure(
                Marten.Events.IEventStoreOptions events,
                IEventBinarySerializer computedSerializer,
                EventAppendMode computedMode,
                StreamIdentity computedIdentity)
            {
                events.UseBinarySerializer<FirstEvent>(new KnownSerializer());
                events.UseBinarySerializer<SecondEvent>(computedSerializer);
                events.UseBinarySerializer<ThirdEvent>(serializer: new OtherSerializer());
                events.UseBinarySerializer<FourthEvent>();

                events.AppendMode = EventAppendMode.Quick;
                events.AppendMode = ConstantMode;
                events.AppendMode = (EventAppendMode)1;
                events.AppendMode = (EventAppendMode)7;
                events.AppendMode = computedMode;
                events.StreamIdentity = StreamIdentity.AsString;
                events.StreamIdentity = (StreamIdentity)0;
                events.StreamIdentity = computedIdentity;
                events.EnableSideEffectsOnInlineProjections = true;
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution VariantContribution = null!;

    void Establish()
    {
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var variantsTree = CSharpSyntaxTree.ParseText(VariantsSource, path: "/workspace/WireVariants/Variants.cs");
        var compilation = CSharpCompilation.Create(
            "WireVariants",
            [frameworkTree, variantsTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "WireVariants",
            ProjectPath = "/workspace/WireVariants/WireVariants.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, variantsTree }
        };
        VariantContribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
