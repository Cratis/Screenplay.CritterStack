// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayGenerator;

public class when_an_authored_repository_body_is_unavailable : given.an_authored_abstraction_application
{
    protected override string ImplementationSource =>
        """
        namespace Persistence;
        public sealed class Repository : Contracts.IRepository<Contracts.Submit>
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
            public extern void Append(Contracts.Submit command);
            public void Append(string value) { }
        }
        """;

    void Because() => Generate();

    [Fact] void should_not_invent_persistence_for_an_external_body() => ShouldRejectWithoutPersistence();
}
