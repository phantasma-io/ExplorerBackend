using System;
using System.Collections.Generic;
using Database.Main;
using Shouldly;
using Xunit;

namespace Backend.Service.Api.Tests.Database;

public sealed class ContractMethodsTests
{
    [Fact]
    public void BatchUpsertRejectsInvalidContractHashesBeforeSqlParameterBinding()
    {
        // HASH is a database key. Continuing without it would desynchronize
        // contracts, tokens, transfers, and later event links.
        var ex = Should.Throw<ArgumentException>(() => ContractMethods.BatchUpsert(null!,
            [
                ("null-hash", 1, null!, "NULLHASH"),
                ("empty-hash", 1, "", "EMPTY"),
                ("blank-hash", 1, "   ", "BLANK")
            ]));

        ex.Message.ShouldContain("empty HASH");
        ex.Message.ShouldContain("null-hash");
    }
}
