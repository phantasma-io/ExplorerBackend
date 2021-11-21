using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

namespace GhostDevs
{
    public class QueryBuilder
    {
        private class QueryColumn
        {
            public string TableNameOrAlias;
            public string ColumnName;
            public string ColumnAlias;
            public bool QuoteColumnName;

            public QueryColumn(string tableNameOrAlias, string columnName, string columnAlias, bool quoteColumnName = true)
            {
                TableNameOrAlias = tableNameOrAlias;
                ColumnName = columnName;
                ColumnAlias = columnAlias;
                QuoteColumnName = quoteColumnName;
            }
        }
        public enum JoinType
        {
            INNER,
            LEFT
        }
        private class QueryJoin
        {
            public JoinType JoinType;
            public string JoinTableName;
            public string JoinTableAlias;
            public string JoinColumnName;
            public string OnTableNameOrAlias;
            public string OnColumnName;
            public string CompleteJoin;

            public QueryJoin(JoinType joinType, string joinTableName, string joinTableAlias, string joinColumnName, string onTableNameOrAlias, string onColumnName)
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

        public enum NullsOrderType
        {
            DEFAULT,
            FIRST,
            LAST
        }

        private bool Distinct;
        private string DistinctFields;
        private List<QueryColumn> Columns;
        private List<string> Subselects;
        private List<string> FromTables;
        private List<QueryJoin> Joins;
        private List<string> WhereClauses;
        private List<Tuple<string, object>> Parameters;

        private string OrderByTable;
        private string OrderByColumn;
        private string GroupByTable;
        private string GroupByColumn;
        private bool GroupByAuto;
        private string CompleteOrderBy;
        private string OrderDirection;
        private NullsOrderType NullsOrder;
        private string Limit;
        private string Offset;

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

        public void AddColumn(string tableNameOrAlias, string columnName, string columnAlias = "", bool quoteColumnName = true)
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
        public void AddJoin(JoinType joinType, string joinTableName, string joinTableAlias, string joinColumnName, string onTableNameOrAlias, string onColumnName)
        {
            Joins.Add(new QueryJoin(joinType, joinTableName, joinTableAlias, joinColumnName, onTableNameOrAlias, onColumnName));
        }
        public void AddJoin(string completeJoin)
        {
            Joins.Add(new QueryJoin(completeJoin));
        }
        public void AddWhere(string clause, bool addBrackets = false)
        {
            if (addBrackets)
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
            var query = "from " + String.Join(", ", FromTables.Select(x => $@"""{x}""").ToArray());
            query += " ";

            query += String.Join("\n", Joins.Select(x =>
                !String.IsNullOrEmpty(x.CompleteJoin) ? x.CompleteJoin :
                x.JoinType.ToString().ToLower() +
                $@" join ""{x.JoinTableName}"" " +
                (!String.IsNullOrEmpty(x.JoinTableAlias) ? $@"as ""{x.JoinTableAlias}"" " : "") +
                $@"on ""{x.OnTableNameOrAlias}"".""{x.OnColumnName}"" = ""{(!String.IsNullOrEmpty(x.JoinTableAlias) ? x.JoinTableAlias : x.JoinTableName)}"".""{x.JoinColumnName}""").ToArray());

            query += "\n";

            var where = "";
            for (int i = 0; i < WhereClauses.Count; i++)
            {
                if (i == 0)
                    where += "where ";
                else
                    where += " and ";

                where += WhereClauses[i];
            }

            query += where;

            return query;
        }
        private string BuildOrderLimitPart()
        {
            var nullsOrder = "";
            switch(NullsOrder)
            {
                case NullsOrderType.FIRST:
                    nullsOrder = " NULLS FIRST";
                    break;
                case NullsOrderType.LAST:
                    nullsOrder = " NULLS LAST";
                    break;
            }

            var part = "";


            if (!String.IsNullOrEmpty(GroupByColumn))
            {
                part += "group by " +
                    (!String.IsNullOrEmpty(GroupByTable) ? $@"""{GroupByTable}""." : "") +
                    $@"""{GroupByColumn}"" ";
            }
            else if(GroupByAuto)
            {
                part += "group by " + String.Join(",\n",
                    Columns.Select(x => (!String.IsNullOrEmpty(x.TableNameOrAlias) ? $@"""{x.TableNameOrAlias}""." : "") +
                        (x.QuoteColumnName ? $@"""{x.ColumnName}""" : $@"{x.ColumnName}")).ToArray());
            }


            if (!String.IsNullOrEmpty(CompleteOrderBy))
            {
                part += CompleteOrderBy;
            }    
            else if (!String.IsNullOrEmpty(OrderByColumn))
            {
                part += "order by " +
                    (!String.IsNullOrEmpty(OrderByTable) ? $@"""{OrderByTable}""." : "") +
                    $@"""{OrderByColumn}""";
            }

            if (!String.IsNullOrEmpty(OrderDirection))
            {
                part += (!String.IsNullOrEmpty(part) ? " " : "") + $"{OrderDirection}";
            }

            part += nullsOrder;

            if (!String.IsNullOrEmpty(Limit))
            {
                part += (!String.IsNullOrEmpty(part) ? " " : "") + $"limit {Limit}";
            }
            if (!String.IsNullOrEmpty(Offset))
            {
                part += (!String.IsNullOrEmpty(part) ? " " : "") + $"offset {Offset}";
            }

            return part;
        }
        public string GetQuery()
        {
            var query = "select ";

            if (Distinct)
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
            }

            query += String.Join(",\n", 
                Columns.Select(x => (!String.IsNullOrEmpty(x.TableNameOrAlias) ? $@"""{x.TableNameOrAlias}""." : "") + 
                    (x.QuoteColumnName ? $@"""{x.ColumnName}""" : $@"{x.ColumnName}") +
                    (!String.IsNullOrEmpty(x.ColumnAlias) ? $@" as ""{x.ColumnAlias}""" : "")).ToArray());

            if(Columns.Count > 0 && Subselects.Count > 0)
                query += ",\n";

            query += String.Join(",\n", Subselects.Select(x => $"({x})").ToArray());

            query += "\n";
            query += BuildFromPart();

            query += "\n";
            query += BuildOrderLimitPart();

            return query;
        }
        public string GetCountQuery(string countAlias)
        {
            var query = $"select ";

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
    }
}
