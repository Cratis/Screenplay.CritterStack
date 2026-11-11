// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// One isolated saga per behavior of Wolverine saga discovery: the correlation member precedence, role admission,
/// chain and constructor rules, method and message ordering across files, completion and timeout reporting. Every
/// saga owns its messages so its diagnostics and facts can be pinned on their own.
/// </summary>
public class a_wolverine_saga_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine.Attributes
        {
            public class WolverineIgnoreAttribute : System.Attribute;
        }

        namespace Wolverine.Persistence.Sagas
        {
            [System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
            public class SagaIdentityAttribute : System.Attribute;

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class SagaIdentityFromAttribute(string propertyName) : System.Attribute
            {
                public string PropertyName { get; } = propertyName;
            }
        }

        namespace Wolverine
        {
            public class WolverineIgnoreAttribute : System.Attribute;
            public class WolverineOptions;
            public abstract class Saga
            {
                protected void MarkCompleted() { }
            }
            public abstract record TimeoutMessage(System.TimeSpan DelayTime) : Wolverine.Configuration.IWolverineReturnType;
        }
        """;

    const string CorrelationSource =
        """
        using Wolverine.Persistence.Sagas;

        namespace Variants;

        public class AttributeMessage { [SagaIdentity] public System.Guid Chosen { get; set; } public System.Guid AttributeSagaId { get; set; } public System.Guid SagaId { get; set; } public System.Guid Id { get; set; } }
        public class AttributeSaga : Wolverine.Saga { public void Handle(AttributeMessage message) { } }

        public class AttributeAmbiguousMessage { [SagaIdentity] public System.Guid First { get; set; } [SagaIdentity] public System.Guid Second { get; set; } public System.Guid AttributeAmbiguousSagaId { get; set; } }
        public class AttributeAmbiguousSaga : Wolverine.Saga { public void Handle(AttributeAmbiguousMessage message) { } }

        public class AttrFromMessage { [SagaIdentity] public System.Guid Chosen { get; set; } public System.Guid Other { get; set; } public System.Guid AttrFromSagaId { get; set; } }
        public class AttrFromSaga : Wolverine.Saga { public void Handle([SagaIdentityFrom("Other")] AttrFromMessage message) { } }

        public class FromMessage { public System.Guid Picked { get; set; } public System.Guid FromSagaId { get; set; } public System.Guid SagaId { get; set; } }
        public class FromSaga : Wolverine.Saga { public void Handle([SagaIdentityFrom("Picked")] FromMessage message) { } }

        public class FromConflictMessage { public System.Guid Primary { get; set; } public System.Guid Secondary { get; set; } }
        public class FromConflictSaga : Wolverine.Saga
        {
            public void Handle(FromConflictMessage message, [SagaIdentityFrom("Secondary")] int context) { }
            public void Handles(FromConflictMessage message, [SagaIdentityFrom("Primary")] string context) { }
        }

        public class FromRepeatedMessage { public System.Guid Primary { get; set; } public System.Guid Secondary { get; set; } }
        public class FromRepeatedSaga : Wolverine.Saga
        {
            public void Handle(FromRepeatedMessage message, [SagaIdentityFrom("Primary")] int context) { }
            public void Handles(FromRepeatedMessage message, [SagaIdentityFrom("Primary")] string context) { }
        }

        public class FromMissingMessage { public System.Guid FromMissingSagaId { get; set; } public System.Guid FromMissingId { get; set; } public System.Guid SagaId { get; set; } public System.Guid Id { get; set; } }
        public class FromMissingSaga : Wolverine.Saga { public void Handle([SagaIdentityFrom("Nope")] FromMissingMessage message) { } }

        public class FromAmbiguousBase { public System.Guid Picked { get; set; } }
        public class FromAmbiguousMessage : FromAmbiguousBase { public new System.Guid Picked { get; set; } public System.Guid SagaId { get; set; } }
        public class FromAmbiguousSaga : Wolverine.Saga { public void Handle([SagaIdentityFrom("Picked")] FromAmbiguousMessage message) { } }

        public class FullNameMessage { public System.Guid FullNameSagaId { get; set; } public System.Guid FullNameId { get; set; } public System.Guid SagaId { get; set; } public System.Guid Id { get; set; } }
        public class FullNameSaga : Wolverine.Saga { public void Handle(FullNameMessage message) { } }

        public class FullAmbiguousBase { public System.Guid FullAmbiguousSagaId { get; set; } }
        public class FullAmbiguousMessage : FullAmbiguousBase { public new System.Guid FullAmbiguousSagaId { get; set; } public System.Guid SagaId { get; set; } }
        public class FullAmbiguousSaga : Wolverine.Saga { public void Handle(FullAmbiguousMessage message) { } }

        public class StrippedMessage { public System.Guid StrippedId { get; set; } public System.Guid SagaId { get; set; } public System.Guid Id { get; set; } }
        public class StrippedSaga : Wolverine.Saga { public void Handle(StrippedMessage message) { } }

        public class StrippedAnywhereMessage { public System.Guid OrderId { get; set; } public System.Guid SagaId { get; set; } }
        public class OrdersagaSaga : Wolverine.Saga { public void Handle(StrippedAnywhereMessage message) { } }

        public class StrippedAmbiguousBase { public System.Guid StrippedAmbiguousId { get; set; } }
        public class StrippedAmbiguousMessage : StrippedAmbiguousBase { public new System.Guid StrippedAmbiguousId { get; set; } public System.Guid SagaId { get; set; } }
        public class StrippedAmbiguousSaga : Wolverine.Saga { public void Handle(StrippedAmbiguousMessage message) { } }

        public class SagaIdConventionMessage { public System.Guid SagaId { get; set; } public System.Guid Id { get; set; } }
        public class SagaIdConventionSaga : Wolverine.Saga { public void Handle(SagaIdConventionMessage message) { } }

        public class SagaIdAmbiguousBase { public System.Guid SagaId { get; set; } }
        public class SagaIdAmbiguousMessage : SagaIdAmbiguousBase { public new System.Guid SagaId { get; set; } public System.Guid Id { get; set; } }
        public class SagaIdAmbiguousSaga : Wolverine.Saga { public void Handle(SagaIdAmbiguousMessage message) { } }

        public class IdMessage { public System.Guid ID { get; set; } }
        public class IdSaga : Wolverine.Saga { public void Handle(IdMessage message) { } }

        public class IdAmbiguousMessage { public System.Guid Id { get; set; } public System.Guid ID { get; set; } }
        public class IdAmbiguousSaga : Wolverine.Saga { public void Handle(IdAmbiguousMessage message) { } }

        public class NoCorrelationMessage { public string Name { get; set; } = string.Empty; }
        public class NoCorrelationSaga : Wolverine.Saga { public void Handle(NoCorrelationMessage message) { } }

        public class MemberKindsMessage { internal System.Guid SagaId { get; set; } public static System.Guid MemberKindsSagaId { get; set; } public System.Guid this[int index] => default; public System.Guid Id { get; set; } }
        public class MemberKindsSaga : Wolverine.Saga { public void Handle(MemberKindsMessage message) { } }

        public class FieldMessage { public System.Guid FieldSagaId; public System.Guid Id { get; set; } }
        public class FieldSaga : Wolverine.Saga { public void Handle(FieldMessage message) { } }

        public class FieldBeforePropertyBase { public System.Guid SagaId; }
        public class FieldBeforePropertyMessage : FieldBeforePropertyBase { public new System.Guid SagaId { get; set; } }
        public class FieldBeforePropertySaga : Wolverine.Saga { public void Handle(FieldBeforePropertyMessage message) { } }

        public partial class GeneratedMemberMessage { public System.Guid Id { get; set; } }
        public class GeneratedMemberSaga : Wolverine.Saga { public void Handle(GeneratedMemberMessage message) { } }
        """;

    const string GeneratedSource =
        """
        // <auto-generated/>
        namespace Variants;

        public partial class GeneratedMemberMessage { public System.Guid GeneratedMemberSagaId { get; set; } }
        """;

    const string RoleSource =
        """
        namespace Variants;

        public record RoleMessage(System.Guid SagaId);
        public record SecondRoleMessage(System.Guid SagaId);

        public class RoleShapesSaga : Wolverine.Saga
        {
            public System.Guid SagaId { get; set; }

            private void Handle(RoleMessage message) { }
            public void Handle<T>(RoleMessage message) { }
            public void Handle() { }
            public int Handle(SecondRoleMessage message) => 0;
            public void Handles(object message) { }
            public void Handles(int message) { }
            public void Handles(string message) { }
            public void Consume(System.Guid[] message) { }
            public void Consumes(RoleShapesSaga message) { }
            public static void Orchestrate(RoleMessage message) { }
            public static void StartOrHandle(RoleMessage message) { }
            public void NotFound(RoleMessage message) { }
            public void Orchestrates(System.Collections.Generic.List<RoleMessage> message) { }
            [Wolverine.Attributes.WolverineIgnore] public void OrchestrateAsync(RoleMessage message) { }
            [Wolverine.WolverineIgnore] public void StartsOrHandles(RoleMessage message) { }
            public void handle(RoleMessage message) { }
            public void Other(RoleMessage message) { }
            public void HandleAsync(RoleMessage message) { }
        }

        public record NoChainMessage(System.Guid SagaId);
        public record ChainedMessage(System.Guid SagaId);
        public class ChainSaga : Wolverine.Saga
        {
            public static ChainSaga Starts(NoChainMessage message) => new();
            public static ChainSaga Starts(ChainedMessage message) => new();
            public void Handles(ChainedMessage message) { }
        }

        public record CtorMixedMessage(System.Guid SagaId);
        public record CtorAloneMessage(System.Guid SagaId);
        public class CtorStartOrHandleSaga : Wolverine.Saga
        {
            CtorStartOrHandleSaga() { }
            public void StartOrHandle(CtorMixedMessage message) { }
            public void Handle(CtorMixedMessage message) { }
            public void StartOrHandle(CtorAloneMessage message) { }
        }

        public record CtorReturnedMessage(System.Guid SagaId);
        public record CtorVoidMessage(System.Guid SagaId);
        public record CtorOrchestratedMessage(System.Guid SagaId);
        public record CtorAsyncMessage(System.Guid SagaId);
        public class CtorStartSaga : Wolverine.Saga
        {
            CtorStartSaga() { }
            public static CtorStartSaga Start(CtorReturnedMessage message) => new();
            public static void Start(CtorVoidMessage message) { }
            public static CtorStartSaga Start(CtorOrchestratedMessage message) => new();
            public void Handle(CtorOrchestratedMessage message) { }
            public static System.Threading.Tasks.Task<CtorStartSaga> StartAsync(CtorAsyncMessage message) => System.Threading.Tasks.Task.FromResult(new CtorStartSaga());
        }

        public record CtorNotFoundAloneMessage(System.Guid SagaId);
        public record CtorNotFoundHandledMessage(System.Guid SagaId);
        public class CtorNotFoundSaga : Wolverine.Saga
        {
            CtorNotFoundSaga() { }
            public static void NotFound(CtorNotFoundAloneMessage message) { }
            public static void NotFound(CtorNotFoundHandledMessage message) { }
            public void Handle(CtorNotFoundHandledMessage message) { }
        }
        """;

    const string OrderingZetaSource =
        """
        namespace Variants;

        public record OrderZMessage(System.Guid SagaId);
        public record OrderAMessage(System.Guid SagaId);

        public partial class ZetaSaga : Wolverine.Saga
        {
            public void Handle(OrderZMessage message) { }
            public void Handle(OrderAMessage message, string context) { }
        }
        """;

    const string OrderingAlphaSource =
        """
        namespace Variants;

        public partial class ZetaSaga
        {
            public void Handles(OrderZMessage message) { }
            public void Handle(OrderAMessage message) { }
        }

        public class AlphaSaga : Wolverine.Saga
        {
            public void Handle(OrderAMessage message) { }
        }
        """;

    const string CompletionSource =
        """
        namespace Variants;

        public record CompletionFirstMessage(System.Guid SagaId);
        public record CompletionSecondMessage(System.Guid SagaId);
        public class CompletionSaga : Wolverine.Saga
        {
            public void Handle(CompletionSecondMessage message) { var later = new System.Action(() => MarkCompleted()); later(); MarkCompleted(); }
            public void Handle(CompletionFirstMessage message) { MarkCompleted(); }
        }

        public record NotCompletedMessage(System.Guid SagaId);
        public class Closer { public void MarkCompleted() { } }
        public class NotCompletedSaga : Wolverine.Saga
        {
            public void Handle(NotCompletedMessage message) { new Closer().MarkCompleted(); }
        }

        public record ReminderTimeout(System.TimeSpan Delay) : Wolverine.TimeoutMessage(Delay);
        public record ExpiryTimeout(System.TimeSpan Delay) : Wolverine.TimeoutMessage(Delay);
        public record TimeoutMessageIn(System.Guid SagaId);
        public class TimeoutSaga : Wolverine.Saga
        {
            public (ReminderTimeout, ExpiryTimeout, ReminderTimeout) Handle(TimeoutMessageIn message) => default;
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected IReadOnlyList<string> Lines = [];

    protected static string[] For(IReadOnlyList<string> lines, string saga) =>
        [.. lines.Where(_ => _.Contains($"Variants.{saga}", StringComparison.Ordinal) || _.Contains($"'{saga}'", StringComparison.Ordinal) || _.Contains($"'{saga}.", StringComparison.Ordinal))];

    DotNetProjectCompilation _project = null!;

    void Establish() =>
        _project = Project(
            ("/workspace/Saga/Framework.cs", FrameworkSource),
            ("/workspace/Saga/Correlation.cs", CorrelationSource),
            ("/workspace/Saga/Generated.cs", GeneratedSource),
            ("/workspace/Saga/Roles.cs", RoleSource),
            ("/workspace/Saga/Zeta.cs", OrderingZetaSource),
            ("/workspace/Saga/Alpha.cs", OrderingAlphaSource),
            ("/workspace/Saga/Completion.cs", CompletionSource));

    protected void Analyze()
    {
        var contribution = new CritterStackScreenplayAdapter().Analyze(new DotNetAnalysisContext([_project]), new DotNetAdapterOptions());
        Lines =
        [
            .. diagnostic_ranges.Of(contribution).Select(_ => "diagnostic " + _),
            .. contribution.Facts.Select(Describe).OfType<string>()
        ];
        Lines = [.. Lines.Select(Normalize)];
    }

    static string Normalize(string line) => line
        .Replace("dotnet://Variants/Variants/Variants.", "~", StringComparison.Ordinal)
        .Replace(LifecycleText, "<lifecycle>", StringComparison.Ordinal)
        .Replace("wolverine:saga-handles:", string.Empty, StringComparison.Ordinal);

    const string LifecycleText = "uses Wolverine-managed lifecycle. Authored source does not safely establish a portable domain workflow; neutral Saga/Handler/Handles facts are retained as realization/provenance, and Screenplay uses ordinary Event Modeling building blocks";

    static string? Describe(GenerationFact fact) => fact switch
    {
        RelationshipFact { Definition.Key.Discriminator: { } discriminator } relationship when discriminator.StartsWith("wolverine:saga", StringComparison.Ordinal) =>
            $"relationship {relationship.Id.Value}|{relationship.Evidence.Strength}|{relationship.Evidence.Explanation}|{Range(relationship.Evidence.Source)}|{relationship.Definition.TargetMember}",
        ArtifactFact { Definition.Key.Kind: ArtifactKind.Saga } artifact =>
            $"saga {artifact.Id.Value}|{string.Join(',', artifact.Definition.Properties.Select(_ => _.Name))}",
        _ => null
    };

    static string Range(SourceRange? source) => source is null
        ? "no-source"
        : $"{source.Path}:{source.StartLine}:{source.StartColumn}-{source.EndLine}:{source.EndColumn}";

    static DotNetProjectCompilation Project(params (string Path, string Source)[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "Variants",
            sources.Select(_ => CSharpSyntaxTree.ParseText(_.Source, path: _.Path)),
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return new()
        {
            Name = "Variants",
            ProjectPath = "/workspace/Saga/Variants.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
        };
    }
}
