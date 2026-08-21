// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Cratis.CritterStack.Screenplay;

static class ScreenplayNames
{
    public static string Declaration(string value)
    {
        var result = new StringBuilder(value.Length);
        var capitalize = true;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                capitalize = true;
                continue;
            }

            result.Append(capitalize ? char.ToUpperInvariant(character) : character);
            capitalize = false;
        }

        if (result.Length == 0)
        {
            return "Application";
        }

        if (char.IsDigit(result[0]))
        {
            result.Insert(0, "Application");
        }

        return result.ToString();
    }
}
