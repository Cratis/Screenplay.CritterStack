// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

public class when_comparing_text_with_an_extra_line : Specification
{
    string? _difference;

    void Because() => _difference = CanonicalGolden.FirstDifference("domain Kitchen", "domain Kitchen\nmodule Sales");

    [Fact] void should_mark_the_expected_end_of_file() =>
        _difference.ShouldEqual("line 2: expected '<end of file>' but generated 'module Sales'");
}
