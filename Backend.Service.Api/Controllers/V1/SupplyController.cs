using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class SupplyController : BaseControllerV1
{
    // To be used for https://supply.phantasma.io/circulating_supply
    // Output example: 119576148.69331215
    [HttpGet("circulatingSupply")]
    [ApiInfo(typeof(double), "Returns SOUL token circulating supply", cacheDuration: 60, cacheTag: "supply")]
    public Task<double> GetResults()
    {
        return Supply.Execute();
    }
}
