#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Service.Api;

public enum CursorSortDirection
{
    Asc,
    Desc
}

public sealed class CursorToken
{
    public string order_by { get; init; } = string.Empty;
    public string order_direction { get; init; } = string.Empty;
    public string[] values { get; init; } = Array.Empty<string>();
    public int id { get; init; }
}

public sealed class CursorPage<T>
{
    public CursorPage(IReadOnlyList<T> items, string? nextCursor)
    {
        Items = items;
        NextCursor = nextCursor;
    }

    public IReadOnlyList<T> Items { get; }
    public string? NextCursor { get; }
}

public interface ICursorOrderSegment<T>
{
    CursorSortDirection ResolveDirection(CursorSortDirection requestedDirection);
    IOrderedQueryable<T> ApplyOrdering(IQueryable<T> query, CursorSortDirection requestedDirection);
    IOrderedQueryable<T> ApplyThenOrdering(IOrderedQueryable<T> query, CursorSortDirection requestedDirection);
    Expression BuildComparison(ParameterExpression parameter, string rawValue, CursorSortDirection requestedDirection,
        Expression whenEqual);
    string SerializeValue(T item);
}

public sealed class CursorOrderSegment<T, TValue> : ICursorOrderSegment<T>
    where TValue : IComparable
{
    private readonly Expression<Func<T, TValue>> _selector;
    private readonly Func<TValue, string> _serialize;
    private readonly Func<string, TValue> _parse;
    private readonly Func<CursorSortDirection, CursorSortDirection> _directionResolver;
    private readonly Func<T, TValue> _valueGetter;

    public CursorOrderSegment(
        Expression<Func<T, TValue>> selector,
        Func<string, TValue> parse,
        Func<TValue, string>? serialize = null,
        Func<CursorSortDirection, CursorSortDirection>? directionResolver = null)
    {
        _selector = selector;
        _parse = parse;
        _serialize = serialize ?? (value => (value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value?.ToString()) ?? string.Empty);
        _directionResolver = directionResolver ?? (direction => direction);
        _valueGetter = selector.Compile();
    }

    public CursorSortDirection ResolveDirection(CursorSortDirection requestedDirection)
    {
        return _directionResolver(requestedDirection);
    }

    public IOrderedQueryable<T> ApplyOrdering(IQueryable<T> query, CursorSortDirection requestedDirection)
    {
        var direction = ResolveDirection(requestedDirection);
        return direction == CursorSortDirection.Asc ? query.OrderBy(_selector) : query.OrderByDescending(_selector);
    }

    public IOrderedQueryable<T> ApplyThenOrdering(IOrderedQueryable<T> query, CursorSortDirection requestedDirection)
    {
        var direction = ResolveDirection(requestedDirection);
        return direction == CursorSortDirection.Asc ? query.ThenBy(_selector) : query.ThenByDescending(_selector);
    }

    public Expression BuildComparison(
        ParameterExpression parameter,
        string rawValue,
        CursorSortDirection requestedDirection,
        Expression whenEqual)
    {
        var direction = ResolveDirection(requestedDirection);
        var selectorBody = ReplaceParameter(_selector.Body, _selector.Parameters[0], parameter);
        var constant = Expression.Constant(_parse(rawValue), typeof(TValue));
        Expression equals;
        Expression comparison;

        // String keyset comparisons need string.Compare(...), because expression operators
        // like GreaterThan/LessThan are not defined for System.String.
        if (typeof(TValue) == typeof(string))
        {
            var compareMethod = typeof(string).GetMethod(nameof(string.Compare), new[] { typeof(string), typeof(string) });
            if (compareMethod == null)
                throw new InvalidOperationException("Failed to resolve string.Compare(string, string).");

            var compareCall = Expression.Call(
                compareMethod,
                Expression.Convert(selectorBody, typeof(string)),
                Expression.Convert(constant, typeof(string)));

            equals = Expression.Equal(compareCall, Expression.Constant(0));
            comparison = direction == CursorSortDirection.Asc
                ? Expression.GreaterThan(compareCall, Expression.Constant(0))
                : Expression.LessThan(compareCall, Expression.Constant(0));
        }
        else
        {
            equals = Expression.Equal(selectorBody, constant);
            comparison = direction == CursorSortDirection.Asc
                ? Expression.GreaterThan(selectorBody, constant)
                : Expression.LessThan(selectorBody, constant);
        }

        return Expression.OrElse(comparison, Expression.AndAlso(equals, whenEqual));
    }

    public string SerializeValue(T item)
    {
        return _serialize(_valueGetter(item));
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, Expression target)
    {
        return new ReplaceVisitor(source, target).Visit(expression);
    }

    private sealed class ReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly Expression _target;

        public ReplaceVisitor(ParameterExpression source, Expression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}

public sealed class CursorOrderDefinition<T>
{
    public CursorOrderDefinition(string name, params ICursorOrderSegment<T>[] segments)
    {
        Name = name;
        Segments = segments;
    }

    public string Name { get; }
    public IReadOnlyList<ICursorOrderSegment<T>> Segments { get; }
}

public static class CursorPagination
{
    public static CursorSortDirection ParseSortDirection(string orderDirection)
    {
        if (orderDirection.Equals("asc", StringComparison.OrdinalIgnoreCase))
            return CursorSortDirection.Asc;

        if (orderDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
            return CursorSortDirection.Desc;

        throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");
    }

    public static CursorToken? ParseCursor(string? rawCursor)
    {
        if (string.IsNullOrWhiteSpace(rawCursor)) return null;

        try
        {
            var decoded = WebEncoders.Base64UrlDecode(rawCursor);
            var json = Encoding.UTF8.GetString(decoded);
            return JsonSerializer.Deserialize<CursorToken>(json);
        }
        catch (Exception exception)
        {
            throw new ApiParameterException($"Unsupported value for 'cursor' parameter. {exception.Message}");
        }
    }

    public static IOrderedQueryable<T> ApplyOrdering<T>(
        IQueryable<T> query,
        CursorOrderDefinition<T> orderDefinition,
        CursorSortDirection sortDirection,
        Expression<Func<T, int>> idSelector)
    {
        if (orderDefinition.Segments.Count == 0)
            throw new ApiUnexpectedException(
                "Cursor order definition must contain at least one segment",
                new InvalidOperationException("Cursor order definition has no segments."));

        var ordered = orderDefinition.Segments[0].ApplyOrdering(query, sortDirection);

        foreach (var segment in orderDefinition.Segments.Skip(1))
            ordered = segment.ApplyThenOrdering(ordered, sortDirection);

        ordered = sortDirection == CursorSortDirection.Asc
            ? ordered.ThenBy(idSelector)
            : ordered.ThenByDescending(idSelector);

        return ordered;
    }

    public static IQueryable<T> ApplyCursor<T>(
        IQueryable<T> query,
        CursorOrderDefinition<T> orderDefinition,
        CursorSortDirection sortDirection,
        CursorToken? cursorToken,
        Expression<Func<T, int>> idSelector)
    {
        if (cursorToken == null) return query;

        if (!orderDefinition.Name.Equals(cursorToken.order_by, StringComparison.OrdinalIgnoreCase))
            throw new ApiParameterException("Cursor order_by does not match request parameters.");

        if (cursorToken.values.Length != orderDefinition.Segments.Count)
            throw new ApiParameterException("Cursor is incompatible with the requested order.");

        var directionFromCursor = ParseSortDirection(cursorToken.order_direction);
        if (directionFromCursor != sortDirection)
            throw new ApiParameterException("Cursor order_direction does not match request parameters.");

        var parameter = Expression.Parameter(typeof(T), "item");

        Expression comparison = BuildTailComparison(parameter, idSelector, cursorToken.id, sortDirection);

        for (var i = orderDefinition.Segments.Count - 1; i >= 0; i--)
        {
            var segment = orderDefinition.Segments[i];
            var rawValue = cursorToken.values[i];

            try
            {
                comparison = segment.BuildComparison(parameter, rawValue, sortDirection, comparison);
            }
            catch (Exception exception) when (exception is FormatException || exception is OverflowException)
            {
                throw new ApiParameterException("Unsupported cursor value for requested order.");
            }
        }

        var lambda = Expression.Lambda<Func<T, bool>>(comparison, parameter);
        return query.Where(lambda);
    }

    public static async Task<CursorPage<T>> ReadPageAsync<T>(
        IQueryable<T> query,
        CursorOrderDefinition<T> orderDefinition,
        CursorSortDirection sortDirection,
        Expression<Func<T, int>> idSelector,
        int limit)
    {
        var pageSize = Math.Max(1, limit);
        var pageItems = await query.Take(pageSize + 1).ToArrayAsync();
        var hasMore = pageItems.Length > pageSize;
        var items = hasMore ? pageItems.Take(pageSize).ToArray() : pageItems;

        string? nextCursor = null;

        if (hasMore && items.Length > 0)
        {
            var lastItem = items[^1];
            var idGetter = idSelector.Compile();
            var values = orderDefinition.Segments.Select(s => s.SerializeValue(lastItem)).ToArray();
            var token = new CursorToken
            {
                order_by = orderDefinition.Name,
                order_direction = sortDirection == CursorSortDirection.Asc ? "asc" : "desc",
                values = values,
                id = idGetter(lastItem)
            };
            var json = JsonSerializer.Serialize(token);
            nextCursor = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        }

        return new CursorPage<T>(items, nextCursor);
    }

    private static Expression BuildTailComparison<T>(
        ParameterExpression parameter,
        Expression<Func<T, int>> idSelector,
        int idValue,
        CursorSortDirection sortDirection)
    {
        var selectorBody = ReplaceParameter(idSelector.Body, idSelector.Parameters[0], parameter);
        var constant = Expression.Constant(idValue);

        return sortDirection == CursorSortDirection.Asc
            ? Expression.GreaterThan(selectorBody, constant)
            : Expression.LessThan(selectorBody, constant);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression source, Expression target)
    {
        return new ReplaceVisitor(source, target).Visit(expression);
    }

    private sealed class ReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly Expression _target;

        public ReplaceVisitor(ParameterExpression source, Expression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}
