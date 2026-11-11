// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_characterizing_wolverine_discovery_variants : given.a_wolverine_policy_variants_application
{
    const string ExpectedDiagnostics =
        """
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.EarlierConfiguration|DiscoveryVariants/Earlier.cs:7:9-7:66|Wolverine handler discovery call 'CustomizeHandlerDiscovery' was not applied because custom handler predicates and lambdas are not statically interpreted
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.EarlierConfiguration|DiscoveryVariants/Earlier.cs:8:9-8:64|Wolverine handler discovery call 'DisableConventionalDiscovery' was not applied because the enabled state is not a compile-time constant
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:18:9-18:48|Wolverine handler discovery call 'IncludeType' was not applied because the included handler type is not authored source in this compilation
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:19:9-19:51|Wolverine handler discovery call 'IncludeType' was not applied because the included handler type is not authored source in this compilation
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:20:9-20:51|Wolverine handler discovery call 'IncludeType' was not applied because the included handler type is not authored source in this compilation
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:22:9-22:67|Wolverine handler discovery call 'IncludeAssembly' was not applied because assembly scanning outside the current source compilation is not supported
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:23:9-23:59|Wolverine handler discovery call 'IncludeAssembly' was not applied because assembly scanning outside the current source compilation is not supported
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:24:9-24:66|Wolverine handler discovery call 'CustomizeHandlerDiscovery' was not applied because custom handler predicates and lambdas are not statically interpreted
        WOLVERINE0007|Warning|Unknown|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.Configuration|DiscoveryVariants/Program.cs:25:9-25:73|Wolverine handler discovery call 'IgnoreAssembly' was not applied because assembly removal cannot be resolved from the current source compilation
        WOLVERINE0007|Warning|Conflict|dotnet://DiscoveryVariants/DiscoveryVariants/DiscoveryVariants.EarlierConfiguration|DiscoveryVariants/Earlier.cs:8:9-8:64|Wolverine handler discovery call 'DisableConventionalDiscovery' was not applied because conflicting enabled states depend on runtime execution order
        """;

    [Fact] void should_pin_the_discovery_diagnostics_with_ranges() => string.Join('\n', Diagnostics(DiscoveryContribution, "WOLVERINE0007")).ShouldEqual(ExpectedDiagnostics);
}
