// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_wolverine_saga_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine.Attributes
        {
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
        }

        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
            public class HandlerDiscovery
            {
                public HandlerDiscovery DisableConventionalDiscovery(bool value = true) => this;
                public HandlerDiscovery IncludeType<T>() => this;
                public HandlerDiscovery IncludeType(System.Type type) => this;
            }
        }

        namespace Wolverine.Persistence.EventSourcing
        {
            public class EventsToAppend : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Http
        {
            public abstract class WolverineHttpMethodAttribute(string route) : System.Attribute
            {
                public string Route { get; } = route;
            }
            public sealed class WolverineGetAttribute(string route) : WolverineHttpMethodAttribute(route);
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
            public class WolverineOptions
            {
                public Wolverine.Configuration.HandlerDiscovery Discovery { get; } = new();
            }
            public class WolverineHandlerAttribute : System.Attribute;
            public class WolverineIgnoreAttribute : System.Attribute;
            public abstract class Saga
            {
                protected void MarkCompleted() { }
                public bool IsCompleted() => false;
                public int Version { get; set; }
            }
            public abstract record TimeoutMessage(System.TimeSpan DelayTime) : Wolverine.Configuration.IWolverineReturnType;
            public interface IResponseAware : Wolverine.Configuration.IWolverineReturnType;
            public interface ISideEffect : Wolverine.Configuration.IWolverineReturnType;
            public class OutgoingMessages : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType;
            public class DeliveryOptions
            {
                public System.TimeSpan? ScheduleDelay { get; set; }
            }
            public interface ICommandBus
            {
                System.Threading.Tasks.Task<T> InvokeAsync<T>(object message);
            }
            public interface IMessageBus : ICommandBus
            {
                System.Threading.Tasks.ValueTask SendAsync<T>(T message, DeliveryOptions? options = null);
                System.Threading.Tasks.ValueTask PublishAsync<T>(T message, DeliveryOptions? options = null);
            }
            public static class MessageBusExtensions
            {
                public static System.Threading.Tasks.ValueTask ScheduleAsync<T>(this IMessageBus bus, T message, System.TimeSpan delay) => default;
            }
        }

        namespace Marten
        {
            public interface IDocumentSession
            {
                void Store<T>(T document);
                void Update<T>(T document);
                void Delete<T>(System.Guid id);
            }
        }
        """;

    const string ApplicationSource =
        """
        using Wolverine;
        using Wolverine.Http;
        using Wolverine.Persistence.Sagas;

        namespace Orders;

        public record StartMessage(System.Guid SagaId);
        public record StartAsyncMessage(System.Guid SagaId);
        public record StartsMessage(System.Guid SagaId);
        public record StartsAsyncMessage(System.Guid SagaId);
        public record StartOrHandleMessage(System.Guid SagaId);
        public record StartOrHandleAsyncMessage(System.Guid SagaId);
        public record StartsOrHandlesMessage(System.Guid SagaId);
        public record StartsOrHandlesAsyncMessage(System.Guid SagaId);
        public record OrchestrateMessage(System.Guid SagaId);
        public record OrchestrateAsyncMessage(System.Guid SagaId);
        public record OrchestratesMessage(System.Guid SagaId);
        public record OrchestratesAsyncMessage(System.Guid SagaId);
        public record HandleMessage(System.Guid SagaId);
        public record HandleAsyncMessage(System.Guid SagaId);
        public record HandlesMessage(System.Guid SagaId);
        public record HandlesAsyncMessage(System.Guid SagaId);
        public record ConsumeMessage(System.Guid SagaId);
        public record ConsumeAsyncMessage(System.Guid SagaId);
        public record ConsumesMessage(System.Guid SagaId);
        public record ConsumesAsyncMessage(System.Guid SagaId);
        public record NotFoundMessage(System.Guid SagaId);
        public record NotFoundAsyncMessage(System.Guid SagaId);

        public partial class RoleSaga : Wolverine.Saga
        {
            public System.Guid Id { get; set; }
            public string Status { get; set; } = string.Empty;

            public static RoleSaga Start(StartMessage message) => new() { Id = message.SagaId };
            public static System.Threading.Tasks.Task<RoleSaga> StartAsync(StartAsyncMessage message) => System.Threading.Tasks.Task.FromResult(new RoleSaga { Id = message.SagaId });
            public static RoleSaga Starts(StartsMessage message) => new() { Id = message.SagaId };
            public static System.Threading.Tasks.Task<RoleSaga> StartsAsync(StartsAsyncMessage message) => System.Threading.Tasks.Task.FromResult(new RoleSaga { Id = message.SagaId });
            public void StartOrHandle(StartOrHandleMessage message) { }
            public System.Threading.Tasks.Task StartOrHandleAsync(StartOrHandleAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void StartsOrHandles(StartsOrHandlesMessage message) { }
            public System.Threading.Tasks.Task StartsOrHandlesAsync(StartsOrHandlesAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Orchestrate(OrchestrateMessage message) { }
            public System.Threading.Tasks.Task OrchestrateAsync(OrchestrateAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Orchestrates(OrchestratesMessage message) { }
            public System.Threading.Tasks.Task OrchestratesAsync(OrchestratesAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Handle(HandleMessage message) { }
            public System.Threading.Tasks.Task HandleAsync(HandleAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Handles(HandlesMessage message) { }
            public System.Threading.Tasks.Task HandlesAsync(HandlesAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Consume(ConsumeMessage message) { }
            public System.Threading.Tasks.Task ConsumeAsync(ConsumeAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public void Consumes(ConsumesMessage message) { }
            public System.Threading.Tasks.Task ConsumesAsync(ConsumesAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
            public static void NotFound(NotFoundMessage message) { }
            public static System.Threading.Tasks.Task NotFoundAsync(NotFoundAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
        }

        public record AttributeIdentityMessage([property: SagaIdentity] System.Guid ExplicitIdentity, System.Guid CorrelationSagaId);
        public record ParameterIdentityMessage(System.Guid Selected, System.Guid CorrelationSagaId);
        public record SecondaryParameterIdentityMessage(System.Guid Selected, System.Guid CorrelationSagaId);
        public record ConflictingParameterIdentityMessage(System.Guid Primary, System.Guid Secondary, System.Guid CorrelationSagaId);
        public record FullNameIdentityMessage(System.Guid CorrelationSagaId, System.Guid CorrelationId);
        public record ShortNameIdentityMessage(System.Guid CorrelationId);
        public record SagaIdentityMessage(System.Guid SagaId, System.Guid Id);
        public record CaseInsensitiveIdentityMessage(System.Guid iD);
        public record MissingExplicitIdentityMessage(System.Guid CorrelationSagaId, System.Guid CorrelationId, System.Guid SagaId, System.Guid Id);
        public class InheritedPropertyIdentityBase
        {
            public System.Guid CorrelationSagaId { get; init; }
        }
        public sealed class InheritedPropertyIdentityMessage : InheritedPropertyIdentityBase;
        public class InheritedFieldIdentityBase
        {
            public System.Guid SagaId;
        }
        public sealed class InheritedFieldIdentityMessage : InheritedFieldIdentityBase;
        public class AmbiguousIdentityBase
        {
            public System.Guid CorrelationId { get; init; }
        }
        public sealed class AmbiguousIdentityMessage : AmbiguousIdentityBase
        {
            public new System.Guid CorrelationId { get; init; }
        }
        public record RuntimeIdentityMessage(string Reference);
        public record BeginCorrelationSaga(System.Guid CorrelationSagaId);

        public sealed class CorrelationSaga : Wolverine.Saga
        {
            public System.Guid Id { get; set; }

            public static CorrelationSaga Start(BeginCorrelationSaga message) => new() { Id = message.CorrelationSagaId };
            public void Handle(AttributeIdentityMessage message) { }
            public void Handle([SagaIdentityFrom(nameof(ParameterIdentityMessage.Selected))] ParameterIdentityMessage message) { }
            public void Handle(SecondaryParameterIdentityMessage message, [SagaIdentityFrom(nameof(SecondaryParameterIdentityMessage.Selected))] string context) { }
            public void Handle(ConflictingParameterIdentityMessage message, [SagaIdentityFrom(nameof(ConflictingParameterIdentityMessage.Primary))] string context) { }
            public void Handles(ConflictingParameterIdentityMessage message, [SagaIdentityFrom(nameof(ConflictingParameterIdentityMessage.Secondary))] int context) { }
            public void Handle(FullNameIdentityMessage message) { }
            public void Handle(ShortNameIdentityMessage message) { }
            public void Handle(SagaIdentityMessage message) { }
            public void Handle(CaseInsensitiveIdentityMessage message) { }
            public void Handle([SagaIdentityFrom("Missing")] MissingExplicitIdentityMessage message) { }
            public void Handle(InheritedPropertyIdentityMessage message) { }
            public void Handle(InheritedFieldIdentityMessage message) { }
            public void Handle(AmbiguousIdentityMessage message) { }
            public void Handle(RuntimeIdentityMessage message) { }
        }

        public record BeginBehavior(System.Guid BehaviorSagaId);
        public record MixedBehavior(System.Guid BehaviorSagaId);
        public record TimeoutTrigger(System.Guid BehaviorSagaId);
        public record CascadeTrigger(System.Guid BehaviorSagaId);
        public record BusTrigger(System.Guid BehaviorSagaId);
        public record OutgoingTrigger(System.Guid BehaviorSagaId);
        public record DocumentTrigger(System.Guid BehaviorSagaId, bool Complete);
        public record ResponseTrigger(System.Guid BehaviorSagaId);
        public record PersistenceTrigger(System.Guid BehaviorSagaId);
        public partial record OrdinaryCascade(System.Guid BehaviorSagaId);
        public partial record DirectSend(System.Guid BehaviorSagaId);
        public record DirectPublish(System.Guid BehaviorSagaId);
        public record DirectSchedule(System.Guid BehaviorSagaId);
        public record DirectRequest(System.Guid BehaviorSagaId);
        public partial record OutgoingImmediate(System.Guid BehaviorSagaId);
        public record OutgoingDelayed(System.Guid BehaviorSagaId);
        public record TimeoutNotice(System.Guid BehaviorSagaId) : Wolverine.TimeoutMessage(System.TimeSpan.FromMinutes(5));
        public sealed class SagaResponse : Wolverine.IResponseAware;
        public sealed class SagaEffect : Wolverine.ISideEffect;
        public sealed record AuditDocument(System.Guid Id);

        public sealed class BehaviorSaga : Wolverine.Saga
        {
            public System.Guid Id { get; set; }
            public static BehaviorSaga Start(BeginBehavior message) => new() { Id = message.BehaviorSagaId };
            public (BehaviorSaga, OrdinaryCascade, SagaResponse, SagaEffect) StartOrHandle(MixedBehavior message) => (this, new(message.BehaviorSagaId), new(), new());
            public TimeoutNotice Handle(TimeoutTrigger message) => new(message.BehaviorSagaId);
            public OrdinaryCascade Handles(CascadeTrigger message) => new(message.BehaviorSagaId);
            public void Consume(BusTrigger message, Wolverine.IMessageBus bus)
            {
                _ = bus.SendAsync(new DirectSend(message.BehaviorSagaId));
                _ = bus.SendAsync(new BehaviorSaga { Id = message.BehaviorSagaId });
                _ = bus.PublishAsync(new DirectPublish(message.BehaviorSagaId));
                _ = bus.PublishAsync(new BehaviorSaga { Id = message.BehaviorSagaId });
                _ = bus.ScheduleAsync(new DirectSchedule(message.BehaviorSagaId), System.TimeSpan.FromMinutes(1));
                _ = bus.ScheduleAsync(new BehaviorSaga { Id = message.BehaviorSagaId }, System.TimeSpan.FromMinutes(1));
                _ = bus.InvokeAsync<string>(new DirectRequest(message.BehaviorSagaId));
                _ = bus.InvokeAsync<string>(new BehaviorSaga { Id = message.BehaviorSagaId });
            }
            public Wolverine.OutgoingMessages Consumes(OutgoingTrigger message) =>
            [
                new OutgoingImmediate(message.BehaviorSagaId),
                new BehaviorSaga { Id = message.BehaviorSagaId },
                new OutgoingDelayed(message.BehaviorSagaId)
            ];
            public void Orchestrate(DocumentTrigger message, Marten.IDocumentSession session)
            {
                var document = new AuditDocument(message.BehaviorSagaId);
                session.Store(document);
                session.Update(document);
                session.Delete<AuditDocument>(message.BehaviorSagaId);
                CompletionUtility.MarkCompleted();
                if (message.Complete)
                {
                    MarkCompleted();
                }
            }
            public (SagaResponse, SagaEffect) Orchestrates(ResponseTrigger message) => (new(), new());
            public (BehaviorSaga, Wolverine.Persistence.EventSourcing.EventsToAppend, OrdinaryCascade) Handle(PersistenceTrigger message) =>
                (this, [this], new(message.BehaviorSagaId));
        }

        public sealed record OrdinaryQueryModel(System.Guid Id);
        public static class SagaQueryEndpoints
        {
            [WolverineGet("/sagas/direct")]
            public static BehaviorSaga Direct() => null!;

            [WolverineGet("/sagas/task")]
            public static System.Threading.Tasks.Task<BehaviorSaga> TaskResult() => null!;

            [WolverineGet("/sagas/collection")]
            public static System.Collections.Generic.IReadOnlyList<BehaviorSaga> Collection() => [];

            [WolverineGet("/ordinary")]
            public static OrdinaryQueryModel Ordinary() => new(System.Guid.Empty);
        }

        public static class CompletionUtility
        {
            public static void MarkCompleted() { }
        }

        public record FilteredMessage(System.Guid SagaId);
        public record IgnoredMethodMessage(System.Guid SagaId);
        public record StaticExistingMessage(System.Guid SagaId);
        public record GenericMethodMessage(System.Guid SagaId);
        public record GeneratedRoleMessage(System.Guid SagaId);
        public record InvalidInstanceNotFoundMessage(System.Guid SagaId);
        public record StaticFallbackStartMessage(System.Guid SagaId);
        public record ExistingOnlyMessage(System.Guid SagaId);
        public record PrimitiveReturnMessage(System.Guid SagaId);
        public record IsolatedStartsMessage(System.Guid SagaId);
        public record IsolatedStartsAsyncMessage(System.Guid SagaId);
        public record IsolatedNotFoundAsyncMessage(System.Guid SagaId);
        public record ContextualStaticMessage(System.Guid SagaId);
        public record ReturnedCreationMessage(System.Guid SagaId);
        public record MissingFallbackConstructorMessage(System.Guid SagaId);
        public record MissingInstanceConstructorMessage(System.Guid SagaId);
        public record ExistingPrivateConstructorMessage(System.Guid SagaId);
        public record NotFoundOnlyPrivateConstructorMessage(System.Guid SagaId);
        public record BeginFilteredSaga(System.Guid SagaId);
        public sealed class FilteredSaga : Wolverine.Saga
        {
            public static FilteredSaga Start(BeginFilteredSaga message) => new();
            public static OrdinaryCascade Start(StaticFallbackStartMessage message) => new(message.SagaId);
            public void Handle(FilteredMessage message) { }
            public void NotFound(InvalidInstanceNotFoundMessage message) { }
            public int Handle(PrimitiveReturnMessage message) => 42;
            [Wolverine.Attributes.WolverineIgnore]
            public void Handle(IgnoredMethodMessage message) { }
            public static void Orchestrate(StaticExistingMessage message) { }
            public void Handle<T>(GenericMethodMessage message) { }
            public void Handle(System.Guid id) { }
            public void Start() { }
        }

        public sealed class ExistingOnlySaga : Wolverine.Saga
        {
            public void Handle(ExistingOnlyMessage message) { }
        }

        public sealed class IsolatedStaticSaga : Wolverine.Saga
        {
            public static IsolatedStaticSaga Starts(IsolatedStartsMessage message) => new();
            public static System.Threading.Tasks.Task<IsolatedStaticSaga> StartsAsync(IsolatedStartsAsyncMessage message) => System.Threading.Tasks.Task.FromResult(new IsolatedStaticSaga());
            public static System.Threading.Tasks.Task NotFoundAsync(IsolatedNotFoundAsyncMessage message) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class ContextualStaticSaga : Wolverine.Saga
        {
            public static ContextualStaticSaga Start(ContextualStaticMessage message) => new();
            public static ContextualStaticSaga Starts(ContextualStaticMessage message) => new();
            public static System.Threading.Tasks.Task<ContextualStaticSaga> StartsAsync(ContextualStaticMessage message) => System.Threading.Tasks.Task.FromResult(new ContextualStaticSaga());
            public static System.Threading.Tasks.Task NotFoundAsync(ContextualStaticMessage message) => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class ReturnedCreationSaga : Wolverine.Saga
        {
            ReturnedCreationSaga() { }
            public static ReturnedCreationSaga Start(ReturnedCreationMessage message) => new();
        }

        public sealed class MissingFallbackConstructorSaga : Wolverine.Saga
        {
            MissingFallbackConstructorSaga() { }
            public static OrdinaryCascade Start(MissingFallbackConstructorMessage message) => new(message.SagaId);
        }

        public sealed class MissingInstanceConstructorSaga : Wolverine.Saga
        {
            MissingInstanceConstructorSaga() { }
            public void Start(MissingInstanceConstructorMessage message) { }
        }

        public sealed class ExistingPrivateConstructorSaga : Wolverine.Saga
        {
            ExistingPrivateConstructorSaga() { }
            public void Handle(ExistingPrivateConstructorMessage message) { }
        }

        public sealed class NotFoundOnlyPrivateConstructorSaga : Wolverine.Saga
        {
            NotFoundOnlyPrivateConstructorSaga() { }
            public static void NotFound(NotFoundOnlyPrivateConstructorMessage message) { }
        }

        public record DocumentOperationMessage(System.Guid Id);
        public static partial class NeutralDocumentOperations
        {
            public static void Apply(DocumentOperationMessage message, Marten.IDocumentSession session) => session.Store(new AuditDocument(message.Id));
            public static void Apply(DocumentOperationMessage message, string reason, Marten.IDocumentSession session) => session.Update(new AuditDocument(message.Id));
        }

        public record IgnoredSagaMessage(System.Guid SagaId);
        [Wolverine.Attributes.WolverineIgnore]
        public sealed class IgnoredSaga : Wolverine.Saga
        {
            public void Handle(IgnoredSagaMessage message) { }
        }

        public record LegacyIgnoredSagaMessage(System.Guid SagaId);
        [Wolverine.WolverineIgnore]
        public sealed class LegacyIgnoredSaga : Wolverine.Saga
        {
            public void Handle(LegacyIgnoredSagaMessage message) { }
        }

        public record GenericSagaMessage(System.Guid SagaId);
        public class GenericSaga<T> : Wolverine.Saga
        {
            public void Handle(GenericSagaMessage message) { }
        }

        public record AbstractSagaMessage(System.Guid SagaId);
        public abstract class AbstractSaga : Wolverine.Saga
        {
            public void Handle(AbstractSagaMessage message) { }
        }

        public record InternalSagaMessage(System.Guid SagaId);
        internal sealed class InternalSaga : Wolverine.Saga
        {
            public void Handle(InternalSagaMessage message) { }
        }

        public record NamedOnlyMessage(System.Guid SagaId);
        public sealed class NamedOnlySaga
        {
            public void Handle(NamedOnlyMessage message) { }
        }

        public partial record GeneratedCorrelationMessage;
        public record BeginGeneratedCorrelationSaga(System.Guid Id);
        public sealed partial class GeneratedCorrelationSaga : Wolverine.Saga
        {
            public static GeneratedCorrelationSaga Start(BeginGeneratedCorrelationSaga message) => new();
            public void Handle(GeneratedCorrelationMessage message) { }
        }

        public record GeneratedBaseMessage(System.Guid SagaId);
        public partial class GeneratedBaseSaga
        {
            public void Handle(GeneratedBaseMessage message) { }
        }
        """;

    const string GeneratedApplicationSource =
        """
        // <auto-generated/>
        namespace Orders;

        public partial class RoleSaga
        {
            public string GeneratedState { get; set; } = string.Empty;
            public void Handle(GeneratedRoleMessage message) { }
        }

        public partial record GeneratedCorrelationMessage
        {
            public System.Guid GeneratedCorrelationSagaId { get; init; }
        }

        public partial record OrdinaryCascade
        {
            public string GeneratedReturnedCascadeProperty { get; init; } = string.Empty;
        }

        public partial record DirectSend
        {
            public string GeneratedDirectBusProperty { get; init; } = string.Empty;
        }

        public partial record OutgoingImmediate
        {
            public string GeneratedOutgoingMessageProperty { get; init; } = string.Empty;
        }

        [Wolverine.Attributes.WolverineIgnore]
        public sealed partial class GeneratedCorrelationSaga;

        public partial class GeneratedBaseSaga : Wolverine.Saga;

        public static partial class NeutralDocumentOperations
        {
            public static void Apply(DocumentOperationMessage message, int generated, Marten.IDocumentSession session) => session.Delete<AuditDocument>(message.Id);
        }

        public record GeneratedOnlyMessage(System.Guid SagaId);
        public sealed class GeneratedOnlySaga : Wolverine.Saga
        {
            public void Handle(GeneratedOnlyMessage message) { }
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
        var frameworkTree = CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs");
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Orders/Sagas.cs");
        var generatedTree = CSharpSyntaxTree.ParseText(GeneratedApplicationSource, path: "/workspace/Orders/Generated.g.cs");
        var compilation = CSharpCompilation.Create(
            "Orders",
            [frameworkTree, applicationTree, generatedTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "Orders",
            ProjectPath = "/workspace/Orders/Orders.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
    }
}
