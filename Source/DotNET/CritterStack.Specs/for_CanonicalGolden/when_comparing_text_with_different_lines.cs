// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

public class when_comparing_text_with_different_lines : Specification
{
    string? _difference;

    void Because() => _difference = CanonicalGolden.FirstDifference("domain Kitchen\nmodule Sales\nevent Old\n", "domain Kitchen\nmodule Orders\nevent New\n");

    [Fact] void should_report_only_the_first_difference_with_a_one_based_line_number() =>
        _difference.ShouldEqual("line 2: expected 'module Sales' but generated 'module Orders'");
}
