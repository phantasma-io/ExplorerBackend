using System.Threading.Tasks;
using Serilog;

namespace Database.Main;

public static class OrganizationEventMethods
{
    public static async Task<OrganizationEvent> UpsertAsync(MainDbContext databaseContext, string organization, string address,
        Event databaseEvent, Chain chain)
    {
        if ( string.IsNullOrEmpty(organization) || string.IsNullOrEmpty(address) ) return null;

        var addressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, address);

        var organizationEntry = OrganizationMethods.Get(databaseContext, organization);

        if ( organizationEntry == null )
        {
            Log.Warning("Organization {Organization} is null", organization);
            return null;
        }

        var organizationEvent = new OrganizationEvent
            {Address = addressEntry, Organization = organizationEntry, Event = databaseEvent};

        await databaseContext.OrganizationEvents.AddAsync(organizationEvent);

        return organizationEvent;
    }
}
