// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.CritterStack.Screenplay.for_CritterStackScreenplayAdapter;

public class when_generating_a_marten_application : given.a_critter_stack_application
{
    GeneratedScreenplayDefinition _result = null!;

    void Because() => _result = new CritterStackScreenplayGenerator().Generate(
        Context.Projects,
        new CritterStackScreenplayOptions { Domain = "Banking" });

    [Fact] void should_succeed() => _result.IsSuccess.ShouldBeTrue();
    [Fact] void should_generate_the_module() => _result.Source.ShouldContain("module BankAccountES");
    [Fact] void should_generate_the_account_read_model() => _result.Source.ShouldContain("readmodel Account");
    [Fact] void should_generate_the_transaction_read_model() => _result.Source.ShouldContain("readmodel AccountTransactions");
    [Fact] void should_generate_the_account_reducer() => _result.Source.ShouldContain("reducer AccountSnapshot => Account");
    [Fact] void should_generate_the_transaction_reducer() => _result.Source.ShouldContain("reducer AccountTransactionsProjection => AccountTransactions");
    [Fact] void should_generate_the_opened_event() => _result.Source.ShouldContain("event AccountOpened");
    [Fact] void should_generate_the_deposited_event() => _result.Source.ShouldContain("event FundsDeposited");
    [Fact] void should_generate_the_withdrawn_event() => _result.Source.ShouldContain("event FundsWithdrawn");
    [Fact] void should_keep_source_file_references() => _result.Source.ShouldContain("file BankAccountES/Account.cs");
}
