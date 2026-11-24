// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

public class a_generator_roster : Specification
{
    protected static GeneratedScreenplayDefinition Generate(params IDotNetScreenplayAdapter[] adapters) =>
        new CritterStackScreenplayGenerator(adapters).Generate(
            [],
            new CritterStackScreenplayOptions { Domain = "Ordering" });

    protected sealed class CountingAdapter(string id, string version = "1.0.0") : IDotNetScreenplayAdapter, IDescribedDotNetScreenplayAdapter
    {
        public AdapterIdentity Identity { get; } = new() { Id = id, Version = version };

        public AdapterDescriptor Descriptor => new()
        {
            Identity = Identity,
            SourceLanguage = AdapterSourceLanguage.CSharp,
            Category = AdapterCategory.ApplicationFramework
        };

        public int LegacyProbeCount { get; private set; }

        public int ProbeCount { get; private set; }

        public int AnalyzeCount { get; private set; }

        public bool CanAnalyze(DotNetAnalysisContext context)
        {
            LegacyProbeCount++;
            return true;
        }

        public AdapterProbeResult Probe(DotNetAnalysisContext context)
        {
            ProbeCount++;
            return new AdapterProbeApplicable();
        }

        public AdapterContribution Analyze(DotNetAnalysisContext context, DotNetAdapterOptions options)
        {
            AnalyzeCount++;
            return new() { Adapter = Identity };
        }
    }
}
