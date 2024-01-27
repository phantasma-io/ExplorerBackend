using System.Text.Json;

namespace Database.Main;

public static class ContractMethodMethods
{
    public static ContractMethod Insert(MainDbContext mainDbContext, Contract contract, JsonElement data,
        long timestampUnixSeconds)
    {
        var contractMethod = new ContractMethod
            {Contract = contract, METHODS = data, TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds};

        mainDbContext.ContractMethods.Add(contractMethod);

        return contractMethod;
    }
}
