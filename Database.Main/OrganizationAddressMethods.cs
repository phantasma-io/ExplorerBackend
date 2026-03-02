using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace Database.Main;

public static class OrganizationAddressMethods
{
    public static void RemoveFromOrganizationAddressesIfNeeded(MainDbContext databaseContext, Organization organization, List<string> addressesOrCurrentMembers)
    {
        if (organization == null || !addressesOrCurrentMembers.Any()) return;

        var addressesToRemove = databaseContext.OrganizationAddresses
            .Where(x => x.OrganizationId == organization.ID &&
                        !addressesOrCurrentMembers.Contains(x.Address.ADDRESS)).ToList();

        if (!addressesToRemove.Any()) return;

        Log.Information("Removing {0} addresses from {1}", addressesToRemove.Count, organization.NAME);

        databaseContext.OrganizationAddresses.RemoveRange(addressesToRemove);
    }

    public static void InsertIfNotExists(MainDbContext databaseContext, Organization organization,
        List<string> addresses, Chain chain)
    {
        if (organization == null || !addresses.Any()) return;

        var addressMap = AddressMethods.InsertIfNotExists(databaseContext, chain, addresses);

        var organizationAddressesToInsert = (from address in addresses
                                             let organizationAddress =
                                                 databaseContext.OrganizationAddresses.FirstOrDefault(x =>
                                                     x.Address.ADDRESS == address && x.Organization == organization)
                                             where organizationAddress == null
                                             select new OrganizationAddress
                                             { Address = addressMap.GetValueOrDefault(address), Organization = organization }).ToList();

        databaseContext.OrganizationAddresses.AddRange(organizationAddressesToInsert);
    }

    public static void SetMembership(MainDbContext databaseContext, Organization organization, Address address,
        bool shouldBeMember)
    {
        if (organization == null || address == null) return;

        var existing = databaseContext.OrganizationAddresses
                           .FirstOrDefault(x => x.OrganizationId == organization.ID && x.AddressId == address.ID) ??
                       DbHelper.GetTracked<OrganizationAddress>(databaseContext)
                           .FirstOrDefault(x => x.OrganizationId == organization.ID && x.AddressId == address.ID);

        if (shouldBeMember)
        {
            if (existing == null)
                databaseContext.OrganizationAddresses.Add(new OrganizationAddress
                { Organization = organization, Address = address });
        }
        else
        {
            if (existing != null)
                databaseContext.OrganizationAddresses.Remove(existing);
        }
    }

    public static void ReconcileMemberships(MainDbContext databaseContext, int organizationId,
        IReadOnlyCollection<int> scopedAddressIds, IReadOnlyCollection<int> targetAddressIds)
    {
        if (organizationId <= 0 || scopedAddressIds == null || scopedAddressIds.Count == 0)
            return;

        var scopedIds = scopedAddressIds.Distinct().ToList();
        var targetIds = targetAddressIds?.Distinct().ToHashSet() ?? new HashSet<int>();

        // Reconcile membership in a bounded scope to avoid per-address probes during sync batches.
        var existingMemberships = databaseContext.OrganizationAddresses
            .Where(x => x.OrganizationId == organizationId && scopedIds.Contains(x.AddressId))
            .ToList();

        var existingIds = existingMemberships.Select(x => x.AddressId).ToHashSet();

        foreach (var addressId in targetIds)
        {
            if (!existingIds.Contains(addressId))
            {
                databaseContext.OrganizationAddresses.Add(new OrganizationAddress
                {
                    OrganizationId = organizationId,
                    AddressId = addressId
                });
            }
        }

        var membershipsToRemove = existingMemberships.Where(x => !targetIds.Contains(x.AddressId)).ToList();
        if (membershipsToRemove.Count > 0)
            databaseContext.OrganizationAddresses.RemoveRange(membershipsToRemove);
    }

    public static IEnumerable<Organization> GetOrganizationsByAddress(MainDbContext databaseContext, string address)
    {
        return string.IsNullOrEmpty(address)
            ? null
            : databaseContext.OrganizationAddresses.Where(x => x.Address.ADDRESS == address).Select(x => x.Organization);
    }


    public static IEnumerable<OrganizationAddress> GetOrganizationAddressByOrganization(MainDbContext databaseContext,
        string organization)
    {
        return string.IsNullOrEmpty(organization)
            ? null
            : databaseContext.OrganizationAddresses.Where(x => x.Organization.NAME == organization);
    }
}
