// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_marten_async_projection_application : Specification
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

        namespace JasperFx.Events.Projections
        {
            public enum ProjectionLifecycle { Inline, Async, Live }
            public enum SnapshotLifecycle { Inline, Async }
            public interface IProjection;
            public class ProjectionGraph
            {
                public void Add(IProjection projection, ProjectionLifecycle lifecycle) { }
                public void Add<T>(ProjectionLifecycle lifecycle) where T : IProjection { }
            }
        }

        namespace Marten.Events.Projections
        {
            public class ProjectionOptions : JasperFx.Events.Projections.ProjectionGraph
            {
                public void Snapshot<T>(JasperFx.Events.Projections.SnapshotLifecycle lifecycle) { }
                public void LiveStreamAggregation<T>() { }
            }
            public abstract class MultiStreamProjection<T, TId> : JasperFx.Events.Projections.IProjection;
            public abstract class EventProjection : JasperFx.Events.Projections.IProjection;
        }

        namespace Marten.Events.Aggregation
        {
            public abstract class SingleStreamProjection<T, TId> : JasperFx.Events.Projections.IProjection;
        }
        """;

    const string ApplicationSource =
        """
        namespace Trips;

        public record TripStarted(int Day);
        public record TripEnded(int Day);
        public record Movement(decimal Distance);
        public record Travel(int Day, Movement[] Movements);
        public record JournalOpened(string Name);
        public record LiveJournalOpened(string Name);

        public class Journal
        {
            public System.Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public void Apply(JournalOpened e) { }
        }

        public class LiveJournal
        {
            public System.Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public void Apply(LiveJournalOpened e) { }
        }

        public class Trip
        {
            public System.Guid Id { get; set; }
            public int StartedOn { get; set; }
        }

        public class Day
        {
            public int Id { get; set; }
            public int Started { get; set; }
        }

        public partial class TripProjection : Marten.Events.Aggregation.SingleStreamProjection<Trip, System.Guid>
        {
            public Trip Create(TripStarted e) => new();
            public void Apply(TripEnded e, Trip trip) { }
        }

        public partial class DayProjection : Marten.Events.Projections.MultiStreamProjection<Day, int>
        {
            public void Apply(Day day, TripStarted e) { }
            public void Apply(Day day, Movement e) { }
        }

        public partial class DistanceProjection : Marten.Events.Projections.EventProjection;

        public static class Configuration
        {
            public static void Configure(Marten.StoreOptions options)
            {
                options.Projections.Snapshot<Journal>(JasperFx.Events.Projections.SnapshotLifecycle.Async);
                options.Projections.LiveStreamAggregation<LiveJournal>();
                options.Projections.Add<TripProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Async);
                options.Projections.Add(new DayProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Async);
                options.Projections.Add(new DistanceProjection(), JasperFx.Events.Projections.ProjectionLifecycle.Async);
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

    void Establish()
    {
        var compilation = CSharpCompilation.Create(
            "Trips",
            [
                CSharpSyntaxTree.ParseText(FrameworkSource, path: "/workspace/Framework.cs"),
                CSharpSyntaxTree.ParseText(ApplicationSource, path: "/workspace/Trips/Projections.cs")
            ],
            _references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var project = new DotNetProjectCompilation
        {
            Name = "Trips",
            ProjectPath = "/workspace/Trips/Trips.csproj",
            SourceRoot = "/workspace",
            Compilation = compilation
        };
        Contribution = new CritterStackScreenplayAdapter().Analyze(
            new DotNetAnalysisContext([project]),
            new DotNetAdapterOptions());
    }
}
