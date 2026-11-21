// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Cratis.CritterStack.Screenplay.Canonical;

namespace Cratis.CritterStack.Screenplay.for_CanonicalGolden;

public class when_normalizing_line_endings_and_trailing_whitespace : Specification
{
    string _normalized = null!;

    void Because() => _normalized = CanonicalGolden.Normalize("domain Kitchen \t\r\n\r\nmodule Sales\t\r\nlast line \r\r\n\t");

    [Fact] void should_use_lf_line_endings_and_one_final_newline() => _normalized.ShouldEqual("domain Kitchen\n\nmodule Sales\nlast line\n");
}
