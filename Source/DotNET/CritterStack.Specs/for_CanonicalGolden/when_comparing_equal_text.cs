// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

public class when_comparing_equal_text : Specification
{
    string? _difference;

    void Because() => _difference = CanonicalGolden.FirstDifference("domain Kitchen\n", "domain Kitchen\n");

    [Fact] void should_not_report_a_difference() => _difference.ShouldBeNull();
}
