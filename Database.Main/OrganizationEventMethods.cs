namespace Database.Main;

public static class OrganizationEventMethods
{
    public static OrganizationEvent Upsert(MainDbContext databaseContext, string organization, string address,
        Event databaseEvent, Chain chain, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(organization) || string.IsNullOrEmpty(address) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chain, address, saveChanges);

        var organizationEntry = OrganizationMethods.Upsert(databaseContext, organization, saveChanges);

        var organizationEvent = new OrganizationEvent
            {Address = addressEntry, Organization = organizationEntry, Event = databaseEvent};

        databaseContext.OrganizationEvents.Add(organizationEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return organizationEvent;
    }
}
