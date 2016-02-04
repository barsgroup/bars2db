﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LinqToDB.SqlQuery
{
    using System.Threading.Tasks;

    using LinqToDB.SqlQuery.QueryElements;
    using LinqToDB.SqlQuery.QueryElements.Clauses;
    using LinqToDB.SqlQuery.QueryElements.Conditions;
    using LinqToDB.SqlQuery.QueryElements.Interfaces;
    using LinqToDB.SqlQuery.QueryElements.Predicates;
    using LinqToDB.SqlQuery.SqlElements;
    using LinqToDB.SqlQuery.SqlElements.Interfaces;

    using SqlProvider;

	class SelectQueryOptimizer
	{
		public SelectQueryOptimizer(SqlProviderFlags flags, SelectQuery selectQuery)
		{
			_flags       = flags;
			_selectQuery = selectQuery;
		}

		readonly SqlProviderFlags _flags;
		readonly SelectQuery      _selectQuery;

		public void FinalizeAndValidate(bool isApplySupported, bool optimizeColumns)
		{
//#if DEBUG
//			if (_selectQuery.IsUpdate)
//			{
//			}

//			var sqlText = _selectQuery.SqlText;

//			var dic = new Dictionary<SelectQuery,SelectQuery>();

//		    foreach (var element in QueryVisitor.FindAll<SelectQuery>(_selectQuery))
//		    {
//                if (dic.ContainsKey(element))
//                    throw new InvalidOperationException("SqlQuery circle reference detected.");

//                dic.Add(element, element);
//            }
//#endif

			OptimizeUnions();
			FinalizeAndValidateInternal(isApplySupported, optimizeColumns, new List<ISqlTableSource>());
			ResolveFields();
			_selectQuery.SetAliases();

//#if DEBUG
//			sqlText = _selectQuery.SqlText;
//#endif
		}

		class QueryData
		{
			public SelectQuery          Query;
			public List<ISqlExpression> Fields  = new List<ISqlExpression>();
			public List<QueryData>      Queries = new List<QueryData>();
		}

		void ResolveFields()
		{
			var root = GetQueryData(_selectQuery);

			ResolveFields(root);
		}

		static QueryData GetQueryData(SelectQuery selectQuery)
		{
			var data = new QueryData { Query = selectQuery };

			new QueryVisitor().VisitParentFirst(selectQuery, e =>
			{
				switch (e.ElementType)
				{
					case QueryElementType.SqlField :
						{
							var field = (SqlField)e;

							if (field.Name.Length != 1 || field.Name[0] != '*')
								data.Fields.Add(field);

							break;
						}

					case QueryElementType.SqlQuery :
						{
							if (e != selectQuery)
							{
								data.Queries.Add(GetQueryData((SelectQuery)e));
								return false;
							}

							break;
						}

					case QueryElementType.Column :
						return ((Column)e).Parent == selectQuery;

					case QueryElementType.SqlTable :
						return false;
				}

				return true;
			});

			return data;
		}

		static TableSource FindField(SqlField field, TableSource table)
		{
			if (field.Table == table.Source)
				return table;

			foreach (var @join in table.Joins)
			{
				var t = FindField(field, @join.Table);

				if (t != null)
					return @join.Table;
			}

			return null;
		}

		static ISqlExpression GetColumn(QueryData data, SqlField field)
		{
			foreach (var query in data.Queries)
			{
				var q = query.Query;

				foreach (var table in q.From.Tables)
				{
					var t = FindField(field, table);

					if (t != null)
					{
						var n   = q.Select.Columns.Count;
						var idx = q.Select.Add(field);

						if (n != q.Select.Columns.Count)
							if (!q.GroupBy.IsEmpty || q.Select.Columns.Any(c => IsAggregationFunction(c.Expression)))
								q.GroupBy.Items.Add(field);

						return q.Select.Columns[idx];
					}
				}
			}

			return null;
		}

		static void ResolveFields(QueryData data)
		{
			if (data.Queries.Count == 0)
				return;

			var dic = new Dictionary<ISqlExpression,ISqlExpression>();

			foreach (SqlField field in data.Fields)
			{
				if (dic.ContainsKey(field))
					continue;

				var found = false;

				foreach (var table in data.Query.From.Tables)
				{
					found = FindField(field, table) != null;

					if (found)
						break;
				}

				if (!found)
				{
					var expr = GetColumn(data, field);

					if (expr != null)
						dic.Add(field, expr);
				}
			}

			if (dic.Count > 0)
				new QueryVisitor().VisitParentFirst(data.Query, e =>
				{
					ISqlExpression ex;

					switch (e.ElementType)
					{
						case QueryElementType.SqlQuery :
							return e == data.Query;

						case QueryElementType.SqlFunction :
							{
								var parms = ((SqlFunction)e).Parameters;

								for (var i = 0; i < parms.Length; i++)
									if (dic.TryGetValue(parms[i], out ex))
										parms[i] = ex;

								break;
							}

						case QueryElementType.SqlExpression :
							{
								var parms = ((SqlExpression)e).Parameters;

								for (var i = 0; i < parms.Length; i++)
									if (dic.TryGetValue(parms[i], out ex))
										parms[i] = ex;

								break;
							}

						case QueryElementType.SqlBinaryExpression :
							{
								var expr = (SqlBinaryExpression)e;
								if (dic.TryGetValue(expr.Expr1, out ex)) expr.Expr1 = ex;
								if (dic.TryGetValue(expr.Expr2, out ex)) expr.Expr2 = ex;
								break;
							}

						case QueryElementType.ExprPredicate       :
						case QueryElementType.NotExprPredicate    :
						case QueryElementType.IsNullPredicate     :
						case QueryElementType.InSubQueryPredicate :
							{
								var expr = (Expr)e;
								if (dic.TryGetValue(expr.Expr1, out ex)) expr.Expr1 = ex;
								break;
							}

						case QueryElementType.ExprExprPredicate :
							{
								var expr = (ExprExpr)e;
								if (dic.TryGetValue(expr.Expr1, out ex)) expr.Expr1 = ex;
								if (dic.TryGetValue(expr.Expr2, out ex)) expr.Expr2 = ex;
								break;
							}

						case QueryElementType.LikePredicate :
							{
								var expr = (Like)e;
								if (dic.TryGetValue(expr.Expr1,  out ex)) expr.Expr1  = ex;
								if (dic.TryGetValue(expr.Expr2,  out ex)) expr.Expr2  = ex;
								if (dic.TryGetValue(expr.Escape, out ex)) expr.Escape = ex;
								break;
							}

						case QueryElementType.BetweenPredicate :
							{
								var expr = (Between)e;
								if (dic.TryGetValue(expr.Expr1, out ex)) expr.Expr1 = ex;
								if (dic.TryGetValue(expr.Expr2, out ex)) expr.Expr2 = ex;
								if (dic.TryGetValue(expr.Expr3, out ex)) expr.Expr3 = ex;
								break;
							}

						case QueryElementType.InListPredicate :
							{
								var expr = (InList)e;

								if (dic.TryGetValue(expr.Expr1, out ex)) expr.Expr1 = ex;

								for (var i = 0; i < expr.Values.Count; i++)
									if (dic.TryGetValue(expr.Values[i], out ex))
										expr.Values[i] = ex;

								break;
							}

						case QueryElementType.Column :
							{
								var expr = (Column)e;

								if (expr.Parent != data.Query)
									return false;

								if (dic.TryGetValue(expr.Expression, out ex)) expr.Expression = ex;

								break;
							}

						case QueryElementType.SetExpression :
							{
								var expr = (SetExpression)e;
								if (dic.TryGetValue(expr.Expression, out ex)) expr.Expression = ex;
								break;
							}

						case QueryElementType.GroupByClause :
							{
								var expr = (GroupByClause)e;

								for (var i = 0; i < expr.Items.Count; i++)
									if (dic.TryGetValue(expr.Items[i], out ex))
										expr.Items[i] = ex;

								break;
							}

						case QueryElementType.OrderByItem :
							{
								var expr = (OrderByItem)e;
								if (dic.TryGetValue(expr.Expression, out ex)) expr.Expression = ex;
								break;
							}
					}

					return true;
				});

			foreach (var query in data.Queries)
				if (query.Queries.Count > 0)
					ResolveFields(query);
		}

	    void OptimizeUnions()
	    {
	        var exprs = new Dictionary<ISqlExpression, ISqlExpression>();

	        foreach (var element in QueryVisitor.FindOnce<SelectQuery>(_selectQuery))
	        {
	            if (element.From.Tables.Count != 1 || !element.IsSimple || element.IsInsert || element.IsUpdate || element.IsDelete)
	                return;

	            var table = element.From.Tables[0];

	            if (table.Joins.Count != 0 || !(table.Source is SelectQuery))
	                return;

	            var union = (SelectQuery)table.Source;

	            if (!union.HasUnion)
	                return;

	            for (var i = 0; i < element.Select.Columns.Count; i++)
	            {
	                var scol = element.Select.Columns[i];
	                var ucol = union.Select.Columns[i];

	                if (scol.Expression != ucol)
	                    return;
	            }

	            exprs.Add(union, element);

	            for (var i = 0; i < element.Select.Columns.Count; i++)
	            {
	                var scol = element.Select.Columns[i];
	                var ucol = union.Select.Columns[i];

	                scol.Expression = ucol.Expression;
	                scol._alias = ucol._alias;

	                exprs.Add(ucol, scol);
	            }

	            for (var i = element.Select.Columns.Count; i < union.Select.Columns.Count; i++)
	                element.Select.Expr(union.Select.Columns[i].Expression);

	            element.From.Tables.Clear();
	            element.From.Tables.AddRange(union.From.Tables);

	            element.Where.SearchCondition.Conditions.AddRange(union.Where.SearchCondition.Conditions);
	            element.Having.SearchCondition.Conditions.AddRange(union.Having.SearchCondition.Conditions);
	            element.GroupBy.Items.AddRange(union.GroupBy.Items);
	            element.OrderBy.Items.AddRange(union.OrderBy.Items);
	            element.Unions.InsertRange(0, union.Unions);
	        }

	        ((ISqlExpressionWalkable)_selectQuery).Walk(
	            false,
	            expr =>
	            {
	                ISqlExpression e;

	                if (exprs.TryGetValue(expr, out e))
	                    return e;

	                return expr;
	            });
	    }

	    void FinalizeAndValidateInternal(bool isApplySupported, bool optimizeColumns, List<ISqlTableSource> tables)
	    {
	        OptimizeSearchCondition(_selectQuery.Where.SearchCondition);
	        OptimizeSearchCondition(_selectQuery.Having.SearchCondition);

	        _selectQuery.ForEachTable(
	            table =>
	            {
	                foreach (var join in table.Joins)
	                    OptimizeSearchCondition(join.Condition);
	            },
	            new HashSet<SelectQuery>());

	        foreach (var query in QueryVisitor.FindOnce<
                SelectQuery>(_selectQuery).Where(item => item != _selectQuery))
	        {
                query.ParentSelect = _selectQuery;

                new SelectQueryOptimizer(_flags, query).FinalizeAndValidateInternal(isApplySupported, optimizeColumns, tables);

                if (query.IsParameterDependent)
                    _selectQuery.IsParameterDependent = true;
            }

	        ResolveWeakJoins(tables);
	        OptimizeColumns();
	        OptimizeApplies(isApplySupported, optimizeColumns);
	        OptimizeSubQueries(isApplySupported, optimizeColumns);
	        OptimizeApplies(isApplySupported, optimizeColumns);

	        foreach (var item in QueryVisitor.FindAll<SelectQuery>(_selectQuery).Where(item => item != _selectQuery))
	        {
	            RemoveOrderBy(item);
	        }

	    }

	    internal static void OptimizeSearchCondition(SearchCondition searchCondition)
		{
			// This 'if' could be replaced by one simple match:
			//
			// match (searchCondition.Conditions)
			// {
			// | [SearchCondition(true, _) sc] =>
			//     searchCondition.Conditions = sc.Conditions;
			//     OptimizeSearchCondition(searchCodition)
			//
			// | [SearchCondition(false, [SearchCondition(true, [ExprExpr]) sc])] => ...
			//
			// | [Expr(true,  SqlValue(true))]
			// | [Expr(false, SqlValue(false))]
			//     searchCondition.Conditions = []
			// }
			//
			// One day I am going to rewrite all this crap in Nemerle.
			//
			if (searchCondition.Conditions.Count == 1)
			{
				var cond = searchCondition.Conditions[0];

				if (cond.Predicate is SearchCondition)
				{
					var sc = (SearchCondition)cond.Predicate;

					if (!cond.IsNot)
					{
						searchCondition.Conditions.Clear();
						searchCondition.Conditions.AddRange(sc.Conditions);

						OptimizeSearchCondition(searchCondition);
						return;
					}

					if (sc.Conditions.Count == 1)
					{
						var c1 = sc.Conditions[0];

						if (!c1.IsNot && c1.Predicate is ExprExpr)
						{
							var ee = (ExprExpr)c1.Predicate;
							Operator op;

							switch (ee.Operator)
							{
								case Operator.Equal          : op = Operator.NotEqual;       break;
								case Operator.NotEqual       : op = Operator.Equal;          break;
								case Operator.Greater        : op = Operator.LessOrEqual;    break;
								case Operator.NotLess        :
								case Operator.GreaterOrEqual : op = Operator.Less;           break;
								case Operator.Less           : op = Operator.GreaterOrEqual; break;
								case Operator.NotGreater     :
								case Operator.LessOrEqual    : op = Operator.Greater;        break;
								default: throw new InvalidOperationException();
							}

							c1.Predicate = new ExprExpr(ee.Expr1, op, ee.Expr2);

							searchCondition.Conditions.Clear();
							searchCondition.Conditions.AddRange(sc.Conditions);

							OptimizeSearchCondition(searchCondition);
							return;
						}
					}
				}

				if (cond.Predicate.ElementType == QueryElementType.ExprPredicate)
				{
					var expr = (Expr)cond.Predicate;

					if (expr.Expr1 is SqlValue)
					{
						var value = (SqlValue)expr.Expr1;

						if (value.Value is bool)
							if (cond.IsNot ? !(bool)value.Value : (bool)value.Value)
								searchCondition.Conditions.Clear();
					}
				}
			}

			for (var i = 0; i < searchCondition.Conditions.Count; i++)
			{
				var cond = searchCondition.Conditions[i];

				if (cond.Predicate.ElementType == QueryElementType.ExprPredicate)
				{
					var expr = (Expr)cond.Predicate;

					if (expr.Expr1 is SqlValue)
					{
						var value = (SqlValue)expr.Expr1;

						if (value.Value is bool)
						{
							if (cond.IsNot ? !(bool)value.Value : (bool)value.Value)
							{
								if (i > 0)
								{
									if (searchCondition.Conditions[i-1].IsOr)
									{
										searchCondition.Conditions.RemoveRange(0, i);
										OptimizeSearchCondition(searchCondition);

										break;
									}
								}
							}
						}
					}
				}
				else if (cond.Predicate is SearchCondition)
				{
					var sc = (SearchCondition)cond.Predicate;
					OptimizeSearchCondition(sc);
				}
			}
		}

		static void RemoveOrderBy(SelectQuery selectQuery)
		{
			if (selectQuery.OrderBy.Items.Count > 0 && selectQuery.Select.SkipValue == null && selectQuery.Select.TakeValue == null)
				selectQuery.OrderBy.Items.Clear();
		}

		internal void ResolveWeakJoins(List<ISqlTableSource> tables)
		{
			Func<TableSource,bool> findTable = null; findTable = table =>
			{
				if (tables.Contains(table.Source))
					return true;

				foreach (var join in table.Joins)
				{
					if (findTable(join.Table))
					{
						join.IsWeak = false;
						return true;
					}
				}

				if (table.Source is SelectQuery)
					foreach (var t in ((SelectQuery)table.Source).From.Tables)
						if (findTable(t))
							return true;

				return false;
			};

			var areTablesCollected = false;

			_selectQuery.ForEachTable(table =>
			{
				for (var i = 0; i < table.Joins.Count; i++)
				{
					var join = table.Joins[i];

					if (join.IsWeak)
					{
						if (!areTablesCollected)
						{
							areTablesCollected = true;

						    var items = new IQueryElement[]
						                {
						                    _selectQuery.Select,
						                    _selectQuery.Where,
						                    _selectQuery.GroupBy,
						                    _selectQuery.Having,
						                    _selectQuery.OrderBy,
                                            _selectQuery.IsInsert ? _selectQuery.Insert : null,
                                            _selectQuery.IsUpdate ? _selectQuery.Update : null,
                                            _selectQuery.IsDelete ? _selectQuery.Delete : null,
                                        };

						    var tableArguments = QueryVisitor.FindAll<SqlTable>(_selectQuery.From).Where(t => t.TableArguments != null).SelectMany(t => t.TableArguments);

                            var newFileds = QueryVisitor.FindAll<SqlField>(items.Union(tableArguments).ToArray())
                            .Where(field => !tables.Contains(field.Table));

                            tables.AddRange(newFileds.Select(f => f.Table));

						}

						if (findTable(join.Table))
						{
							join.IsWeak = false;
						}
						else
						{
							table.Joins.RemoveAt(i);
							i--;
						}
					}
				}
			}, new HashSet<SelectQuery>());
		}

		TableSource OptimizeSubQuery(
			TableSource source,
			bool optimizeWhere,
			bool allColumns,
			bool isApplySupported,
			bool optimizeValues,
			bool optimizeColumns)
		{
			foreach (var jt in source.Joins)
			{
				var table = OptimizeSubQuery(
					jt.Table,
					jt.JoinType == JoinType.Inner || jt.JoinType == JoinType.CrossApply,
					false,
					isApplySupported,
					jt.JoinType == JoinType.Inner || jt.JoinType == JoinType.CrossApply,
					optimizeColumns);

				if (table != jt.Table)
				{
					var sql = jt.Table.Source as SelectQuery;

					if (sql != null && sql.OrderBy.Items.Count > 0)
						foreach (var item in sql.OrderBy.Items)
							_selectQuery.OrderBy.Expr(item.Expression, item.IsDescending);

					jt.Table = table;
				}
			}

			return source.Source is SelectQuery ?
				RemoveSubQuery(source, optimizeWhere, allColumns && !isApplySupported, optimizeValues, optimizeColumns) :
				source;
		}

	    static bool CheckColumn(Column column, ISqlExpression expr, SelectQuery query, bool optimizeValues, bool optimizeColumns)
	    {
	        if (expr is SqlField || expr is Column)
	            return false;

	        if (expr is SqlValue)
	            return !optimizeValues && 1.Equals(((SqlValue)expr).Value);

	        if (expr is SqlBinaryExpression)
	        {
	            var e = (SqlBinaryExpression)expr;

	            if (e.Operation == "*" && e.Expr1 is SqlValue)
	            {
	                var value = (SqlValue)e.Expr1;

	                if (value.Value is int && (int)value.Value == -1)
	                    return CheckColumn(column, e.Expr2, query, optimizeValues, optimizeColumns);
	            }
	        }

	        var visitor = new QueryVisitor();

	        if (optimizeColumns && visitor.Find(expr, e => e is SelectQuery || IsAggregationFunction(e)) == null)
	        {
	            var q = query.ParentSelect ?? query;
	            var count = QueryVisitor.FindAll<Column>(q).Count(e => e == column);

	            return count > 2;
	        }

	        return true;
	    }

	    TableSource RemoveSubQuery(
			TableSource childSource,
			bool concatWhere,
			bool allColumns,
			bool optimizeValues,
			bool optimizeColumns)
		{
			var query = (SelectQuery)childSource. Source;

			var isQueryOK = query.From.Tables.Count == 1;

			isQueryOK = isQueryOK && (concatWhere || query.Where.IsEmpty && query.Having.IsEmpty);
			isQueryOK = isQueryOK && !query.HasUnion && query.GroupBy.IsEmpty && !query.Select.HasModifier;
			//isQueryOK = isQueryOK && (_flags.IsDistinctOrderBySupported || query.Select.IsDistinct );

			if (!isQueryOK)
				return childSource;

			var isColumnsOK =
				(allColumns && !query.Select.Columns.Any(c => IsAggregationFunction(c.Expression))) ||
				!query.Select.Columns.Any(c => CheckColumn(c, c.Expression, query, optimizeValues, optimizeColumns));

			if (!isColumnsOK)
				return childSource;

			var map = new Dictionary<ISqlExpression,ISqlExpression>(query.Select.Columns.Count);

			foreach (var c in query.Select.Columns)
				map.Add(c, c.Expression);

			var top = _selectQuery;

			while (top.ParentSelect != null)
				top = top.ParentSelect;

			((ISqlExpressionWalkable)top).Walk(false, expr =>
			{
				ISqlExpression fld;
				return map.TryGetValue(expr, out fld) ? fld : expr;
			});

	        foreach (var expr in QueryVisitor.FindAll<InList>(top).Where(p => p.Expr1 == query))
	        {
                expr.Expr1 = query.From.Tables[0];
            }

			query.From.Tables[0].Joins.AddRange(childSource.Joins);

			if (query.From.Tables[0].Alias == null)
				query.From.Tables[0].Alias = childSource.Alias;

			if (!query.Where. IsEmpty) ConcatSearchCondition(_selectQuery.Where,  query.Where);
			if (!query.Having.IsEmpty) ConcatSearchCondition(_selectQuery.Having, query.Having);

			((ISqlExpressionWalkable)top).Walk(false, expr =>
			{
				if (expr is SelectQuery)
				{
					var sql = (SelectQuery)expr;

					if (sql.ParentSelect == query)
						sql.ParentSelect = query.ParentSelect ?? _selectQuery;
				}

				return expr;
			});

			return query.From.Tables[0];
		}

		static bool IsAggregationFunction(IQueryElement expr)
		{
			if (expr is SqlFunction)
				switch (((SqlFunction)expr).Name)
				{
					case "Count"   :
					case "Average" :
					case "Min"     :
					case "Max"     :
					case "Sum"     : return true;
				}

			return false;
		}

		void OptimizeApply(TableSource tableSource, JoinedTable joinTable, bool isApplySupported, bool optimizeColumns)
		{
			var joinSource = joinTable.Table;

			foreach (var join in joinSource.Joins)
				if (join.JoinType == JoinType.CrossApply || join.JoinType == JoinType.OuterApply)
					OptimizeApply(joinSource, join, isApplySupported, optimizeColumns);

			if (isApplySupported && !joinTable.CanConvertApply)
				return;

			if (joinSource.Source.ElementType == QueryElementType.SqlQuery)
			{
				var sql   = (SelectQuery)joinSource.Source;
				var isAgg = sql.Select.Columns.Any(c => IsAggregationFunction(c.Expression));

				if (isApplySupported && (isAgg || sql.Select.TakeValue != null || sql.Select.SkipValue != null))
					return;

				var searchCondition = new List<Condition>(sql.Where.SearchCondition.Conditions);

				sql.Where.SearchCondition.Conditions.Clear();

				if (!ContainsTable(tableSource.Source, sql))
				{
					joinTable.JoinType = joinTable.JoinType == JoinType.CrossApply ? JoinType.Inner : JoinType.Left;
					joinTable.Condition.Conditions.AddRange(searchCondition);
				}
				else
				{
					sql.Where.SearchCondition.Conditions.AddRange(searchCondition);

					var table = OptimizeSubQuery(
						joinTable.Table,
						joinTable.JoinType == JoinType.Inner || joinTable.JoinType == JoinType.CrossApply,
						joinTable.JoinType == JoinType.CrossApply,
						isApplySupported,
						joinTable.JoinType == JoinType.Inner || joinTable.JoinType == JoinType.CrossApply,
						optimizeColumns);

					if (table != joinTable.Table)
					{
						var q = joinTable.Table.Source as SelectQuery;

						if (q != null && q.OrderBy.Items.Count > 0)
							foreach (var item in q.OrderBy.Items)
								_selectQuery.OrderBy.Expr(item.Expression, item.IsDescending);

						joinTable.Table = table;

						OptimizeApply(tableSource, joinTable, isApplySupported, optimizeColumns);
					}
				}
			}
			else
			{
				if (!ContainsTable(tableSource.Source, joinSource.Source))
					joinTable.JoinType = joinTable.JoinType == JoinType.CrossApply ? JoinType.Inner : JoinType.Left;
			}
		}

		static bool ContainsTable(ISqlTableSource table, IQueryElement sql)
		{
			return null != new QueryVisitor().Find(sql, e =>
				e == table ||
				e.ElementType == QueryElementType.SqlField && table == ((SqlField)e).Table ||
				e.ElementType == QueryElementType.Column   && table == ((Column)  e).Parent);
		}

		static void ConcatSearchCondition(WhereClause where1, WhereClause where2)
		{
			if (where1.IsEmpty)
			{
				where1.SearchCondition.Conditions.AddRange(where2.SearchCondition.Conditions);
			}
			else
			{
				if (where1.SearchCondition.Precedence < Precedence.LogicalConjunction)
				{
					var sc1 = new SearchCondition();

					sc1.Conditions.AddRange(where1.SearchCondition.Conditions);

					where1.SearchCondition.Conditions.Clear();
					where1.SearchCondition.Conditions.Add(new Condition(false, sc1));
				}

				if (where2.SearchCondition.Precedence < Precedence.LogicalConjunction)
				{
					var sc2 = new SearchCondition();

					sc2.Conditions.AddRange(where2.SearchCondition.Conditions);

					where1.SearchCondition.Conditions.Add(new Condition(false, sc2));
				}
				else
					where1.SearchCondition.Conditions.AddRange(where2.SearchCondition.Conditions);
			}
		}

		void OptimizeSubQueries(bool isApplySupported, bool optimizeColumns)
		{
			for (var i = 0; i < _selectQuery.From.Tables.Count; i++)
			{
				var table = OptimizeSubQuery(_selectQuery.From.Tables[i], true, false, isApplySupported, true, optimizeColumns);

				if (table != _selectQuery.From.Tables[i])
				{
					var sql = _selectQuery.From.Tables[i].Source as SelectQuery;

					if (!_selectQuery.Select.Columns.All(c => IsAggregationFunction(c.Expression)))
						if (sql != null && sql.OrderBy.Items.Count > 0)
							foreach (var item in sql.OrderBy.Items)
								_selectQuery.OrderBy.Expr(item.Expression, item.IsDescending);

					_selectQuery.From.Tables[i] = table;
				}
			}
		}

		void OptimizeApplies(bool isApplySupported, bool optimizeColumns)
		{
			foreach (var table in _selectQuery.From.Tables)
				foreach (var join in table.Joins)
					if (join.JoinType == JoinType.CrossApply || join.JoinType == JoinType.OuterApply)
						OptimizeApply(table, join, isApplySupported, optimizeColumns);
		}

		void OptimizeColumns()
		{
			((ISqlExpressionWalkable)_selectQuery.Select).Walk(false, expr =>
			{
				var query = expr as SelectQuery;
					
				if (query != null && query.From.Tables.Count == 0 && query.Select.Columns.Count == 1)
				{

				    foreach (var q in QueryVisitor.FindAll<SelectQuery>(query.Select.Columns[0].Expression).Where(q => q.ParentSelect == query))
				    {
                        q.ParentSelect = query.ParentSelect;
                    }

					return query.Select.Columns[0].Expression;
				}

				return expr;
			});
		}
	}
}
