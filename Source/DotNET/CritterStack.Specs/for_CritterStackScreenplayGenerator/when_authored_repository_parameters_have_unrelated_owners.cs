// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_authored_repository_parameters_have_unrelated_owners : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository(System.Guid storedId, Marten.IDocumentSession session) : Contracts.IRepository<Contracts.Submit>
        {
            public void Append(Contracts.Submit command) => Store(command.Id);
            public void Append(string value) { }
            void Store(System.Guid forwardedId) => session.Events.Append(storedId, new Contracts.Made(forwardedId));
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_alias_constructor_and_method_parameters_by_ordinal() => Productions.Single().Key.Discriminator.ShouldEqual("imperative");
    [Fact] void should_report_the_unproven_stream_identity() => Result.Diagnostics.Any(diagnostic => diagnostic.Code == WolverineDiagnosticCodes.ProductionLinkOmitted).ShouldBeTrue();
}
