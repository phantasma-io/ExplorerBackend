#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetOrganizations
{
    private sealed class OrganizationPageItem
    {
        public int Id { get; init; }
        public string OrganizationId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public required Organization ApiOrganization { get; init; }
    }

    [ProducesResponseType(typeof(OrganizationResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(OrganizationResult), "Returns the Organizations on the backend.", false, 10)]
    public static async Task<OrganizationResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "name",
        string order_direction = "asc",
        int limit = 50,
        string cursor = "",
        string organization_id = "",
        string organization_id_partial = "",
        string organization_name = "",
        string organization_name_partial = "",
        string q = "",
        int with_creation_event = 0,
        int with_address = 0
    // ReSharper enable InconsistentNaming
    )
    {
        Organization[] organizationArray;
        string? nextCursor = null;
        var qTrimmed = string.IsNullOrWhiteSpace(q) ? string.Empty : q.Trim();

        try
        {
            if (!string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if (!ArgValidation.CheckOrderDirection(order_direction))
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if (!ArgValidation.CheckLimit(limit, false))
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if (!string.IsNullOrEmpty(organization_id) && !ArgValidation.CheckString(organization_id))
                throw new ApiParameterException("Unsupported value for 'organization_id' parameter.");

            if (!string.IsNullOrEmpty(organization_id_partial) &&
                 !ArgValidation.CheckString(organization_id_partial))
                throw new ApiParameterException("Unsupported value for 'organization_name_partial' parameter.");

            if (!string.IsNullOrEmpty(organization_name) && !ArgValidation.CheckString(organization_name))
                throw new ApiParameterException("Unsupported value for 'organization_name' parameter.");

            if (!string.IsNullOrEmpty(organization_name_partial) &&
                 !ArgValidation.CheckString(organization_name_partial))
                throw new ApiParameterException("Unsupported value for 'organization_name_partial' parameter.");

            if (!string.IsNullOrEmpty(qTrimmed) && !ArgValidation.CheckGeneralSearch(qTrimmed))
                throw new ApiParameterException("Unsupported value for 'q' parameter.");

            var cursorToken = CursorPagination.ParseCursor(cursor);
            var sortDirection = CursorPagination.ParseSortDirection(order_direction);
            var orderBy = string.IsNullOrWhiteSpace(order_by) ? "name" : order_by;

            var orderDefinitions =
                new Dictionary<string, CursorOrderDefinition<OrganizationPageItem>>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "id",
                        new CursorOrderDefinition<OrganizationPageItem>(
                            "id",
                            new CursorOrderSegment<OrganizationPageItem, int>(
                                x => x.Id,
                                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)))
                    },
                    {
                        "name",
                        new CursorOrderDefinition<OrganizationPageItem>(
                            "name",
                            new CursorOrderSegment<OrganizationPageItem, string>(
                                x => x.Name,
                                value => value))
                    },
                    {
                        "organization_id",
                        new CursorOrderDefinition<OrganizationPageItem>(
                            "organization_id",
                            new CursorOrderSegment<OrganizationPageItem, string>(
                                x => x.OrganizationId,
                                value => value))
                    }
                };

            if (!orderDefinitions.TryGetValue(orderBy, out var orderDefinition))
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.Organizations.AsQueryable().AsNoTracking();

            var qUpper = string.IsNullOrEmpty(qTrimmed) ? string.Empty : qTrimmed.ToUpperInvariant();

            if (!string.IsNullOrEmpty(qUpper))
                query = query.Where(x =>
                    EF.Functions.ILike(x.ORGANIZATION_ID, $"%{qTrimmed}%") ||
                    EF.Functions.ILike(x.NAME, $"%{qTrimmed}%") ||
                    (x.ADDRESS != null && EF.Functions.ILike(x.ADDRESS, $"%{qTrimmed}%")) ||
                    (x.ADDRESS_NAME != null && EF.Functions.ILike(x.ADDRESS_NAME, $"%{qTrimmed}%")));

            if (!string.IsNullOrEmpty(organization_id))
                query = query.Where(x => x.ORGANIZATION_ID == organization_id);

            if (!string.IsNullOrEmpty(organization_id_partial))
                query = query.Where(x => x.ORGANIZATION_ID.Contains(organization_id_partial));

            if (!string.IsNullOrEmpty(organization_name)) query = query.Where(x => x.NAME == organization_name);

            if (!string.IsNullOrEmpty(organization_name_partial))
                query = query.Where(x => x.NAME.Contains(organization_name_partial));

            var pageQuery = query.Select(x => new OrganizationPageItem
            {
                Id = x.ID,
                OrganizationId = x.ORGANIZATION_ID ?? string.Empty,
                Name = x.NAME ?? string.Empty,
                ApiOrganization = new Organization
                {
                    id = x.ORGANIZATION_ID,
                    name = x.NAME,
                    size = x.OrganizationAddresses.Count,
                    create_event = with_creation_event == 1 && x.CreateEvent != null
                        ? new Event
                        {
                            event_id = x.ID,
                            chain = x.CreateEvent.Chain.NAME.ToLower(),
                            date = x.CreateEvent.TIMESTAMP_UNIX_SECONDS.ToString(),
                            block_hash = x.CreateEvent.Transaction.Block.HASH,
                            transaction_hash = x.CreateEvent.Transaction.HASH,
                            token_id = x.CreateEvent.TOKEN_ID,
                            event_kind = x.CreateEvent.EventKind.NAME,
                            address = x.CreateEvent.Address.ADDRESS,
                            address_name = x.CreateEvent.Address.ADDRESS_NAME,
                            contract = new Contract
                            {
                                name = x.CreateEvent.Contract.NAME,
                                hash = x.CreateEvent.Contract.HASH,
                                symbol = x.CreateEvent.Contract.SYMBOL
                            },
                            string_event = EventPayloadMapper.ParseStringEvent(x.CreateEvent.PAYLOAD_JSON)
                        }
                        : null,
                    address = with_address == 1 && x.ADDRESS != null && x.ADDRESS_NAME != null
                        ? new Address
                        {
                            address = x.ADDRESS,
                            address_name = x.ADDRESS_NAME
                        }
                        : null
                }
            });

            var cursorFiltered = CursorPagination.ApplyCursor(pageQuery, orderDefinition, sortDirection, cursorToken,
                x => x.Id);
            var orderedQuery = CursorPagination.ApplyOrdering(cursorFiltered, orderDefinition, sortDirection,
                x => x.Id);
            var page = await CursorPagination.ReadPageAsync(orderedQuery, orderDefinition, sortDirection, x => x.Id,
                limit);
            organizationArray = page.Items.Select(x => x.ApiOrganization).ToArray();
            nextCursor = page.NextCursor;


            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("Organization()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new OrganizationResult
        {
            total_results = null,
            organizations = organizationArray,
            next_cursor = nextCursor
        };
    }
}
