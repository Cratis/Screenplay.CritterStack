# Screenplay.CritterStack — project context

Generates compiler-checked, reviewable Cratis Screenplay candidates from
authorized Marten, Wolverine, and independently composed .NET source
semantics (`Cratis.CritterStack.Screenplay`). Follows the same package
architecture as `Cratis.Arc.Screenplay`: a host supplies Roslyn compilations,
the package analyzes framework conventions, builds one semantic application
model, lowers it through the shared Screenplay generation SDK, prints
canonical `.play` source, and verifies it with the Screenplay compiler.
C#/.NET.

## Project concerns

Read every concern below before working in this repository. Together they are the project-owned instructions and override conflicting shared guidance.

- [Commands](project/commands.md)
- [AI-assisted development](project/ai-assisted-development.md)
