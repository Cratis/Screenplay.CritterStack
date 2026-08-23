// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_legacy_wolverine_dcb_application : Specification
{
    const string FrameworkSource =
        """
        namespace Wolverine.Configuration
        {
            public interface IWolverineReturnType;
        }

        namespace Wolverine
        {
            public class WolverineOptions;
            public abstract class Saga;
            public interface IResponseAware : Wolverine.Configuration.IWolverineReturnType;
            public interface ISideEffect : Wolverine.Configuration.IWolverineReturnType;
        }

        namespace Wolverine.Marten
        {
            public class BoundaryModelAttribute : System.Attribute;

            public class Events : System.Collections.Generic.List<object>, Wolverine.Configuration.IWolverineReturnType
            {
                public Events() { }
                public Events(System.Collections.Generic.IEnumerable<object> events) : base(events) { }
            }
        }

        namespace JasperFx.Events.Tags
        {
            public sealed class EventTagQuery
            {
                public static EventTagQuery For<TTag>(TTag value) => new();
                public EventTagQuery Or<TTag>(TTag value) => this;
                public EventTagQuery Or<TEvent, TTag>(TTag value) => this;
                public EventTagQuery AndEventsOfType<T1>() => this;
                public EventTagQuery AndEventsOfType<T1, T2>() => this;
            }

            public interface IEventBoundary<out T> where T : class
            {
                T? Aggregate { get; }
                void AppendOne(object @event);
                void AppendMany(params object[] events);
                void AppendMany(System.Collections.Generic.IEnumerable<object> events);
            }
        }
        """;

    const string ApplicationSource =
        """
        namespace LegacyAccounts;

        public record AccountId(System.Guid Value);
        public record LegacyChange(AccountId AccountId);
        public record LegacyBoundaryChange(AccountId AccountId);

        public sealed class AccountState
        {
            public decimal Balance { get; set; }
        }

        public record LegacyOpened(AccountId AccountId);
        public record LegacyChanged(AccountId AccountId);
        public record LegacyAppended(AccountId AccountId);
        public record LegacyWrapped(AccountId AccountId);
        public record LegacyBoundaryReturn(AccountId AccountId);

        public static class LegacyChangeHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Load(LegacyChange command) =>
                JasperFx.Events.Tags.EventTagQuery.For<AccountId>(command.AccountId)
                    .AndEventsOfType<LegacyOpened>();

            public static LegacyChanged Handle(
                LegacyChange command,
                [Wolverine.Marten.BoundaryModel] AccountState state) =>
                new(command.AccountId);
        }

        public static class LegacyBoundaryChangeHandler
        {
            public static JasperFx.Events.Tags.EventTagQuery Before(LegacyBoundaryChange command) =>
                new JasperFx.Events.Tags.EventTagQuery().Or<AccountId>(command.AccountId);

            public static (Wolverine.Marten.Events, LegacyBoundaryReturn) Handle(
                LegacyBoundaryChange command,
                [Wolverine.Marten.BoundaryModel] JasperFx.Events.Tags.IEventBoundary<AccountState> boundary)
            {
                boundary.AppendOne(new LegacyAppended(command.AccountId));
                return (new Wolverine.Marten.Events
                {
                    new LegacyWrapped(command.AccountId)
                }, new(command.AccountId));
            }
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
        var applicationTree = CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/LegacyAccounts/Dcb.cs");
        var compilation = CSharpCompilation.Create(
            "LegacyAccounts",
            [frameworkTree, applicationTree],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Project = new DotNetProjectCompilation
        {
            Name = "LegacyAccounts",
            ProjectPath = "/workspace/LegacyAccounts/LegacyAccounts.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree> { frameworkTree, applicationTree }
        };
    }
}
