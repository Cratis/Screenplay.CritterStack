// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Projection side effects that publish the same message repeatedly, from several projections, beside custom
/// ISendMyself payloads, timeout messages (which are exempt), an impostor interface and unresolved payloads.
/// </summary>
public class a_projection_side_effect_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine
        {
            public interface IMessageContext;
            public interface ISendMyself { System.Threading.Tasks.ValueTask ApplyAsync(IMessageContext context); }
            public abstract record TimeoutMessage : ISendMyself
            {
                public System.Threading.Tasks.ValueTask ApplyAsync(IMessageContext context) => default;
            }
        }

        namespace Marten
        {
            public class StoreOptions;
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T>;
        }

        namespace JasperFx.Events
        {
            public sealed class MessageMetadata;

            public interface IEventSlice<T>
            {
                void PublishMessage(object message);
                void PublishMessage(object message, MessageMetadata metadata);
            }
        }
        """;

    const string VariantsSource =
        """
        namespace SideEffectVariants;

        public record Summary(string Id);
        public record Opened(string Id);
        public record Closed(string Id);
        public record Announced(string Id);
        public record Escalated(string Id);
        public record Reminder : Wolverine.TimeoutMessage;
        public interface ISendMyself;
        public record Impostor : ISendMyself;
        public record Delivery : Wolverine.ISendMyself
        {
            public System.Threading.Tasks.ValueTask ApplyAsync(Wolverine.IMessageContext context) => default;
        }

        public class FirstProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary>
        {
            public Summary Apply(Opened opened, Summary current, JasperFx.Events.IEventSlice<Summary> slice)
            {
                slice.PublishMessage(new Announced(opened.Id));
                slice.PublishMessage(new Announced(opened.Id));
                slice.PublishMessage(new Delivery());
                slice.PublishMessage(new Reminder());
                slice.PublishMessage(new Impostor());
                return current;
            }

            public Summary Apply(Closed closed, Summary current, JasperFx.Events.IEventSlice<Summary> slice)
            {
                slice.PublishMessage(new Announced(closed.Id), new JasperFx.Events.MessageMetadata());
                slice.PublishMessage(message: new Escalated(closed.Id));
                Announced indirect = new(closed.Id);
                slice.PublishMessage(indirect);
                object computed = new Escalated(closed.Id);
                slice.PublishMessage(computed);
                slice.PublishMessage(new Delivery(), new JasperFx.Events.MessageMetadata());
                return current;
            }
        }

        public class SecondProjection : Marten.Events.Aggregation.SingleStreamProjection<Summary>
        {
            public Summary Apply(Opened opened, Summary current, JasperFx.Events.IEventSlice<Summary> slice)
            {
                slice.PublishMessage(new Escalated(opened.Id));
                slice.PublishMessage(new Announced(opened.Id));
                slice.PublishMessage(new Delivery());
                return current;
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
        var variantsTree = CSharpSyntaxTree.ParseText(VariantsSource, path: "/workspace/SideEffectVariants/Variants.cs");
        var compilation = CSharpCompilation.Create(
            "SideEffectVariants",
            [frameworkTree, variantsTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "SideEffectVariants",
            ProjectPath = "/workspace/SideEffectVariants/SideEffectVariants.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, variantsTree }
        };
        VariantContribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
