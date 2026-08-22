// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_critter_stack_application : Specification
{
    const string FrameworkSource =
        """
        namespace Marten
        {
            public interface IDocumentStore;
            public class StoreOptions
            {
                public Marten.Events.Projections.ProjectionOptions Projections { get; } = new();
            }
        }

        namespace Marten.Events.Projections
        {
            public enum SnapshotLifecycle { Inline, Async }
            public enum ProjectionLifecycle { Inline, Async, Live }
            public class ProjectionOptions
            {
                public void Snapshot<T>(SnapshotLifecycle lifecycle) { }
                public void Add<T>(ProjectionLifecycle lifecycle) { }
            }
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T, TId>;
        }
        """;

    const string ApplicationSource =
        """
        namespace BankAccountES;

        public record AccountOpened(System.Guid AccountId, string Currency);
        public record FundsDeposited(System.Guid AccountId, decimal Amount, decimal NewBalance);
        public record FundsWithdrawn(System.Guid AccountId, decimal Amount, decimal NewBalance);

        public class Account
        {
            public System.Guid Id { get; set; }
            public string Currency { get; set; } = "USD";
            public decimal Balance { get; set; }

            public void Apply(AccountOpened e) { }
            public void Apply(FundsDeposited e) { }
            public void Apply(FundsWithdrawn e) { }
        }

        public class AccountTransactions
        {
            public System.Guid Id { get; set; }
            public decimal Balance { get; set; }
        }

        public partial class AccountTransactionsProjection
            : Marten.Events.Aggregation.SingleStreamProjection<AccountTransactions, System.Guid>
        {
            public static AccountTransactions Create(AccountOpened e) => new();
            public void Apply(FundsDeposited e, AccountTransactions view) { }
            public void Apply(FundsWithdrawn e, AccountTransactions view) { }
        }

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Projections.Snapshot<Account>(Marten.Events.Projections.SnapshotLifecycle.Inline);
                options.Projections.Add<AccountTransactionsProjection>(Marten.Events.Projections.ProjectionLifecycle.Inline);
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected CritterStackScreenplayAdapter Adapter = null!;
    protected AdapterContribution Contribution = null!;
    protected DotNetAnalysisContext Context = null!;

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "BankAccountES",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/BankAccountES/Account.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Context = new([
            new DotNetProjectCompilation
            {
                Name = "BankAccountES",
                ProjectPath = "/workspace/BankAccountES/BankAccountES.csproj",
                SourceRoot = "/workspace",
                Compilation = compilation,
                AuthoredSyntaxTrees = compilation.SyntaxTrees.ToHashSet()
            }
        ]);
        Adapter = new();
        Contribution = Adapter.Analyze(Context, new DotNetAdapterOptions());
    }
}
