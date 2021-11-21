using System;
using System.Collections.Generic;
using System.Linq;

namespace Database.Main
{
    public class AddressParsed
    {
        public readonly string address;
        public readonly string chain;
        public readonly bool caseSensitive;
        public AddressParsed(string address, string chainShortName = null)
        {
            this.address = ParseExtendedFormat(address, out this.caseSensitive, out this.chain, chainShortName);
        }
        private static string ParseExtendedFormat(string address, out bool caseSensitivity, out string resultingChainShortName, string chainShortName = null)
        {
            if (address.Contains(':'))
            {
                var parsed = address.Split(':');
                if (parsed.Length != 2)
                    throw new Exception($"Incorrect address format: '{address}'.");
                chainShortName = parsed[0];
                address = parsed[1];
            }

            caseSensitivity = true;

            resultingChainShortName = chainShortName;

            ContractMethods.Drop0x(ref address);

            return address;
        }
    }
    public static class AddressMethods
    {
        public static string Prepend0x(string address, string chainShortName = null, bool lowercaseWhenApplicable = true)
        {
            if (string.IsNullOrEmpty(address))
            {
                return address;
            }

            // return "0x" + (lowercaseWhenApplicable ? address.ToLower() : address);

            return address;
        }

        public static AddressParsed[] ParseExtendedFormat(string addresses, string chainShortName = null)
        {
            var values = addresses.Contains(",") ? addresses.Split(',') : new string[] { addresses };
            var result = new List<AddressParsed>();
            foreach(var value in values)
            {
                result.Add(new AddressParsed(value, chainShortName));
            }

            return result.ToArray();
        }

        // Checks if "Addresses" table has entry with given name,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static Address Upsert(MainDatabaseContext databaseContext, int chainId, string address, bool saveChanges = true)
        {
            ContractMethods.Drop0x(ref address);

            var chain = ChainMethods.Get(databaseContext, chainId);
            bool addressCaseInsensitive = false;

            var entry = databaseContext.Addresses.Where(x =>
                x.ChainId == chainId &&
                (addressCaseInsensitive && x.ADDRESS.ToUpper() == address.ToUpper() || x.ADDRESS == address))
                .FirstOrDefault();

            if (entry != null)
            {
                return entry;
            }

            // Checking if entry has been added already
            // but not yet inserted into database.
            entry = DbHelper.GetTracked<Address>(databaseContext).Where(x =>
                x.ChainId == chainId &&
                (addressCaseInsensitive && x.ADDRESS.ToUpper() == address.ToUpper() || x.ADDRESS == address))
                .FirstOrDefault();

            if (entry != null)
            {
                return entry;
            }

            entry = new Address { ChainId = chainId, ADDRESS = address };
            databaseContext.Addresses.Add(entry);

            if (saveChanges)
            {
                try
                {
                    databaseContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    var exMessage = ex.ToString();
                    if (exMessage.Contains("duplicate key value violates unique constraint") &&
                        exMessage.Contains("IX_Addresses_ChainId_ADDRESS"))
                    {
                        // We tried to create same record in two threads concurrently.
                        // Now we should just remove duplicating record and get an existing record.
                        databaseContext.Addresses.Remove(entry);
                        entry = databaseContext.Addresses.Where(x => x.ChainId == chainId && x.ADDRESS == address).First();
                    }
                    else
                    {
                        // Unknown exception.
                        throw;
                    }
                }
            }

            return entry;
        }
        public static Address Get(MainDatabaseContext databaseContext, int chainId, string address)
        {
            ContractMethods.Drop0x(ref address);

            var entry = databaseContext.Addresses.Where(x =>
                x.ChainId == chainId &&
                x.ADDRESS == address)
                .FirstOrDefault();

            if (entry != null)
                return entry;

            // Checking if entry has been added already
            // but not yet inserted into database.
            entry = DbHelper.GetTracked<Address>(databaseContext).Where(x =>
                x.ChainId == chainId &&
                x.ADDRESS == address)
                .FirstOrDefault();

            return entry;
        }
        public static int GetId(MainDatabaseContext databaseContext, int chainId, string address)
        {
            ContractMethods.Drop0x(ref address);

            int id = 0;
            var entry = databaseContext.Addresses.Where(x =>
                x.ChainId == chainId &&
                x.ADDRESS == address)
                .FirstOrDefault();

            if (entry != null)
            {
                id = entry.ID;
            }
            
            return id;
        }

        public static int[] GetIdsFromExtendedFormat(MainDatabaseContext databaseContext, string addresses, bool returnNonexistentAddressIfNoneFound = true, string defaultChain = null)
        {
            var values = AddressMethods.ParseExtendedFormat(addresses, defaultChain);

            // Getting owners' ids.
            var ids = new List<int>();
            for (var i = 0; i < values.Length; i++)
            {
                // Address query is case insensitive only for BSC
                // TODO: check through special chain settings table

                if (string.IsNullOrEmpty(values[i].chain))
                {
                    ids.AddRange(databaseContext.Addresses.Where(x => (values[i].caseSensitive ? x.ADDRESS == values[i].address : x.ADDRESS.ToUpper() == values[i].address.ToUpper()) || x.ADDRESS_NAME.ToUpper() == values[i].address.ToUpper() || x.USER_NAME.ToUpper() == values[i].address.ToUpper()).Select(x => x.ID).ToArray());
                }
                else
                {
                    ids.AddRange(databaseContext.Addresses.Where(x => ((values[i].caseSensitive ? x.ADDRESS == values[i].address : x.ADDRESS.ToUpper() == values[i].address.ToUpper()) || x.ADDRESS_NAME.ToUpper() == values[i].address.ToUpper() || x.USER_NAME.ToUpper() == values[i].address.ToUpper()) && x.Chain.NAME.ToUpper() == values[i].chain.ToUpper()).Select(x => x.ID).ToArray());
                }
            }

            if (returnNonexistentAddressIfNoneFound && ids.Count() == 0)
                ids.Add(0);

            return ids.ToArray();
        }
    }
}
