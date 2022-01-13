using System;
using System.Collections.Generic;
using System.Linq;

namespace GhostDevs;

public class QueryBuilder
{
    public enum JoinType
    {
        INNER,
        LEFT
    }

    public enum NullsOrderType
    {
        DEFAULT,
        FIRST,
        LAST
    }

    private readonly List<QueryColumn> Columns;

    private readonly bool Distinct;
    private readonly string DistinctFields;
    private readonly List<string> FromTables;
    private readonly List<QueryJoin> Joins;
    private readonly List<Tuple<string, object>> Parameters;
    private readonly List<string> Subselects;
    private readonly List<string> WhereClauses;
    private string CompleteOrderBy;
    private bool GroupByAuto;
    private string GroupByColumn;
    private string GroupByTable;
    private string Limit;
    private NullsOrderType NullsOrder;
    private string Offset;
    private string OrderByColumn;

    private string OrderByTable;
    private string OrderDirection;


    public QueryBuilder(bool distinct = false, string distinctFields = null)
    {
        Distinct = distinct;
        DistinctFields = distinctFields;
        Columns = new List<QueryColumn>();
        Subselects = new List<string>();
        FromTables = new List<string>();
        Joins = new List<QueryJoin>();
        WhereClauses = new List<string>();
        Parameters = new List<Tuple<string, object>>();

        NullsOrder = NullsOrderType.DEFAULT;
    }


    public void AddColumn(string tableNameOrAlias, string columnName, string columnAlias = "",
        bool quoteColumnName = true)
    {
        Columns.Add(new QueryColumn(tableNameOrAlias, columnName, columnAlias, quoteColumnName));
    }


    public void AddSubselect(string subselect)
    {
        Subselects.Add(subselect);
    }


    public void AddFrom(string table)
    {
        FromTables.Add(table);
    }


    public void AddJoin(JoinType joinType, string joinTableName, string joinTableAlias, string joinColumnName,
        string onTableNameOrAlias, string onColumnName)
    {
        Joins.Add(new QueryJoin(joinType, joinTableName, joinTableAlias, joinColumnName, onTableNameOrAlias,
            onColumnName));
    }


    public void AddJoin(string completeJoin)
    {
        Joins.Add(new QueryJoin(completeJoin));
    }


    public void AddWhere(string clause, bool addBrackets = false)
    {
        if ( addBrackets )
            WhereClauses.Add("(" + clause + ")");
        else
            WhereClauses.Add(clause);
    }


    public void AddParam(string name, object value)
    {
        Parameters.Add(new Tuple<string, object>(name, value));
    }


    public void SetOrderBy(string orderByTable, string orderByColumn)
    {
        OrderByTable = orderByTable;
        OrderByColumn = orderByColumn;
    }


    public void SetGroupBy(string groupByTable, string groupByColumn)
    {
        GroupByTable = groupByTable;
        GroupByColumn = groupByColumn;
    }


    public void SetGroupByAuto(bool autoGroupBy)
    {
        GroupByAuto = autoGroupBy;
    }


    public void SetOrderBy(string completeOrderBy)
    {
        CompleteOrderBy = completeOrderBy;
    }


    public void SetOrderDirection(string orderDirection)
    {
        OrderDirection = orderDirection;
    }


    public void SetNullsOrder(NullsOrderType nullsOrder)
    {
        NullsOrder = nullsOrder;
    }


    public void SetLimit(int limit)
    {
        Limit = limit.ToString();
    }


    public void SetOffset(int offset)
    {
        Offset = offset.ToString();
    }


    private string BuildFromPart()
    {
        var query = "from " + string.Join(", ", FromTables.Select(x => $@"""{x}""").ToArray());
        query += " ";

        query += string.Join("\n", Joins.Select(x =>
                !string.IsNullOrEmpty(x.CompleteJoin)
                    ? x.CompleteJoin
                    : x.JoinType.ToString().ToLower() +
                      $@" join ""{x.JoinTableName}"" " +
                      ( !string.IsNullOrEmpty(x.JoinTableAlias) ? $@"as ""{x.JoinTableAlias}"" " : "" ) +
                      $@"on ""{x.OnTableNameOrAlias}"".""{x.OnColumnName}"" = ""{( !string.IsNullOrEmpty(x.JoinTableAlias) ? x.JoinTableAlias : x.JoinTableName )}"".""{x.JoinColumnName}""")
            .ToArray());

        query += "\n";

        var where = "";
        for ( var i = 0; i < WhereClauses.Count; i++ )
        {
            if ( i == 0 )
                @where += "where ";
            else
                @where += " and ";

            where += WhereClauses[i];
        }

        query += where;

        return query;
    }


    private string BuildOrderLimitPart()
    {
        var nullsOrder = "";
        switch ( NullsOrder )
        {
            case NullsOrderType.FIRST:
                nullsOrder = " NULLS FIRST";
                break;
            case NullsOrderType.LAST:
                nullsOrder = " NULLS LAST";
                break;
        }

        var part = "";


        if ( !string.IsNullOrEmpty(GroupByColumn) )
            part += "group by " +
                    ( !string.IsNullOrEmpty(GroupByTable) ? $@"""{GroupByTable}""." : "" ) +
                    $@"""{GroupByColumn}"" ";
        else if ( GroupByAuto )
            part += "group by " + string.Join(",\n",
                Columns.Select(x => ( !string.IsNullOrEmpty(x.TableNameOrAlias) ? $@"""{x.TableNameOrAlias}""." : "" ) +
                                    ( x.QuoteColumnName ? $@"""{x.ColumnName}""" : $@"{x.ColumnName}" )).ToArray());


        if ( !string.IsNullOrEmpty(CompleteOrderBy) )
            part += CompleteOrderBy;
        else if ( !string.IsNullOrEmpty(OrderByColumn) )
            part += "order by " +
                    ( !string.IsNullOrEmpty(OrderByTable) ? $@"""{OrderByTable}""." : "" ) +
                    $@"""{OrderByColumn}""";

        if ( !string.IsNullOrEmpty(OrderDirection) )
            part += ( !string.IsNullOrEmpty(part) ? " " : "" ) + $"{OrderDirection}";

        part += nullsOrder;

        if ( !string.IsNullOrEmpty(Limit) ) part += ( !string.IsNullOrEmpty(part) ? " " : "" ) + $"limit {Limit}";

        if ( !string.IsNullOrEmpty(Offset) ) part += ( !string.IsNullOrEmpty(part) ? " " : "" ) + $"offset {Offset}";

        return part;
    }


    public string GetQuery()
    {
        var query = "select ";

        if ( Distinct )
        {
            if ( string.IsNullOrEmpty(DistinctFields) )
                query += "distinct ";
            else
            {
                query += "distinct on (" + DistinctFields;

                // Including order by into distinct
                if ( !string.IsNullOrEmpty(OrderByColumn) )
                    query += ", " +
                             ( !string.IsNullOrEmpty(OrderByTable) ? $@"""{OrderByTable}""." : "" ) +
                             $@"""{OrderByColumn}""";

                query += ") ";
            }
        }

        query += string.Join(",\n",
            Columns.Select(x => ( !string.IsNullOrEmpty(x.TableNameOrAlias) ? $@"""{x.TableNameOrAlias}""." : "" ) +
                                ( x.QuoteColumnName ? $@"""{x.ColumnName}""" : $@"{x.ColumnName}" ) +
                                ( !string.IsNullOrEmpty(x.ColumnAlias) ? $@" as ""{x.ColumnAlias}""" : "" )).ToArray());

        if ( Columns.Count > 0 && Subselects.Count > 0 ) query += ",\n";

        query += string.Join(",\n", Subselects.Select(x => $"({x})").ToArray());

        query += "\n";
        query += BuildFromPart();

        query += "\n";
        query += BuildOrderLimitPart();

        return query;
    }


    public string GetCountQuery(string countAlias)
    {
        var query = "select ";

        /*if (Distinct)
        {
            if (string.IsNullOrEmpty(DistinctFields))
            {
                query += "distinct ";
            }
            else
            {
                query += "distinct on (" + DistinctFields;

                // Including order by into distinct
                if (!String.IsNullOrEmpty(OrderByColumn))
                {
                    query += ", " +
                        (!String.IsNullOrEmpty(OrderByTable) ? $@"""{OrderByTable}""." : "") +
                        $@"""{OrderByColumn}""";
                }

                query += ") ";
            }
        }*/

        query += $@"count(*) AS {countAlias} ";

        query += "\n";

        query += BuildFromPart();

        return query;
    }


    public List<Tuple<string, object>> GetParams()
    {
        return Parameters;
    }


    private class QueryColumn
    {
        public readonly string ColumnAlias;
        public readonly string ColumnName;
        public readonly bool QuoteColumnName;
        public readonly string TableNameOrAlias;


        public QueryColumn(string tableNameOrAlias, string columnName, string columnAlias, bool quoteColumnName = true)
        {
            TableNameOrAlias = tableNameOrAlias;
            ColumnName = columnName;
            ColumnAlias = columnAlias;
            QuoteColumnName = quoteColumnName;
        }
    }

    private class QueryJoin
    {
        public readonly string CompleteJoin;
        public readonly string JoinColumnName;
        public readonly string JoinTableAlias;
        public readonly string JoinTableName;
        public readonly JoinType JoinType;
        public readonly string OnColumnName;
        public readonly string OnTableNameOrAlias;


        public QueryJoin(JoinType joinType, string joinTableName, string joinTableAlias, string joinColumnName,
            string onTableNameOrAlias, string onColumnName)
        {
            JoinType = joinType;
            JoinTableName = joinTableName;
            JoinTableAlias = joinTableAlias;
            JoinColumnName = joinColumnName;
            OnTableNameOrAlias = onTableNameOrAlias;
            OnColumnName = onColumnName;
            CompleteJoin = null;
        }


        public QueryJoin(string completeJoin)
        {
            CompleteJoin = completeJoin;
        }
    }
}
