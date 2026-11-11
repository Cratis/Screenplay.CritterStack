// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Marten document usage in alternative forms: every read, store, update and delete method name, extension and
/// integration APIs, identity configuration that is valid first and unresolved later (and the reverse), unwrapped
/// lambdas, attributed identity members, usage inside event projections and unresolved custom processors,
/// compiled query executions, field initializers and repeated usage across files.
/// </summary>
public class a_marten_document_variants_application : Specification
{
    const string FrameworkSource =
        """
        namespace JasperFx
        {
            public class IdentityAttribute : System.Attribute;
        }

        namespace JasperFx.Events
        {
            public interface IEvent
            {
                object Data { get; }
            }
        }

        namespace Marten.Linq
        {
            public interface ICompiledQuery<TDoc, TOut>
            {
                System.Linq.Expressions.Expression<System.Func<System.Linq.IQueryable<TDoc>, TOut>> QueryIs();
            }
        }

        namespace Marten
        {
            public interface IDocumentStore;
            public interface IQuerySession
            {
                T? Load<T>(System.Guid id) where T : class;
                System.Threading.Tasks.Task<T?> LoadAsync<T>(System.Guid id) where T : class;
                System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<T>> LoadManyAsync<T>(params System.Guid[] ids) where T : class;
                System.Linq.IQueryable<T> Query<T>();
                System.Threading.Tasks.Task<TOut> QueryAsync<TDoc, TOut>(Marten.Linq.ICompiledQuery<TDoc, TOut> query);
            }
            public interface IDocumentOperations : IQuerySession
            {
                void Store<T>(params T[] documents);
                void Insert<T>(params T[] documents);
                void Update<T>(params T[] documents);
                void Delete<T>(System.Guid id);
                void Delete(object entity);
                void DeleteWhere<T>(System.Linq.Expressions.Expression<System.Func<T, bool>> predicate);
                void SaveChanges();
            }
            public interface IDocumentSession : IDocumentOperations;
            public class StoreOptions
            {
                public MartenRegistry Schema { get; } = new();
                public void RegisterDocumentType<T>() { }
            }
            public class MartenRegistry
            {
                public DocumentMappingExpression<T> For<T>() => new();

                public class DocumentMappingExpression<T>
                {
                    public DocumentMappingExpression<T> Identity(System.Linq.Expressions.Expression<System.Func<T, object>> member) => this;
                }
            }
        }

        namespace Marten.Extensions
        {
            public static class DocumentExtensions
            {
                public static void Update<T>(this Marten.IQuerySession session, T document) { }
            }
        }

        namespace Marten.Events.Projections
        {
            public interface IProjection;
            public abstract class EventProjection : IProjection
            {
                public virtual System.Threading.Tasks.ValueTask ApplyAsync(
                    Marten.IDocumentOperations operations,
                    JasperFx.Events.IEvent e,
                    System.Threading.CancellationToken cancellation) => default;
            }
        }

        namespace Wolverine.Marten
        {
            public static class MartenOps
            {
                public static void Store<T>(T document) { }
                public static void Insert<T>(T document) { }
            }
        }

        namespace Lookalike
        {
            public class Session
            {
                public void Store<T>(T document) { }
                public void Update<T>(T document) { }
                public void For<T>() { }
            }
        }
        """;

    const string DocumentSource =
        """
        namespace Documents;

        public class Alpha
        {
            public System.Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class Beta
        {
            public System.Guid Id { get; set; }
            [JasperFx.Identity]
            public string Key { get; set; } = string.Empty;
        }

        public class Gamma
        {
            public System.Guid Id { get; set; }
            [JasperFx.Identity]
            public System.Guid IdentityField;
        }

        public class Delta
        {
            public int Code { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class Epsilon
        {
            public int Code { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class Zeta
        {
            public int Code { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class Eta
        {
            public int Code { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class Theta
        {
            public string Name { get; set; } = string.Empty;
        }

        public class Iota
        {
            public int Code { internal get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class Kappa
        {
            public System.Guid Id { get; set; }
        }

        public class Lambda
        {
            public System.Guid Id { get; set; }
        }

        public class Mu
        {
            public System.Guid Id { get; set; }
        }

        public class Nu
        {
            public System.Guid Id { get; set; }
        }

        public class Xi
        {
            public System.Guid Id { get; set; }
        }

        public class Omicron
        {
            public System.Guid Id { get; set; }
        }

        public class Pi
        {
            public System.Guid Id { get; set; }
        }

        public class Rho
        {
            public System.Guid Id { get; set; }
        }

        public class Sigma
        {
            public System.Guid Id { get; set; }
        }

        public class Tau
        {
            public System.Guid Id { get; set; }
        }

        public class Upsilon
        {
            public System.Guid Id { get; set; }
        }

        public class Phi
        {
            public System.Guid Id { get; set; }
        }

        public class Chi
        {
            public System.Guid Id { get; set; }
        }

        public class Psi
        {
            public System.Guid Id { get; set; }
        }

        public class Omega
        {
            public System.Guid Id { get; set; }
        }

        public class ByCode : Marten.Linq.ICompiledQuery<Tau, Tau>
        {
            public System.Linq.Expressions.Expression<System.Func<System.Linq.IQueryable<Tau>, Tau>> QueryIs() =>
                query => System.Linq.Queryable.First(query);
        }

        public class ByExternal : Marten.Linq.ICompiledQuery<string, string>
        {
            public System.Linq.Expressions.Expression<System.Func<System.Linq.IQueryable<string>, string>> QueryIs() =>
                query => System.Linq.Queryable.First(query);
        }
        """;

    const string UsageSource =
        """
        using Marten.Extensions;

        namespace Documents;

        public static class FirstUsage
        {
            public static readonly System.Action<Marten.IDocumentSession, Alpha> Storer = (session, alpha) => session.Store(alpha);

            public static void Store(Alpha alpha, Marten.IDocumentSession session) => session.Store(alpha);
            public static void Insert(Beta beta, Marten.IDocumentSession session) => session.Insert(beta);
            public static void Update(Beta beta, Marten.IDocumentSession session) => session.Update(beta);
            public static Gamma? Load(Marten.IQuerySession session) => session.Load<Gamma>(System.Guid.Empty);
            public static System.Threading.Tasks.Task<Delta?> LoadAsync(Marten.IQuerySession session) => session.LoadAsync<Delta>(System.Guid.Empty);
            public static System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Epsilon>> LoadMany(Marten.IQuerySession session) => session.LoadManyAsync<Epsilon>();
            public static System.Linq.IQueryable<Zeta> Query(Marten.IQuerySession session) => session.Query<Zeta>();
            public static void Delete(Eta eta, Marten.IDocumentSession session) => session.Delete(eta);
            public static void DeleteById(Marten.IDocumentSession session) => session.Delete<Theta>(System.Guid.Empty);
            public static void DeleteWhere(Marten.IDocumentSession session) => session.DeleteWhere<Iota>(_ => true);
            public static void Extension(Kappa kappa, Marten.IQuerySession session) => Marten.Extensions.DocumentExtensions.Update(session, kappa);
            public static void ReducedExtension(Lambda lambda, Marten.IQuerySession session) => session.Update(lambda);
            public static void Integration(Mu mu) => Wolverine.Marten.MartenOps.Store(mu);
            public static void IntegrationInsert(Nu nu) => Wolverine.Marten.MartenOps.Insert(nu);
            public static void Save(Marten.IDocumentSession session) => session.SaveChanges();
            public static void ExternalType(Marten.IDocumentSession session) => session.Store("text");
            public static void LookalikeCalls(Lookalike.Session session, Xi xi)
            {
                session.Store(xi);
                session.Update(xi);
                session.For<Xi>();
            }

            public static async System.Threading.Tasks.Task Compiled(Marten.IQuerySession session)
            {
                await session.QueryAsync(new ByCode());
                await session.QueryAsync(new ByExternal());
            }

            public static void Mixed(Marten.IDocumentSession session, Omicron omicron)
            {
                session.Store(omicron);
                session.Load<Omicron>(System.Guid.Empty);
                session.Update(omicron);
                session.Delete(omicron);
            }
        }

        public static class Holder
        {
            public static Marten.IQuerySession Session = null!;
            public static readonly Pi? Field = Session.Load<Pi>(System.Guid.Empty);
        }

        public class EventProjectionHost : Marten.Events.Projections.EventProjection
        {
            public override System.Threading.Tasks.ValueTask ApplyAsync(
                Marten.IDocumentOperations operations,
                JasperFx.Events.IEvent e,
                System.Threading.CancellationToken cancellation)
            {
                operations.Store(new Rho());
                operations.Load<Sigma>(System.Guid.Empty);
                operations.Update(new Sigma());
                return default;
            }
        }

        public class CustomProcessor : Marten.Events.Projections.IProjection
        {
            public void Process(Marten.IDocumentSession session)
            {
                session.Load<Upsilon>(System.Guid.Empty);
                session.Store(new Upsilon());
                session.Query<Phi>();
            }

            public void Configure(Marten.StoreOptions options)
            {
                options.RegisterDocumentType<Chi>();
            }
        }
        """;

    const string ConfigurationSource =
        """
        namespace Documents;

        public static class SecondUsage
        {
            public static void Update(Alpha alpha, Marten.IDocumentSession session) => session.Update(alpha);
            public static void Store(Beta beta, Marten.IDocumentSession session) => session.Store(beta);
        }

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Schema.For<Delta>().Identity(d => d.Code);
                options.Schema.For<Delta>().Identity(d => d.Name.ToUpperInvariant());
                options.Schema.For<Epsilon>().Identity(d => d.Name.Length);
                options.Schema.For<Epsilon>().Identity((Epsilon d) => (object)(d.Code));
                options.Schema.For<Zeta>().Identity(d => ((object)d.Code));
                options.Schema.For<Eta>().Identity(d => d.Name);
                options.Schema.For<Iota>().Identity(d => d.Code);
                options.Schema.For<Omega>();
                options.RegisterDocumentType<Psi>();
                options.RegisterDocumentType<string>();
            }
        }
        """;

    static readonly IReadOnlyList<MetadataReference> _references =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator)
            .Select(_ => MetadataReference.CreateFromFile(_))
    ];

    protected AdapterContribution Contribution = null!;
    protected AdapterContribution MartenOnlyContribution = null!;
    protected IReadOnlyList<string> Identifiers = [];

    void Establish()
    {
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
            CSharpSyntaxTree.ParseText(DocumentSource, path: "/workspace/Documents/Documents.cs"),
            CSharpSyntaxTree.ParseText(UsageSource, path: "/workspace/Documents/Usage.cs"),
            CSharpSyntaxTree.ParseText(ConfigurationSource, path: "/workspace/Documents/Configuration.cs")
        };
        var compilation = CSharpCompilation.Create(
            "Documents",
            trees,
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(_ => _.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var project = new DotNetProjectCompilation
        {
            Name = "Documents",
            ProjectPath = "/workspace/Documents/Documents.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation,
            AuthoredSyntaxTrees = new HashSet<SyntaxTree>(trees)
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
        MartenOnlyContribution = new MartenScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
        Identifiers =
        [
            .. Contribution.Facts.OfType<ArtifactFact>()
                .Where(_ => _.Definition.Key.Kind == ArtifactKind.Document)
                .Select(_ => $"{_.Definition.Name}:{string.Join(',', _.Definition.Properties.Where(p => p.IsIdentifier).Select(p => p.Name))}")
        ];
    }
}
