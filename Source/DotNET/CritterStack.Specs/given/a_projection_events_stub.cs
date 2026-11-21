// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.given;

/// <summary>
/// Shares the Marten subscription <c>ProcessEventsAsync</c> signature that the embedded application sources of the
/// Marten projection and subscription givens declare. The text it produces is character for character what the givens
/// previously spelled out, so source positions asserted by specs do not move.
/// </summary>
public abstract class a_projection_events_stub : Specification
{
    /// <summary>
    /// The interface member declaration at the indentation of a nested type member.
    /// </summary>
    protected static readonly string InterfaceProcessEvents = ProcessEventsSignature(string.Empty, 8);

    /// <summary>
    /// The abstract member declaration at the indentation of a nested type member.
    /// </summary>
    protected static readonly string AbstractProcessEvents = ProcessEventsSignature("public abstract ", 8);

    /// <summary>
    /// The override declaration at the indentation of a top-level type member.
    /// </summary>
    protected static readonly string OverrideProcessEvents = ProcessEventsSignature("public override ", 4);

    /// <summary>
    /// The public implementation declaration at the indentation of a top-level type member.
    /// </summary>
    protected static readonly string PublicProcessEvents = ProcessEventsSignature("public ", 4);

    /// <summary>
    /// Builds the signature without its leading indentation, so it can be placed after the indentation of its line.
    /// </summary>
    /// <param name="modifiers">The modifiers that precede the return type, including the trailing space.</param>
    /// <param name="indent">The number of spaces the declaration is indented by.</param>
    /// <returns>The signature text up to and including the closing parenthesis.</returns>
    static string ProcessEventsSignature(string modifiers, int indent)
    {
        var parameterIndent = new string(' ', indent + 4);
        return string.Join(
            "\n",
            $"{modifiers}System.Threading.Tasks.Task ProcessEventsAsync(",
            $"{parameterIndent}Marten.Events.Daemon.EventRange page,",
            $"{parameterIndent}Marten.Events.Daemon.ISubscriptionController controller,",
            $"{parameterIndent}Marten.IDocumentOperations operations,",
            $"{parameterIndent}System.Threading.CancellationToken cancellationToken)");
    }
}
