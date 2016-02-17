﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace LinqToDB.SqlProvider
{
	using Common;

	using LinqToDB.Extensions;
	using LinqToDB.SqlQuery.QueryElements;
	using LinqToDB.SqlQuery.QueryElements.Conditions;
	using LinqToDB.SqlQuery.QueryElements.Enums;
	using LinqToDB.SqlQuery.QueryElements.Interfaces;
	using LinqToDB.SqlQuery.QueryElements.Predicates;
	using LinqToDB.SqlQuery.QueryElements.Predicates.Interfaces;
	using LinqToDB.SqlQuery.QueryElements.SqlElements;
	using LinqToDB.SqlQuery.QueryElements.SqlElements.Enums;
	using LinqToDB.SqlQuery.QueryElements.SqlElements.Interfaces;

	using Mapping;
	using SqlQuery;

	public abstract class BasicSqlBuilder : ISqlBuilder
	{
		#region Init

		protected BasicSqlBuilder(ISqlOptimizer sqlOptimizer, SqlProviderFlags sqlProviderFlags, ValueToSqlConverter valueToSqlConverter)
		{
			SqlOptimizer        = sqlOptimizer;
			SqlProviderFlags    = sqlProviderFlags;
			ValueToSqlConverter = valueToSqlConverter;
		}

		protected ISelectQuery SelectQuery;
		protected int                 Indent;
		protected Step                BuildStep;
		protected ISqlOptimizer       SqlOptimizer;
		protected SqlProviderFlags    SqlProviderFlags;
		protected ValueToSqlConverter ValueToSqlConverter;
		protected StringBuilder       StringBuilder;
		protected bool                SkipAlias;

		#endregion

		#region Support Flags

		public virtual bool IsNestedJoinSupported => true;

	    public virtual bool IsNestedJoinParenthesisRequired => false;

	    #endregion

		#region CommandCount

		public virtual int CommandCount(ISelectQuery selectQuery)
		{
			return 1;
		}

		#endregion

		#region BuildSql

		public void BuildSql(int commandNumber, ISelectQuery selectQuery, StringBuilder sb)
		{
			BuildSql(commandNumber, selectQuery, sb, 0, false);
		}

		protected virtual void BuildSql(int commandNumber, ISelectQuery selectQuery, StringBuilder sb, int indent, bool skipAlias)
		{
			SelectQuery   = selectQuery;
			StringBuilder = sb;
			Indent        = indent;
			SkipAlias     = skipAlias;

			if (commandNumber == 0)
			{
				BuildSql();

				if (selectQuery.HasUnion)
				{
					foreach (var union in selectQuery.Unions)
					{
						AppendIndent();
						sb.Append("UNION");
						if (union.IsAll) sb.Append(" ALL");
						sb.AppendLine();

						((BasicSqlBuilder)CreateSqlBuilder()).BuildSql(commandNumber, union.SelectQuery, sb, indent, skipAlias);
					}
				}
			}
			else
			{
				BuildCommand(commandNumber);
			}
		}

		protected virtual void BuildCommand(int commandNumber)
		{
		}

		#endregion

		#region Overrides

		protected virtual void BuildSqlBuilder(ISelectQuery selectQuery, int indent, bool skipAlias)
		{
			if (!SqlProviderFlags.GetIsSkipSupportedFlag(selectQuery)
				&& selectQuery.Select.SkipValue != null)
				throw new SqlException("Skip for subqueries is not supported by the '{0}' provider.", Name);

			if (!SqlProviderFlags.IsTakeSupported && selectQuery.Select.TakeValue != null)
				throw new SqlException("Take for subqueries is not supported by the '{0}' provider.", Name);

			((BasicSqlBuilder)CreateSqlBuilder()).BuildSql(0, selectQuery, StringBuilder, indent, skipAlias);
		}

		protected abstract ISqlBuilder CreateSqlBuilder();

		protected T WithStringBuilder<T>(StringBuilder sb, Func<T> func)
		{
			var current = StringBuilder;

			StringBuilder = sb;

			var ret = func();

			StringBuilder = current;

			return ret;
		}

		void WithStringBuilder(StringBuilder sb, Action func)
		{
			var current = StringBuilder;

			StringBuilder = sb;

			func();

			StringBuilder = current;
		}

		protected virtual bool ParenthesizeJoin()
		{
			return false;
		}

		protected virtual void BuildSql()
		{
			switch (SelectQuery.EQueryType)
			{
				case EQueryType.Select         : BuildSelectQuery        (); break;
				case EQueryType.Delete         : BuildDeleteQuery        (); break;
				case EQueryType.Update         : BuildUpdateQuery        (); break;
				case EQueryType.Insert         : BuildInsertQuery        (); break;
				case EQueryType.InsertOrUpdate : BuildInsertOrUpdateQuery(); break;
				case EQueryType.CreateTable    :
					if (SelectQuery.CreateTable.IsDrop)
						BuildDropTableStatement();
					else
						BuildCreateTableStatement();
					break;
				default                       : BuildUnknownQuery       (); break;
			}
		}

		protected virtual void BuildDeleteQuery()
		{
			BuildStep = Step.DeleteClause;  BuildDeleteClause ();
			BuildStep = Step.FromClause;    BuildFromClause   ();
			BuildStep = Step.WhereClause;   BuildWhereClause  ();
			BuildStep = Step.GroupByClause; BuildGroupByClause();
			BuildStep = Step.HavingClause;  BuildHavingClause ();
			BuildStep = Step.OrderByClause; BuildOrderByClause();
			BuildStep = Step.OffsetLimit;   BuildOffsetLimit  ();
		}

		protected virtual void BuildUpdateQuery()
		{
			BuildStep = Step.UpdateClause;  BuildUpdateClause ();
			BuildStep = Step.FromClause;    BuildFromClause   ();
			BuildStep = Step.WhereClause;   BuildWhereClause  ();
			BuildStep = Step.GroupByClause; BuildGroupByClause();
			BuildStep = Step.HavingClause;  BuildHavingClause ();
			BuildStep = Step.OrderByClause; BuildOrderByClause();
			BuildStep = Step.OffsetLimit;   BuildOffsetLimit  ();
		}

		protected virtual void BuildSelectQuery()
		{
			BuildStep = Step.SelectClause;  BuildSelectClause ();
			BuildStep = Step.FromClause;    BuildFromClause   ();
			BuildStep = Step.WhereClause;   BuildWhereClause  ();
			BuildStep = Step.GroupByClause; BuildGroupByClause();
			BuildStep = Step.HavingClause;  BuildHavingClause ();
			BuildStep = Step.OrderByClause; BuildOrderByClause();
			BuildStep = Step.OffsetLimit;   BuildOffsetLimit  ();
		}

		protected virtual void BuildInsertQuery()
		{
			BuildStep = Step.InsertClause; BuildInsertClause();

			if (SelectQuery.EQueryType == EQueryType.Insert && SelectQuery.From.Tables.Count != 0)
			{
				BuildStep = Step.SelectClause;  BuildSelectClause ();
				BuildStep = Step.FromClause;    BuildFromClause   ();
				BuildStep = Step.WhereClause;   BuildWhereClause  ();
				BuildStep = Step.GroupByClause; BuildGroupByClause();
				BuildStep = Step.HavingClause;  BuildHavingClause ();
				BuildStep = Step.OrderByClause; BuildOrderByClause();
				BuildStep = Step.OffsetLimit;   BuildOffsetLimit  ();
			}

			if (SelectQuery.Insert.WithIdentity)
				BuildGetIdentity();
		}

		protected virtual void BuildUnknownQuery()
		{
			throw new SqlException("Unknown query type '{0}'.", SelectQuery.EQueryType);
		}

		public virtual StringBuilder BuildTableName(StringBuilder sb, string database, string owner, string table)
		{
			if (database != null)
			{
				if (owner == null)  sb.Append(database).Append("..");
				else                sb.Append(database).Append(".").Append(owner).Append(".");
			}
			else if (owner != null) sb.Append(owner).Append(".");

			return sb.Append(table);
		}

		public virtual object Convert(object value, ConvertType convertType)
		{
			return value;
		}

		#endregion

		#region Build Select

		protected virtual void BuildSelectClause()
		{
			AppendIndent();
			StringBuilder.Append("SELECT");

			if (SelectQuery.Select.IsDistinct)
				StringBuilder.Append(" DISTINCT");

			BuildSkipFirst();

			StringBuilder.AppendLine();
			BuildColumns();
		}

		protected virtual IEnumerable<IColumn> GetSelectedColumns()
		{
			return SelectQuery.Select.Columns;
		}

		protected virtual void BuildColumns()
		{
			Indent++;

			var first = true;

			foreach (var col in GetSelectedColumns())
			{
				if (!first)
					StringBuilder.Append(',').AppendLine();
				first = false;

				var addAlias = true;

				AppendIndent();
				BuildColumnExpression(col.Expression, col.Alias, ref addAlias);

				if (!SkipAlias && addAlias && col.Alias != null)
					StringBuilder.Append(" as ").Append(Convert(col.Alias, ConvertType.NameToQueryFieldAlias));
			}

			if (first)
				AppendIndent().Append("*");

			Indent--;

			StringBuilder.AppendLine();
		}

		protected virtual void BuildColumnExpression(IQueryExpression expr, string alias, ref bool addAlias)
		{
			BuildExpression(expr, true, true, alias, ref addAlias, true);
		}

		#endregion

		#region Build Delete

		protected virtual void BuildDeleteClause()
		{
			AppendIndent();
			StringBuilder.Append("DELETE");
			BuildSkipFirst();
			StringBuilder.Append(" ");
		}

		#endregion

		#region Build Update

		protected virtual void BuildUpdateClause()
		{
			BuildUpdateTable();
			BuildUpdateSet  ();
		}

		protected virtual void BuildUpdateTable()
		{
			AppendIndent().Append("UPDATE");

			BuildSkipFirst();

			StringBuilder.AppendLine().Append('\t');
			BuildUpdateTableName();
			StringBuilder.AppendLine();
		}

		protected virtual void BuildUpdateTableName()
		{
			if (SelectQuery.Update.Table != null && SelectQuery.Update.Table != SelectQuery.From.Tables.First.Value.Source)
				BuildPhysicalTable(SelectQuery.Update.Table, null);
			else
				BuildTableName(SelectQuery.From.Tables.First.Value, true, true);
		}

		protected virtual void BuildUpdateSet()
		{
			AppendIndent()
				.AppendLine("SET");

			Indent++;

			var first = true;

			foreach (var expr in SelectQuery.Update.Items)
			{
				if (!first)
					StringBuilder.Append(',').AppendLine();
				first = false;

				AppendIndent();

				BuildExpression(expr.Column, SqlProviderFlags.IsUpdateSetTableAliasSupported, true, false);
				StringBuilder.Append(" = ");

				var addAlias = false;

				BuildColumnExpression(expr.Expression, null, ref addAlias);
			}

			Indent--;

			StringBuilder.AppendLine();
		}

		#endregion

		#region Build Insert

		protected void BuildInsertClause()
		{
			BuildInsertClause("INSERT INTO ", true);
		}

		protected virtual void BuildEmptyInsert()
		{
			StringBuilder.AppendLine("DEFAULT VALUES");
		}

		protected virtual void BuildOutputSubclause()
		{
		}

		protected virtual void BuildInsertClause(string insertText, bool appendTableName)
		{
			AppendIndent().Append(insertText);

			if (appendTableName)
				BuildPhysicalTable(SelectQuery.Insert.Into, null);

			if (SelectQuery.Insert.Items.Count == 0)
			{
				StringBuilder.Append(' ');

				BuildOutputSubclause();

				BuildEmptyInsert();
			}
			else
			{
				StringBuilder.AppendLine();

				AppendIndent().AppendLine("(");

				Indent++;

				var first = true;

				foreach (var expr in SelectQuery.Insert.Items)
				{
					if (!first)
						StringBuilder.Append(',').AppendLine();
					first = false;

					AppendIndent();
					BuildExpression(expr.Column, false, true);
				}

				Indent--;

				StringBuilder.AppendLine();
				AppendIndent().AppendLine(")");

				BuildOutputSubclause();

				if (SelectQuery.EQueryType == EQueryType.InsertOrUpdate || SelectQuery.From.Tables.Count == 0)
				{
					AppendIndent().AppendLine("VALUES");
					AppendIndent().AppendLine("(");

					Indent++;

					first = true;

					foreach (var expr in SelectQuery.Insert.Items)
					{
						if (!first)
							StringBuilder.Append(',').AppendLine();
						first = false;

						AppendIndent();
						BuildExpression(expr.Expression);
					}

					Indent--;

					StringBuilder.AppendLine();
					AppendIndent().AppendLine(")");
				}
			}
		}

		protected virtual void BuildGetIdentity()
		{
			//throw new SqlException("Insert with identity is not supported by the '{0}' sql provider.", Name);
		}

		#endregion

		#region Build InsertOrUpdate

		protected virtual void BuildInsertOrUpdateQuery()
		{
			throw new SqlException("InsertOrUpdate query type is not supported by {0} provider.", Name);
		}

		protected void BuildInsertOrUpdateQueryAsMerge(string fromDummyTable)
		{
			var table       = SelectQuery.Insert.Into;
			var targetAlias = Convert(SelectQuery.From.Tables.First.Value.Alias, ConvertType.NameToQueryTableAlias).ToString();
			var sourceAlias = Convert(GetTempAliases(1, "s")[0],        ConvertType.NameToQueryTableAlias).ToString();
			var keys        = SelectQuery.Update.Keys;

			AppendIndent().Append("MERGE INTO ");
			BuildPhysicalTable(table, null);
			StringBuilder.Append(' ').AppendLine(targetAlias);

			AppendIndent().Append("USING (SELECT ");

            keys.ForEach(
                node =>
                {
                    BuildExpression(node.Value.Expression, false, false);
                    StringBuilder.Append(" AS ");
                    BuildExpression(node.Value.Column, false, false);

                    if (node.Next != null)
                        StringBuilder.Append(", ");
                });

			if (!string.IsNullOrEmpty(fromDummyTable))
				StringBuilder.Append(' ').Append(fromDummyTable);

			StringBuilder.Append(") ").Append(sourceAlias).AppendLine(" ON");

			AppendIndent().AppendLine("(");

			Indent++;

		    keys.ForEach(
		        node =>
		        {
		            var key = node.Value;
		            AppendIndent();

		            StringBuilder.Append(targetAlias).Append('.');
		            BuildExpression(key.Column, false, false);

		            StringBuilder.Append(" = ").Append(sourceAlias).Append('.');
		            BuildExpression(key.Column, false, false);

		            if (node.Next != null)
		                StringBuilder.Append(" AND");

		            StringBuilder.AppendLine();
		        });

			Indent--;

			AppendIndent().AppendLine(")");
			AppendIndent().AppendLine("WHEN MATCHED THEN");

			Indent++;
			AppendIndent().AppendLine("UPDATE ");
			BuildUpdateSet();
			Indent--;

			AppendIndent().AppendLine("WHEN NOT MATCHED THEN");

			Indent++;
			BuildInsertClause("INSERT", false);
			Indent--;

			while (_endLine.Contains(StringBuilder[StringBuilder.Length - 1]))
				StringBuilder.Length--;
		}

		static readonly char[] _endLine = { ' ', '\r', '\n' };

		protected void BuildInsertOrUpdateQueryAsUpdateInsert()
		{
			AppendIndent().AppendLine("BEGIN TRAN").AppendLine();

			BuildUpdateQuery();

			AppendIndent().AppendLine("WHERE");

			var alias = Convert(SelectQuery.From.Tables.First.Value.Alias, ConvertType.NameToQueryTableAlias).ToString();
			var exprs = SelectQuery.Update.Keys;

			Indent++;

            exprs.ForEach(
                node =>
                {
                    var expr = node.Value;

                    AppendIndent();

                    StringBuilder.Append(alias).Append('.');
                    BuildExpression(expr.Column, false, false);

                    StringBuilder.Append(" = ");
                    BuildExpression(Precedence.Comparison, expr.Expression);

                    if (node.Next != null)
                        StringBuilder.Append(" AND");

                    StringBuilder.AppendLine();
                });

			Indent--;

			StringBuilder.AppendLine();
			AppendIndent().AppendLine("IF @@ROWCOUNT = 0");
			AppendIndent().AppendLine("BEGIN");

			Indent++;

			BuildInsertQuery();

			Indent--;

			AppendIndent().AppendLine("END");

			StringBuilder.AppendLine();
			AppendIndent().AppendLine("COMMIT");
		}

		#endregion

		#region Build DDL

		protected virtual void BuildDropTableStatement()
		{
			var table = SelectQuery.CreateTable.Table;

			AppendIndent().Append("DROP TABLE ");
			BuildPhysicalTable(table, null);
			StringBuilder.AppendLine();
		}

		protected virtual void BuildStartCreateTableStatement(ICreateTableStatement createTable)
		{
			if (createTable.StatementHeader == null)
			{
				AppendIndent().Append("CREATE TABLE ");
				BuildPhysicalTable(createTable.Table, null);
			}
			else
			{
				var name = WithStringBuilder(
					new StringBuilder(),
					() =>
					{
						BuildPhysicalTable(createTable.Table, null);
						return StringBuilder.ToString();
					});

				AppendIndent().AppendFormat(createTable.StatementHeader, name);
			}
		}

		protected virtual void BuildEndCreateTableStatement(ICreateTableStatement createTable)
		{
			if (createTable.StatementFooter != null)
				AppendIndent().Append(createTable.StatementFooter);
		}

		class CreateFieldInfo
		{
			public ISqlField     Field;
			public StringBuilder StringBuilder;
			public string        Name;
			public string        Type;
			public string        Identity;
			public string        Null;
		}

		protected virtual void BuildCreateTableStatement()
		{
			var table = SelectQuery.CreateTable.Table;

			BuildStartCreateTableStatement(SelectQuery.CreateTable);

			StringBuilder.AppendLine();
			AppendIndent().Append("(");
			Indent++;

			var fields = table.Fields.Select(f => new CreateFieldInfo { Field = f.Value, StringBuilder = new StringBuilder() }).ToList();
			var maxlen = 0;

			Action<bool> appendToMax = addCreateFormat =>
			{
				foreach (var field in fields)
					if (addCreateFormat || field.Field.CreateFormat == null)
						while (maxlen > field.StringBuilder.Length)
							field.StringBuilder.Append(' ');
			};

			var isAnyCreateFormat = false;

			// Build field name.
			//
			foreach (var field in fields)
			{
				field.StringBuilder.Append(Convert(field.Field.PhysicalName, ConvertType.NameToQueryField));

				if (maxlen < field.StringBuilder.Length)
					maxlen = field.StringBuilder.Length;

				if (field.Field.CreateFormat != null)
					isAnyCreateFormat = true;
			}

			appendToMax(true);

			if (isAnyCreateFormat)
				foreach (var field in fields)
					if (field.Field.CreateFormat != null)
						field.Name = field.StringBuilder.ToString() + ' ';

			// Build field type.
			//
			foreach (var field in fields)
			{
				field.StringBuilder.Append(' ');

				if (!string.IsNullOrEmpty(field.Field.DbType))
					field.StringBuilder.Append(field.Field.DbType);
				else
				{
					var sb = StringBuilder;
					StringBuilder = field.StringBuilder;

					BuildCreateTableFieldType(field.Field);

					StringBuilder = sb;
				}

				if (maxlen < field.StringBuilder.Length)
					maxlen = field.StringBuilder.Length;
			}

			appendToMax(true);

			if (isAnyCreateFormat)
			{
				foreach (var field in fields)
				{
					if (field.Field.CreateFormat != null)
					{
						var sb = field.StringBuilder;

						field.Type = sb.ToString().Substring(field.Name.Length) + ' ';
						sb.Length = 0;
					}
				}
			}

			var hasIdentity = fields.Any(f => f.Field.IsIdentity);

			// Build identity attribute.
			//
			if (hasIdentity)
			{
				foreach (var field in fields)
				{
					if (field.Field.CreateFormat == null)
						field.StringBuilder.Append(' ');

					if (field.Field.IsIdentity)
						WithStringBuilder(field.StringBuilder, () => BuildCreateTableIdentityAttribute1(field.Field));

					if (field.Field.CreateFormat != null)
					{
						field.Identity = field.StringBuilder.ToString();

						if (field.Identity.Length != 0)
							field.Identity += ' ';

						field.StringBuilder.Length = 0;
					}
					else if (maxlen < field.StringBuilder.Length)
					{
						maxlen = field.StringBuilder.Length;
					}
				}

				appendToMax(false);
			}

			// Build nullable attribute.
			//
			foreach (var field in fields)
			{
				if (field.Field.CreateFormat == null)
					field.StringBuilder.Append(' ');

				WithStringBuilder(
					field.StringBuilder,
					() => BuildCreateTableNullAttribute(field.Field, SelectQuery.CreateTable.EDefaulNullable));

				if (field.Field.CreateFormat != null)
				{
					field.Null = field.StringBuilder.ToString() + ' ';
					field.StringBuilder.Length = 0;
				}
				else if (maxlen < field.StringBuilder.Length)
				{
					maxlen = field.StringBuilder.Length;
				}
			}

			appendToMax(false);

			// Build identity attribute.
			//
			if (hasIdentity)
			{
				foreach (var field in fields)
				{
					if (field.Field.CreateFormat == null)
						field.StringBuilder.Append(' ');

					if (field.Field.IsIdentity)
						WithStringBuilder(field.StringBuilder, () => BuildCreateTableIdentityAttribute2(field.Field));

					if (field.Field.CreateFormat != null)
					{
						if (field.Field.CreateFormat != null && field.Identity.Length == 0)
						{
							field.Identity = field.StringBuilder.ToString() + ' ';
							field.StringBuilder.Length = 0;
						}
					}
					else if (maxlen < field.StringBuilder.Length)
					{
						maxlen = field.StringBuilder.Length;
					}
				}

				appendToMax(false);
			}

			// Build fields.
			//
			for (var i = 0; i < fields.Count; i++)
			{
				while (fields[i].StringBuilder.Length > 0 && fields[i].StringBuilder[fields[i].StringBuilder.Length - 1] == ' ')
					fields[i].StringBuilder.Length--;

				StringBuilder.AppendLine(i == 0 ? "" : ",");
				AppendIndent();

				var field = fields[i];

				if (field.Field.CreateFormat != null)
				{
					StringBuilder.AppendFormat(field.Field.CreateFormat, field.Name, field.Type, field.Null, field.Identity);

					while (StringBuilder.Length > 0 && StringBuilder[StringBuilder.Length - 1] == ' ')
						StringBuilder.Length--;
				}
				else
				{
					StringBuilder.Append(field.StringBuilder);
				}
			}

			var pk =
			(
				from f in fields
				where f.Field.IsPrimaryKey
				orderby f.Field.PrimaryKeyOrder
				select f
			).ToList();

			if (pk.Count > 0)
			{
				StringBuilder.AppendLine(",").AppendLine();

				BuildCreateTablePrimaryKey(
					Convert("PK_" + SelectQuery.CreateTable.Table.PhysicalName, ConvertType.NameToQueryTable).ToString(),
					pk.Select(f => Convert(f.Field.PhysicalName, ConvertType.NameToQueryField).ToString()));
			}

			Indent--;
			StringBuilder.AppendLine();
			AppendIndent().AppendLine(")");

			BuildEndCreateTableStatement(SelectQuery.CreateTable);
		}

		protected virtual void BuildCreateTableFieldType(ISqlField field)
		{
			BuildDataType(new SqlDataType(
				field.DataType,
				field.SystemType,
				field.Length,
				field.Precision,
				field.Scale),
				createDbType : true);
		}

		protected virtual void BuildCreateTableNullAttribute(ISqlField field, EDefaulNullable eDefaulNullable)
		{
			if (eDefaulNullable == EDefaulNullable.Null && field.Nullable)
				return;

			if (eDefaulNullable == EDefaulNullable.NotNull && !field.Nullable)
				return;

			StringBuilder.Append(field.Nullable ? "    NULL" : "NOT NULL");
		}

		protected virtual void BuildCreateTableIdentityAttribute1(ISqlField field)
		{
		}

		protected virtual void BuildCreateTableIdentityAttribute2(ISqlField field)
		{
		}

		protected virtual void BuildCreateTablePrimaryKey(string pkName, IEnumerable<string> fieldNames)
		{
			AppendIndent();
			StringBuilder.Append("CONSTRAINT ").Append(pkName).Append(" PRIMARY KEY (");
			StringBuilder.Append(fieldNames.Aggregate((f1,f2) => f1 + ", " + f2));
			StringBuilder.Append(")");
		}

		#endregion

		#region Build From

		protected virtual void BuildFromClause()
		{
			if (SelectQuery.From.Tables.Count == 0)
				return;

			AppendIndent();

			StringBuilder.Append("FROM").AppendLine();

			Indent++;
			AppendIndent();

			var first = true;

		    foreach (var ts in SelectQuery.From.Tables)
		    {
		        if (!first)
		        {
		            StringBuilder.AppendLine(",");
		            AppendIndent();
		        }

		        first = false;

		        int[] jn = { ParenthesizeJoin()
		                         ? ts.GetJoinNumber()
		                         : 0 };

		        if (jn[0] > 0)
		        {
		            jn[0]--;
		            for (var i = 0; i < jn[0]; i++)
		                StringBuilder.Append("(");
		        }

		        BuildTableName(ts, true, true);

		        ts.Joins.ForEach(node => BuildJoinTable(node.Value, ref jn[0]));
		    }

		    Indent--;

			StringBuilder.AppendLine();
		}

		protected void BuildPhysicalTable(ISqlTableSource table, string alias)
		{
			switch (table.ElementType)
			{
				case EQueryElementType.SqlTable    :
				case EQueryElementType.TableSource :
					StringBuilder.Append(GetPhysicalTableName(table, alias));
					break;

				case EQueryElementType.SqlQuery    :
					StringBuilder.Append("(").AppendLine();
					BuildSqlBuilder((ISelectQuery)table, Indent + 1, false);
					AppendIndent().Append(")");

					break;

				default:
					throw new InvalidOperationException();
			}
		}

		protected void BuildTableName(ITableSource ts, bool buildName, bool buildAlias)
		{
			if (buildName)
			{
				var alias = GetTableAlias(ts);
				BuildPhysicalTable(ts.Source, alias);
			}

			if (buildAlias)
			{
				if (ts.SqlTableType != ESqlTableType.Expression)
				{
					var alias = GetTableAlias(ts);

					if (!string.IsNullOrEmpty(alias))
					{
						if (buildName)
							StringBuilder.Append(" ");
						StringBuilder.Append(Convert(alias, ConvertType.NameToQueryTableAlias));
					}
				}
			}
		}

		void BuildJoinTable(IJoinedTable join, ref int joinCounter)
		{
			StringBuilder.AppendLine();
			Indent++;
			AppendIndent();

			var buildOn = BuildJoinType(join);

			if (IsNestedJoinParenthesisRequired && join.Table.Joins.Count != 0)
				StringBuilder.Append('(');

			BuildTableName(join.Table, true, true);

			if (IsNestedJoinSupported && join.Table.Joins.Count != 0)
			{
				foreach (var jt in join.Table.Joins)
					BuildJoinTable(jt, ref joinCounter);

				if (IsNestedJoinParenthesisRequired && join.Table.Joins.Count != 0)
					StringBuilder.Append(')');

				if (buildOn)
				{
					StringBuilder.AppendLine();
					AppendIndent();
					StringBuilder.Append("ON ");
				}
			}
			else if (buildOn)
				StringBuilder.Append(" ON ");

			if (buildOn)
			{
				if (join.Condition.Conditions.Count != 0)
					BuildSearchCondition(Precedence.Unknown, join.Condition);
				else
					StringBuilder.Append("1=1");
			}

			if (joinCounter > 0)
			{
				joinCounter--;
				StringBuilder.Append(")");
			}

			if (!IsNestedJoinSupported)
				foreach (var jt in join.Table.Joins)
					BuildJoinTable(jt, ref joinCounter);

			Indent--;
		}

		protected virtual bool BuildJoinType(IJoinedTable join)
		{
			switch (join.JoinType)
			{
				case EJoinType.Inner      : StringBuilder.Append("INNER JOIN ");  return true;
				case EJoinType.Left       : StringBuilder.Append("LEFT JOIN ");   return true;
				case EJoinType.CrossApply : StringBuilder.Append("CROSS APPLY "); return false;
				case EJoinType.OuterApply : StringBuilder.Append("OUTER APPLY "); return false;
				default: throw new InvalidOperationException();
			}
		}

		#endregion

		#region Where Clause

		protected virtual bool BuildWhere()
		{
			return SelectQuery.Where.SearchCondition.Conditions.Count != 0;
		}

		protected virtual void BuildWhereClause()
		{
			if (!BuildWhere())
				return;

			AppendIndent();

			StringBuilder.Append("WHERE").AppendLine();

			Indent++;
			AppendIndent();
			BuildWhereSearchCondition(SelectQuery.Where.SearchCondition);
			Indent--;

			StringBuilder.AppendLine();
		}

		#endregion

		#region GroupBy Clause

		protected virtual void BuildGroupByClause()
		{
			if (SelectQuery.GroupBy.Items.Count == 0)
				return;

			var items = SelectQuery.GroupBy.Items.Where(i => !(i is ISqlValue || i is ISqlParameter)).ToList();

			if (items.Count == 0)
				return;

			AppendIndent();

			StringBuilder.Append("GROUP BY").AppendLine();

			Indent++;

			for (var i = 0; i < items.Count; i++)
			{
				AppendIndent();

				BuildExpression(items[i]);

				if (i + 1 < items.Count)
					StringBuilder.Append(',');

				StringBuilder.AppendLine();
			}

			Indent--;
		}

		#endregion

		#region Having Clause

		protected virtual void BuildHavingClause()
		{
			if (SelectQuery.Having.SearchCondition.Conditions.Count == 0)
				return;

			AppendIndent();

			StringBuilder.Append("HAVING").AppendLine();

			Indent++;
			AppendIndent();
			BuildWhereSearchCondition(SelectQuery.Having.SearchCondition);
			Indent--;

			StringBuilder.AppendLine();
		}

		#endregion

		#region OrderBy Clause

		protected virtual void BuildOrderByClause()
		{
			if (SelectQuery.OrderBy.Items.Count == 0)
				return;

			AppendIndent();

			StringBuilder.Append("ORDER BY").AppendLine();

			Indent++;

			for (var i = 0; i < SelectQuery.OrderBy.Items.Count; i++)
			{
				AppendIndent();

				var item = SelectQuery.OrderBy.Items[i];

				BuildExpression(item.Expression);

				if (item.IsDescending)
					StringBuilder.Append(" DESC");

				if (i + 1 < SelectQuery.OrderBy.Items.Count)
					StringBuilder.Append(',');

				StringBuilder.AppendLine();
			}

			Indent--;
		}

		#endregion

		#region Skip/Take

		protected virtual bool   SkipFirst => true;

	    protected virtual string SkipFormat => null;

	    protected virtual string FirstFormat => null;

	    protected virtual string LimitFormat => null;

	    protected virtual string OffsetFormat => null;

	    protected virtual bool   OffsetFirst => false;

	    protected bool NeedSkip => SelectQuery.Select.SkipValue != null && SqlProviderFlags.GetIsSkipSupportedFlag(SelectQuery);

	    protected bool NeedTake => SelectQuery.Select.TakeValue != null && SqlProviderFlags.IsTakeSupported;

	    protected virtual void BuildSkipFirst()
		{
			if (SkipFirst && NeedSkip && SkipFormat != null)
				StringBuilder.Append(' ').AppendFormat(
					SkipFormat, WithStringBuilder(new StringBuilder(), () => BuildExpression(SelectQuery.Select.SkipValue)));

			if (NeedTake && FirstFormat != null)
				StringBuilder.Append(' ').AppendFormat(
					FirstFormat, WithStringBuilder(new StringBuilder(), () => BuildExpression(SelectQuery.Select.TakeValue)));

			if (!SkipFirst && NeedSkip && SkipFormat != null)
				StringBuilder.Append(' ').AppendFormat(
					SkipFormat, WithStringBuilder(new StringBuilder(), () => BuildExpression(SelectQuery.Select.SkipValue)));
		}

		protected virtual void BuildOffsetLimit()
		{
			var doSkip = NeedSkip && OffsetFormat != null;
			var doTake = NeedTake && LimitFormat  != null;

			if (doSkip || doTake)
			{
				AppendIndent();

				if (doSkip && OffsetFirst)
				{
					StringBuilder.AppendFormat(
						OffsetFormat, WithStringBuilder(new StringBuilder(), () => BuildExpression(SelectQuery.Select.SkipValue)));

					if (doTake)
						StringBuilder.Append(' ');
				}

				if (doTake)
				{
					StringBuilder.AppendFormat(
						LimitFormat, WithStringBuilder(new StringBuilder(), () => BuildExpression(SelectQuery.Select.TakeValue)));

					if (doSkip)
						StringBuilder.Append(' ');
				}

				if (doSkip && !OffsetFirst)
					StringBuilder.AppendFormat(
						OffsetFormat, WithStringBuilder(new StringBuilder(), () => BuildExpression(SelectQuery.Select.SkipValue)));

				StringBuilder.AppendLine();
			}
		}

		#endregion

		#region Builders

		#region BuildSearchCondition

		protected virtual void BuildWhereSearchCondition(ISearchCondition condition)
		{
			BuildSearchCondition(Precedence.Unknown, condition);
		}

		protected virtual void BuildSearchCondition(ISearchCondition condition)
		{
			var isOr = (bool?)null;
			var len  = StringBuilder.Length;
			var parentPrecedence = condition.Precedence + 1;

			foreach (var cond in condition.Conditions)
			{
				if (isOr != null)
				{
					StringBuilder.Append(isOr.Value ? " OR" : " AND");

					if (condition.Conditions.Count < 4 && StringBuilder.Length - len < 50 || condition != SelectQuery.Where.SearchCondition)
					{
						StringBuilder.Append(' ');
					}
					else
					{
						StringBuilder.AppendLine();
						AppendIndent();
						len = StringBuilder.Length;
					}
				}

				if (cond.IsNot)
					StringBuilder.Append("NOT ");

				var precedence = GetPrecedence(cond.Predicate);

				BuildPredicate(cond.IsNot ? Precedence.LogicalNegation : parentPrecedence, precedence, cond.Predicate);

				isOr = cond.IsOr;
			}
		}

		protected virtual void BuildSearchCondition(int parentPrecedence, ISearchCondition condition)
		{
			var wrap = Wrap(GetPrecedence(condition as IQueryExpression), parentPrecedence);

			if (wrap) StringBuilder.Append('(');
			BuildSearchCondition(condition);
			if (wrap) StringBuilder.Append(')');
		}

		#endregion

		#region BuildPredicate

		protected virtual void BuildPredicate(ISqlPredicate predicate)
		{
			switch (predicate.ElementType)
			{
				case EQueryElementType.ExprExprPredicate :
					{
						var expr = (IExprExpr)predicate;

						switch (expr.EOperator)
						{
							case EOperator.Equal :
							case EOperator.NotEqual :
								{
									IQueryExpression e = null;

								    var valueContainer = expr.Expr1 as IValueContainer;
								    if (valueContainer != null && valueContainer.Value == null)
										e = expr.Expr2;
									else
								    {
								        var container = expr.Expr2 as IValueContainer;
								        if (container != null && container.Value == null)
								            e = expr.Expr1;
								    }

								    if (e != null)
									{
										BuildExpression(GetPrecedence(expr), e);
										StringBuilder.Append(expr.EOperator == EOperator.Equal ? " IS NULL" : " IS NOT NULL");
										return;
									}

									break;
								}
						}

						BuildExpression(GetPrecedence(expr), expr.Expr1);

						switch (expr.EOperator)
						{
							case EOperator.Equal          : StringBuilder.Append(" = ");  break;
							case EOperator.NotEqual       : StringBuilder.Append(" <> "); break;
							case EOperator.Greater        : StringBuilder.Append(" > ");  break;
							case EOperator.GreaterOrEqual : StringBuilder.Append(" >= "); break;
							case EOperator.NotGreater     : StringBuilder.Append(" !> "); break;
							case EOperator.Less           : StringBuilder.Append(" < ");  break;
							case EOperator.LessOrEqual    : StringBuilder.Append(" <= "); break;
							case EOperator.NotLess        : StringBuilder.Append(" !< "); break;
						}

						BuildExpression(GetPrecedence(expr), expr.Expr2);
					}

					break;

				case EQueryElementType.LikePredicate :
					BuildLikePredicate((ILike)predicate);
					break;

				case EQueryElementType.BetweenPredicate :
					{
						var p = (Between)predicate;
						BuildExpression(GetPrecedence(p), p.Expr1);
						if (p.IsNot) StringBuilder.Append(" NOT");
						StringBuilder.Append(" BETWEEN ");
						BuildExpression(GetPrecedence(p), p.Expr2);
						StringBuilder.Append(" AND ");
						BuildExpression(GetPrecedence(p), p.Expr3);
					}

					break;

				case EQueryElementType.IsNullPredicate :
					{
						var p = (IIsNull)predicate;
						BuildExpression(GetPrecedence(p), p.Expr1);
						StringBuilder.Append(p.IsNot ? " IS NOT NULL" : " IS NULL");
					}

					break;

				case EQueryElementType.InSubQueryPredicate :
					{
						var p = (IInSubQuery)predicate;
						BuildExpression(GetPrecedence(p), p.Expr1);
						StringBuilder.Append(p.IsNot ? " NOT IN " : " IN ");
						BuildExpression(GetPrecedence(p), p.SubQuery);
					}

					break;

				case EQueryElementType.InListPredicate :
					BuildInListPredicate(predicate);
					break;

				case EQueryElementType.FuncLikePredicate :
					{
						var f = (IFuncLike)predicate;
						BuildExpression(f.Function.Precedence, f.Function);
					}

					break;

				case EQueryElementType.SearchCondition :
					BuildSearchCondition(predicate.Precedence, (ISearchCondition)predicate);
					break;

				case EQueryElementType.NotExprPredicate :
					{
						var p = (INotExpr)predicate;

						if (p.IsNot)
							StringBuilder.Append("NOT ");

						BuildExpression(p.IsNot ? Precedence.LogicalNegation : GetPrecedence(p), p.Expr1);
					}

					break;

				case EQueryElementType.ExprPredicate :
					{
						var p = (IExpr)predicate;

					    var sqlValue = p.Expr1 as ISqlValue;
					    var value = sqlValue?.Value;

					    if (value is bool)
					    {
					        StringBuilder.Append((bool)value ? "1 = 1" : "1 = 0");
					        return;
					    }

					    BuildExpression(GetPrecedence(p), p.Expr1);
					}

					break;

				default :
					throw new InvalidOperationException();
			}
		}

		static ISqlField GetUnderlayingField(IQueryExpression expr)
		{
			switch (expr.ElementType)
			{
				case EQueryElementType.SqlField: return (ISqlField)expr;
				case EQueryElementType.Column  : return GetUnderlayingField(((IColumn)expr).Expression);
			}

			throw new InvalidOperationException();
		}

		void BuildInListPredicate(ISqlPredicate predicate)
		{
			var p = (IInList)predicate;

			if (p.Values == null || p.Values.Count == 0)
			{
				BuildPredicate(new Expr(new SqlValue(false)));
			}
			else
			{
				ICollection values = p.Values;

			    var sqlParameter = p.Values[0] as ISqlParameter;
			    if (p.Values.Count == 1 && sqlParameter != null &&
					!(p.Expr1.SystemType == typeof(string) && sqlParameter.Value is string))
				{
				    if (sqlParameter.Value == null)
					{
						BuildPredicate(new Expr(new SqlValue(false)));
						return;
					}

				    var enumerable = sqlParameter.Value as IEnumerable;
				    if (enumerable != null)
					{
						var items = enumerable;

					    var tableSource = p.Expr1 as ISqlTableSource;
					    if (tableSource != null)
						{
							var firstValue = true;
							var keys       = tableSource.GetKeys(true);

							if (keys == null || keys.Count == 0)
								throw new SqlException("Cannot create IN expression.");

							if (keys.Count == 1)
							{
								foreach (var item in items)
								{
									if (firstValue)
									{
										firstValue = false;
										BuildExpression(GetPrecedence(p), keys[0]);
										StringBuilder.Append(p.IsNot ? " NOT IN (" : " IN (");
									}

									var field = GetUnderlayingField(keys[0]);
									var value = field.ColumnDescriptor.MemberAccessor.GetValue(item);

								    var queryExpression = value as IQueryExpression;
								    if (queryExpression != null)
										BuildExpression(queryExpression);
									else
										BuildValue(
											new SqlDataType(
												field.DataType,
												field.SystemType,
												field.Length,
												field.Precision,
												field.Scale),
											value);

									StringBuilder.Append(", ");
								}
							}
							else
							{
								var len = StringBuilder.Length;
								var rem = 1;

								foreach (var item in items)
								{
									if (firstValue)
									{
										firstValue = false;
										StringBuilder.Append('(');
									}

									foreach (var key in keys)
									{
										var field = GetUnderlayingField(key);
										var value = field.ColumnDescriptor.MemberAccessor.GetValue(item);

										BuildExpression(GetPrecedence(p), key);

										if (value == null)
										{
											StringBuilder.Append(" IS NULL");
										}
										else
										{
											StringBuilder.Append(" = ");
											BuildValue(
												new SqlDataType(
													field.DataType,
													field.SystemType,
													field.Length,
													field.Precision,
													field.Scale),
												value);
										}

										StringBuilder.Append(" AND ");
									}

									StringBuilder.Remove(StringBuilder.Length - 4, 4).Append("OR ");

									if (StringBuilder.Length - len >= 50)
									{
										StringBuilder.AppendLine();
										AppendIndent();
										StringBuilder.Append(' ');
										len = StringBuilder.Length;
										rem = 5 + Indent;
									}
								}

								if (!firstValue)
									StringBuilder.Remove(StringBuilder.Length - rem, rem);
							}

							if (firstValue)
								BuildPredicate(new Expr(new SqlValue(p.IsNot)));
							else
								StringBuilder.Remove(StringBuilder.Length - 2, 2).Append(')');
						}
						else
						{
							BuildInListValues(p, items);
						}

						return;
					}
				}

				BuildInListValues(p, values);
			}
		}

		void BuildInListValues(IInList predicate, IEnumerable values)
		{
			var firstValue = true;
			var len        = StringBuilder.Length;
			var hasNull    = false;
			var count      = 0;
			var longList   = false;

			SqlDataType sqlDataType = null;

			foreach (var value in values)
			{
				if (count++ >= SqlProviderFlags.MaxInListValuesCount)
				{
					count    = 1;
					longList = true;

					// start building next bucked
					firstValue = true;
					StringBuilder.Remove(StringBuilder.Length - 2, 2).Append(')');
					StringBuilder.Append(" OR ");
				}

				var val = value;

			    var valueContainer = val as IValueContainer;
			    if (valueContainer != null)
					val = valueContainer.Value;

				if (val == null)
				{
					hasNull = true;
					continue;
				}

				if (firstValue)
				{
					firstValue = false;
					BuildExpression(GetPrecedence(predicate), predicate.Expr1);
					StringBuilder.Append(predicate.IsNot ? " NOT IN (" : " IN (");

					switch (predicate.Expr1.ElementType)
					{
						case EQueryElementType.SqlField     :
							{
								var field = (ISqlField)predicate.Expr1;

								sqlDataType = new SqlDataType(
									field.DataType,
									field.SystemType,
									field.Length,
									field.Precision,
									field.Scale);
							}
							break;

						case EQueryElementType.SqlParameter :
							{
								var p = (ISqlParameter)predicate.Expr1;
								sqlDataType = new SqlDataType(p.DataType, p.SystemType, 0, 0, 0);
							}

							break;
					}
				}

			    var queryExpression = value as IQueryExpression;
			    if (queryExpression != null)
					BuildExpression(queryExpression);
				else
					BuildValue(sqlDataType, value);

				StringBuilder.Append(", ");
			}

			if (firstValue)
			{
				BuildPredicate(
					hasNull ?
						new IsNull(predicate.Expr1, predicate.IsNot) :
						new Expr(new SqlValue(predicate.IsNot)));
			}
			else
			{
				StringBuilder.Remove(StringBuilder.Length - 2, 2).Append(')');

				if (hasNull)
				{
					StringBuilder.Insert(len, "(");
					StringBuilder.Append(" OR ");
					BuildPredicate(new IsNull(predicate.Expr1, predicate.IsNot));
					StringBuilder.Append(")");
				}
			}

			if (longList && !hasNull)
			{
				StringBuilder.Insert(len, "(");
				StringBuilder.Append(")");
			}
		}

		protected void BuildPredicate(int parentPrecedence, ISqlPredicate predicate)
		{
			BuildPredicate(parentPrecedence, GetPrecedence(predicate), predicate);
		}

		protected void BuildPredicate(int parentPrecedence, int precedence, ISqlPredicate predicate)
		{
			var wrap = Wrap(precedence, parentPrecedence);

			if (wrap) StringBuilder.Append('(');
			BuildPredicate(predicate);
			if (wrap) StringBuilder.Append(')');
		}

		protected virtual void BuildLikePredicate(ILike predicate)
		{
			var precedence = GetPrecedence(predicate);

			BuildExpression(precedence, predicate.Expr1);
            StringBuilder.AppendFormat(" {0} ", predicate.GetOperator());
			BuildExpression(precedence, predicate.Expr2);

			if (predicate.Escape != null)
			{
				StringBuilder.Append(" ESCAPE ");
				BuildExpression(predicate.Escape);
			}
		}

		#endregion

		#region BuildExpression

		protected virtual StringBuilder BuildExpression(
			IQueryExpression expr,
			bool           buildTableName,
			bool           checkParentheses,
			string         alias,
			ref bool       addAlias,
			bool           throwExceptionIfTableNotFound = true)
		{
			// TODO: check the necessity.
			//
			expr = SqlOptimizer.ConvertExpression(expr);

			switch (expr.ElementType)
			{
				case EQueryElementType.SqlField:
					{
						var field = (ISqlField)expr;

						if (buildTableName)
						{
							var ts = SelectQuery.GetTableSource(field.Table);

							if (ts == null)
							{
								if (field != field.Table.All)
								{
									if (throwExceptionIfTableNotFound)
										throw new SqlException("Table '{0}' not found.", field.Table);
								}
							}
							else
							{
								var table = GetTableAlias(ts);

								table = table == null ?
									GetPhysicalTableName(field.Table, null) :
									Convert(table, ConvertType.NameToQueryTableAlias).ToString();

								if (string.IsNullOrEmpty(table))
									throw new SqlException("Table {0} should have an alias.", field.Table);

								addAlias = alias != field.PhysicalName;

								StringBuilder
									.Append(table)
									.Append('.');
							}
						}

						if (field == field.Table.All)
						{
							StringBuilder.Append("*");
						}
						else
						{
							StringBuilder.Append(Convert(field.PhysicalName, ConvertType.NameToQueryField));
						}
					}

					break;

				case EQueryElementType.Column:
					{
						var column = (IColumn)expr;

						var table = SelectQuery.GetTableSource(column.Parent);

						if (table == null)
						{
							throw new SqlException("Table not found for '{0}'.", column);
						}

						var tableAlias = GetTableAlias(table) ?? GetPhysicalTableName(column.Parent, null);

						if (string.IsNullOrEmpty(tableAlias))
							throw new SqlException("Table {0} should have an alias.", column.Parent);

						addAlias = alias != column.Alias;

						StringBuilder
							.Append(Convert(tableAlias, ConvertType.NameToQueryTableAlias))
							.Append('.')
							.Append(Convert(column.Alias, ConvertType.NameToQueryField));
					}

					break;

				case EQueryElementType.SqlQuery:
					{
						var hasParentheses = checkParentheses && StringBuilder[StringBuilder.Length - 1] == '(';

						if (!hasParentheses)
							StringBuilder.Append("(");
						StringBuilder.AppendLine();

						BuildSqlBuilder((ISelectQuery)expr, Indent + 1, BuildStep != Step.FromClause);

						AppendIndent();

						if (!hasParentheses)
							StringBuilder.Append(")");
					}

					break;

				case EQueryElementType.SqlValue:
			        var sqlValue = (ISqlValue)expr;

                    // Если значение не равно NULL, то CAST не требуется
                    // Для колонок, которые не принимают непосредственное участие в формировании поля сущности, нет смысла делать CAST
                    if (sqlValue.Value != null || sqlValue.SystemType == null)
			        {
                        BuildValue(null, sqlValue.Value);
                    }
			        else
			        {
                        // Если колонка имеет значение NULL и SystemType не равен NULL(участвует в формировании поля сущности), кастим колонку к типу поля
                        StringBuilder.Append("CAST(");

                        BuildValue(null, sqlValue.Value);

                        StringBuilder.Append(" as ");

                        BuildDataType(SqlDataType.GetDataType(expr.SystemType));

                        StringBuilder.Append(")");
                    }
					break;

				case EQueryElementType.SqlExpression:
					{
						var e = (ISqlExpression)expr;
						var s = new StringBuilder();

						if (e.Parameters == null || e.Parameters.Length == 0)
							StringBuilder.Append(e.Expr);
						else
						{
							var values = new object[e.Parameters.Length];

							for (var i = 0; i < values.Length; i++)
							{
								var value = e.Parameters[i];

								s.Length = 0;
								WithStringBuilder(s, () => BuildExpression(GetPrecedence(e), value));
								values[i] = s.ToString();
							}

							StringBuilder.AppendFormat(e.Expr, values);
						}
					}

					break;

				case EQueryElementType.SqlBinaryExpression:
					BuildBinaryExpression((ISqlBinaryExpression)expr);
					break;

				case EQueryElementType.SqlFunction:
					BuildFunction((ISqlFunction)expr);
					break;

				case EQueryElementType.SqlParameter:
					{
						var parm = (ISqlParameter)expr;

						if (parm.IsQueryParameter)
						{
							var name = Convert(parm.Name, ConvertType.NameToQueryParameter);
							StringBuilder.Append(name);
						}
						else
						{
							BuildValue(new SqlDataType(parm.DataType, parm.SystemType, 0, 0, 0), parm.Value);
						}
					}

					break;

				case EQueryElementType.SqlDataType:
					BuildDataType((ISqlDataType)expr);
					break;

				case EQueryElementType.SearchCondition:
					BuildSearchCondition(expr.Precedence, (ISearchCondition)expr);
					break;

				default:
					throw new InvalidOperationException();
			}

			return StringBuilder;
		}

		void BuildExpression(int parentPrecedence, IQueryExpression expr, string alias, ref bool addAlias)
		{
			var wrap = Wrap(GetPrecedence(expr), parentPrecedence);

			if (wrap) StringBuilder.Append('(');
			BuildExpression(expr, true, true, alias, ref addAlias);
			if (wrap) StringBuilder.Append(')');
		}

		protected StringBuilder BuildExpression(IQueryExpression expr)
		{
			var dummy = false;
			return BuildExpression(expr, true, true, null, ref dummy);
		}

		protected void BuildExpression(IQueryExpression expr, bool buildTableName, bool checkParentheses, bool throwExceptionIfTableNotFound = true)
		{
			var dummy = false;
			BuildExpression(expr, buildTableName, checkParentheses, null, ref dummy, throwExceptionIfTableNotFound);
		}

		protected void BuildExpression(int precedence, IQueryExpression expr)
		{
			var dummy = false;
			BuildExpression(precedence, expr, null, ref dummy);
		}

		#endregion

		#region BuildValue

		protected void BuildValue(ISqlDataType dataType, object value)
		{
			if (dataType != null)
				ValueToSqlConverter.Convert(StringBuilder, dataType, value);
			else
				ValueToSqlConverter.Convert(StringBuilder, value);
		}

		#endregion

		#region BuildBinaryExpression

		protected virtual void BuildBinaryExpression(ISqlBinaryExpression expr)
		{
			BuildBinaryExpression(expr.Operation, expr);
		}

		void BuildBinaryExpression(string op, ISqlBinaryExpression expr)
		{
		    var sqlValue = expr.Expr1 as ISqlValue;
		    if (expr.Operation == "*" && sqlValue != null)
			{
			    if (sqlValue.Value is int && (int)sqlValue.Value == -1)
				{
					StringBuilder.Append('-');
					BuildExpression(GetPrecedence(expr), expr.Expr2);
					return;
				}
			}

			BuildExpression(GetPrecedence(expr), expr.Expr1);
			StringBuilder.Append(' ').Append(op).Append(' ');
			BuildExpression(GetPrecedence(expr), expr.Expr2);
		}

		#endregion

		#region BuildFunction

		protected virtual void BuildFunction(ISqlFunction func)
		{
			if (func.Name == "CASE")
			{
				StringBuilder.Append(func.Name).AppendLine();

				Indent++;

				var i = 0;

				for (; i < func.Parameters.Length - 1; i += 2)
				{
					AppendIndent().Append("WHEN ");

					var len = StringBuilder.Length;

					BuildExpression(func.Parameters[i]);

					if (SqlExpression.NeedsEqual(func.Parameters[i]))
					{
						StringBuilder.Append(" = ");
						BuildValue(null, true);
					}

					if (StringBuilder.Length - len > 20)
					{
						StringBuilder.AppendLine();
						AppendIndent().Append("\tTHEN ");
					}
					else
						StringBuilder.Append(" THEN ");

					BuildExpression(func.Parameters[i+1]);
					StringBuilder.AppendLine();
				}

				if (i < func.Parameters.Length)
				{
					AppendIndent().Append("ELSE ");
					BuildExpression(func.Parameters[i]);
					StringBuilder.AppendLine();
				}

				Indent--;

				AppendIndent().Append("END");
			}
			else
				BuildFunction(func.Name, func.Parameters);
		}

		void BuildFunction(string name, IQueryExpression[] exprs)
		{
			StringBuilder.Append(name).Append('(');

			var first = true;

			foreach (var parameter in exprs)
			{
				if (!first)
					StringBuilder.Append(", ");

				BuildExpression(parameter, true, !first || name == "EXISTS");

				first = false;
			}

			StringBuilder.Append(')');
		}

		#endregion

		#region BuildDataType
	
		protected virtual void BuildDataType(ISqlDataType type, bool createDbType = false)
		{
			switch (type.DataType)
			{
				case DataType.Double  : StringBuilder.Append("Float");    return;
				case DataType.Single  : StringBuilder.Append("Real");     return;
				case DataType.SByte   : StringBuilder.Append("TinyInt");  return;
				case DataType.UInt16  : StringBuilder.Append("Int");      return;
				case DataType.UInt32  : StringBuilder.Append("BigInt");   return;
				case DataType.UInt64  : StringBuilder.Append("Decimal");  return;
				case DataType.Byte    : StringBuilder.Append("TinyInt");  return;
				case DataType.Int16   : StringBuilder.Append("SmallInt"); return;
				case DataType.Int32   : StringBuilder.Append("Int");      return;
				case DataType.Int64   : StringBuilder.Append("BigInt");   return;
				case DataType.Boolean : StringBuilder.Append("Bit");      return;
			}

			StringBuilder.Append(type.DataType);

			if (type.Length > 0)
				StringBuilder.Append('(').Append(type.Length).Append(')');

			if (type.Precision > 0)
				StringBuilder.Append('(').Append(type.Precision).Append(',').Append(type.Scale).Append(')');
		}

		#endregion

		#region GetPrecedence

		static int GetPrecedence(IQueryExpression expr)
		{
			return expr.Precedence;
		}

		protected static int GetPrecedence(ISqlPredicate predicate)
		{
			return predicate.Precedence;
		}

		#endregion

		#endregion

		#region Internal Types

		protected enum Step
		{
			SelectClause,
			DeleteClause,
			UpdateClause,
			InsertClause,
			FromClause,
			WhereClause,
			GroupByClause,
			HavingClause,
			OrderByClause,
			OffsetLimit
		}

		#endregion

		#region Alternative Builders

		void BuildAliases(string table, List<IColumn> columns, string postfix)
		{
			Indent++;

			var first = true;

			foreach (var col in columns)
			{
				if (!first)
					StringBuilder.Append(',').AppendLine();
				first = false;

				AppendIndent().AppendFormat("{0}.{1}", table, Convert(col.Alias, ConvertType.NameToQueryFieldAlias));

				if (postfix != null)
					StringBuilder.Append(postfix);
			}

			Indent--;

			StringBuilder.AppendLine();
		}

		protected void AlternativeBuildSql(bool implementOrderBy, Action buildSql)
		{
			if (NeedSkip)
			{
				var aliases  = GetTempAliases(2, "t");
				var rnaliase = GetTempAliases(1, "rn")[0];

				AppendIndent().Append("SELECT *").AppendLine();
				AppendIndent().Append("FROM").    AppendLine();
				AppendIndent().Append("(").       AppendLine();
				Indent++;

				AppendIndent().Append("SELECT").AppendLine();

				Indent++;
				AppendIndent().AppendFormat("{0}.*,", aliases[0]).AppendLine();
				AppendIndent().Append("ROW_NUMBER() OVER");

				if (!SelectQuery.OrderBy.IsEmpty && !implementOrderBy)
					StringBuilder.Append("()");
				else
				{
					StringBuilder.AppendLine();
					AppendIndent().Append("(").AppendLine();

					Indent++;

					if (SelectQuery.OrderBy.IsEmpty)
					{
						AppendIndent().Append("ORDER BY").AppendLine();
						BuildAliases(aliases[0], SelectQuery.Select.Columns.Take(1).ToList(), null);
					}
					else
						BuildAlternativeOrderBy(true);

					Indent--;
					AppendIndent().Append(")");
				}

				StringBuilder.Append(" as ").Append(rnaliase).AppendLine();
				Indent--;

				AppendIndent().Append("FROM").AppendLine();
				AppendIndent().Append("(").AppendLine();

				Indent++;
				buildSql();
				Indent--;

				AppendIndent().AppendFormat(") {0}", aliases[0]).AppendLine();

				Indent--;

				AppendIndent().AppendFormat(") {0}", aliases[1]).AppendLine();
				AppendIndent().Append("WHERE").AppendLine();

				Indent++;

				if (NeedTake)
				{
					var expr1 = Add(SelectQuery.Select.SkipValue, 1);
					var expr2 = Add<int>(SelectQuery.Select.SkipValue, SelectQuery.Select.TakeValue);

				    var sqlValue1 = expr1 as ISqlValue;
				    var sqlValue2 = expr2 as ISqlValue;
				    if (sqlValue1 != null && sqlValue2 != null && Equals(sqlValue1.Value, sqlValue2.Value))
					{
						AppendIndent().AppendFormat("{0}.{1} = ", aliases[1], rnaliase);
						BuildExpression(sqlValue1);
					}
					else
					{
						AppendIndent().AppendFormat("{0}.{1} BETWEEN ", aliases[1], rnaliase);
						BuildExpression(expr1);
						StringBuilder.Append(" AND ");
						BuildExpression(expr2);
					}
				}
				else
				{
					AppendIndent().AppendFormat("{0}.{1} > ", aliases[1], rnaliase);
					BuildExpression(SelectQuery.Select.SkipValue);
				}

				StringBuilder.AppendLine();
				Indent--;
			}
			else
				buildSql();
		}

		protected void AlternativeBuildSql2(Action buildSql)
		{
			var aliases = GetTempAliases(3, "t");

			AppendIndent().Append("SELECT *").AppendLine();
			AppendIndent().Append("FROM")    .AppendLine();
			AppendIndent().Append("(")       .AppendLine();
			Indent++;

			AppendIndent().Append("SELECT TOP ");
			BuildExpression(SelectQuery.Select.TakeValue);
			StringBuilder.Append(" *").AppendLine();
			AppendIndent().Append("FROM").AppendLine();
			AppendIndent().Append("(")   .AppendLine();
			Indent++;

			if (SelectQuery.OrderBy.IsEmpty)
			{
				AppendIndent().Append("SELECT TOP ");

				var p = SelectQuery.Select.SkipValue as ISqlParameter;

			    var sqlValue = SelectQuery.Select.TakeValue as ISqlValue;
			    if (p != null && !p.IsQueryParameter && sqlValue != null)
					BuildValue(null, (int)p.Value + (int)sqlValue.Value);
				else
					BuildExpression(Add<int>(SelectQuery.Select.SkipValue, SelectQuery.Select.TakeValue));

				StringBuilder.Append(" *").AppendLine();
				AppendIndent().Append("FROM").AppendLine();
				AppendIndent().Append("(")   .AppendLine();
				Indent++;
			}

			buildSql();

			if (SelectQuery.OrderBy.IsEmpty)
			{
				Indent--;
				AppendIndent().AppendFormat(") {0}", aliases[2]).AppendLine();
				AppendIndent().Append("ORDER BY").AppendLine();
				BuildAliases(aliases[2], SelectQuery.Select.Columns, null);
			}

			Indent--;
			AppendIndent().AppendFormat(") {0}", aliases[1]).AppendLine();

			if (SelectQuery.OrderBy.IsEmpty)
			{
				AppendIndent().Append("ORDER BY").AppendLine();
				BuildAliases(aliases[1], SelectQuery.Select.Columns, " DESC");
			}
			else
			{
				BuildAlternativeOrderBy(false);
			}

			Indent--;
			AppendIndent().AppendFormat(") {0}", aliases[0]).AppendLine();

			if (SelectQuery.OrderBy.IsEmpty)
			{
				AppendIndent().Append("ORDER BY").AppendLine();
				BuildAliases(aliases[0], SelectQuery.Select.Columns, null);
			}
			else
			{
				BuildAlternativeOrderBy(true);
			}
		}

		void BuildAlternativeOrderBy(bool ascending)
		{
			AppendIndent().Append("ORDER BY").AppendLine();

			var obys = GetTempAliases(SelectQuery.OrderBy.Items.Count, "oby");

			Indent++;

			for (var i = 0; i < obys.Length; i++)
			{
				AppendIndent().Append(obys[i]);

				if ( ascending &&  SelectQuery.OrderBy.Items[i].IsDescending ||
					!ascending && !SelectQuery.OrderBy.Items[i].IsDescending)
					StringBuilder.Append(" DESC");

				if (i + 1 < obys.Length)
					StringBuilder.Append(',');

				StringBuilder.AppendLine();
			}

			Indent--;
		}

		protected delegate IEnumerable<IColumn> ColumnSelector();

		protected IEnumerable<IColumn> AlternativeGetSelectedColumns(ColumnSelector columnSelector)
		{
			foreach (var col in columnSelector())
				yield return col;

			var obys = GetTempAliases(SelectQuery.OrderBy.Items.Count, "oby");

			for (var i = 0; i < obys.Length; i++)
				yield return new Column(SelectQuery, SelectQuery.OrderBy.Items[i].Expression, obys[i]);
		}

		protected static bool IsDateDataType(IQueryExpression expr, string dateName)
		{
			switch (expr.ElementType)
			{
				case EQueryElementType.SqlDataType   : return ((ISqlDataType)  expr).DataType == DataType.Date;
				case EQueryElementType.SqlExpression : return ((ISqlExpression)expr).Expr     == dateName;
			}

			return false;
		}

		protected static bool IsTimeDataType(IQueryExpression expr)
		{
			switch (expr.ElementType)
			{
				case EQueryElementType.SqlDataType   : return ((ISqlDataType)expr).  DataType == DataType.Time;
				case EQueryElementType.SqlExpression : return ((ISqlExpression)expr).Expr     == "Time";
			}

			return false;
		}

		static bool IsBooleanParameter(IQueryExpression expr, int count, int i)
		{
			if ((i % 2 == 1 || i == count - 1) && expr.SystemType == typeof(bool) || expr.SystemType == typeof(bool?))
			{
				switch (expr.ElementType)
				{
					case EQueryElementType.SearchCondition : return true;
				}
			}

			return false;
		}

		protected ISqlFunction ConvertFunctionParameters(ISqlFunction func)
		{
			if (func.Name == "CASE" &&
				func.Parameters.Select((p,i) => new { p, i }).Any(p => IsBooleanParameter(p.p, func.Parameters.Length, p.i)))
			{
				return new SqlFunction(
					func.SystemType,
					func.Name,
					func.Precedence,
					func.Parameters.Select((p,i) =>
						IsBooleanParameter(p, func.Parameters.Length, i) ?
							SqlOptimizer.ConvertExpression(new SqlFunction(typeof(bool), "CASE", p, new SqlValue(true), new SqlValue(false))) :
							p
					).ToArray());
			}

			return func;
		}

		#endregion

		#region Helpers

		protected SequenceNameAttribute GetSequenceNameAttribute(ISqlTable table, bool throwException)
		{
			var identityField = table.GetIdentityField();

			if (identityField == null)
				if (throwException)
					throw new SqlException("Identity field must be defined for '{0}'.", table.Name);
				else
					return null;

			if (table.ObjectType == null)
				if (throwException)
					throw new SqlException("Sequence name can not be retrieved for the '{0}' table.", table.Name);
				else
					return null;

			var attrs = table.SequenceAttributes;

			if (attrs.IsNullOrEmpty())
				if (throwException)
					throw new SqlException("Sequence name can not be retrieved for the '{0}' table.", table.Name);
				else
					return null;

			SequenceNameAttribute defaultAttr = null;

			foreach (var attr in attrs)
			{
				if (attr.Configuration == Name)
					return attr;

				if (defaultAttr == null && attr.Configuration == null)
					defaultAttr = attr;
			}

			if (defaultAttr == null)
				if (throwException)
					throw new SqlException("Sequence name can not be retrieved for the '{0}' table.", table.Name);
				else
					return null;

			return defaultAttr;
		}

		static bool Wrap(int precedence, int parentPrecedence)
		{
			return
				precedence == 0 ||
				precedence < parentPrecedence ||
				(precedence == parentPrecedence && 
					(parentPrecedence == Precedence.Subtraction ||
					 parentPrecedence == Precedence.LogicalNegation));
		}

		protected string[] GetTempAliases(int n, string defaultAlias)
		{
			return SelectQuery.GetTempAliases(n, defaultAlias);
		}

		protected static string GetTableAlias(ISqlTableSource table)
		{
			switch (table.ElementType)
			{
				case EQueryElementType.TableSource :
					var ts    = (ITableSource)table;
					var alias = string.IsNullOrEmpty(ts.Alias) ? GetTableAlias(ts.Source) : ts.Alias;
					return alias != "$" ? alias : null;

				case EQueryElementType.SqlTable :
					return ((ISqlTable)table).Alias;

                case EQueryElementType.SqlQuery:
                    return GetTableAlias(((ISelectQuery)table).From.Tables.First.Value);

                default :
					throw new InvalidOperationException();
			}
		}

		protected virtual string GetTableDatabaseName(ISqlTable table)
		{
			return table.Database == null ? null : Convert(table.Database, ConvertType.NameToDatabase).ToString();
		}

		protected virtual string GetTableOwnerName(ISqlTable table)
		{
			return table.Owner == null ? null : Convert(table.Owner, ConvertType.NameToOwner).ToString();
		}

		protected virtual string GetTablePhysicalName(ISqlTable table)
		{
			return table.PhysicalName == null ? null : Convert(table.PhysicalName, ConvertType.NameToQueryTable).ToString();
		}

		string GetPhysicalTableName(ISqlTableSource table, string alias)
		{
			switch (table.ElementType)
			{
				case EQueryElementType.SqlTable :
					{
						var tbl = (ISqlTable)table;

						var database     = GetTableDatabaseName(tbl);
						var owner        = GetTableOwnerName   (tbl);
						var physicalName = GetTablePhysicalName(tbl);

						var sb = new StringBuilder();

						BuildTableName(sb, database, owner, physicalName);

						if (tbl.SqlTableType == ESqlTableType.Expression)
						{
						    var tableArguments = tbl.TableArguments.ToArray();

						    var values = new object[2 + tableArguments.Length];

							values[0] = sb.ToString();
							values[1] = Convert(alias, ConvertType.NameToQueryTableAlias);

							for (var i = 2; i < values.Length; i++)
							{
								var value = tableArguments[i - 2];

								sb.Length = 0;
								WithStringBuilder(sb, () => BuildExpression(Precedence.Primary, value));
								values[i] = sb.ToString();
							}

							sb.Length = 0;
							sb.AppendFormat(tbl.Name, values);
						}

						if (tbl.SqlTableType == ESqlTableType.Function)
						{
							sb.Append('(');

						    if (tbl.TableArguments != null && tbl.TableArguments.Count > 0)
						    {
						        var first = true;

						        tbl.TableArguments.ForEach(
						            node =>
						            {
						                if (!first)
						                    sb.Append(", ");

						                var firstClosure = first;
						                WithStringBuilder(sb, () => BuildExpression(node.Value, true, !firstClosure));

						                first = false;
						            });

						    }

						    sb.Append(')');
						}

						return sb.ToString();
					}

				case EQueryElementType.TableSource :
					return GetPhysicalTableName(((ITableSource)table).Source, alias);

				default :
					throw new InvalidOperationException();
			}
		}

		protected StringBuilder AppendIndent()
		{
			if (Indent > 0)
				StringBuilder.Append('\t', Indent);

			return StringBuilder;
		}

		IQueryExpression Add(IQueryExpression expr1, IQueryExpression expr2, Type type)
		{
			return SqlOptimizer.ConvertExpression(new SqlBinaryExpression(type, expr1, "+", expr2, Precedence.Additive));
		}

		protected IQueryExpression Add<T>(IQueryExpression expr1, IQueryExpression expr2)
		{
			return Add(expr1, expr2, typeof(T));
		}

		IQueryExpression Add(IQueryExpression expr1, int value)
		{
			return Add<int>(expr1, new SqlValue(value));
		}

		#endregion

		#region ISqlProvider Members

		public virtual IQueryExpression GetIdentityExpression(ISqlTable table)
		{
			return null;
		}

		protected virtual void PrintParameterName(StringBuilder sb, IDbDataParameter parameter)
		{
			if (!parameter.ParameterName.StartsWith("@"))
				sb.Append('@');
			sb.Append(parameter.ParameterName);
		}

		protected virtual string GetTypeName(IDbDataParameter parameter)
		{
			return null;
		}

		protected virtual string GetUdtTypeName(IDbDataParameter parameter)
		{
			return null;
		}

		protected virtual string GetProviderTypeName(IDbDataParameter parameter)
		{
			return null;
		}

		protected virtual void PrintParameterType(StringBuilder sb, IDbDataParameter parameter)
		{
			var typeName = GetTypeName(parameter);
			if (!string.IsNullOrEmpty(typeName))
				sb.Append(typeName).Append(" -- ");

			var udtTypeName = GetUdtTypeName(parameter);
			if (!string.IsNullOrEmpty(udtTypeName))
				sb.Append(udtTypeName).Append(" -- ");

			var t1 = GetProviderTypeName(parameter);
			var t2 = parameter.DbType.ToString();

			sb.Append(t1);

			if (t1 != t2)
				sb.Append(" -- ").Append(t2);
		}

		protected virtual void PrintParameterValue(StringBuilder sb, IDbDataParameter parameter)
		{
			ValueToSqlConverter.Convert(sb, parameter.Value);
		}

		public virtual StringBuilder PrintParameters(StringBuilder sb, IDbDataParameter[] parameters)
		{
			if (parameters != null && parameters.Length > 0)
			{
				foreach (var p in parameters)
				{
					sb.Append("DECLARE ");
					PrintParameterName(sb, p);
					sb.Append(' ');
					PrintParameterType(sb, p);
					sb.AppendLine();

					sb.Append("SET     ");
					PrintParameterName(sb, p);
					sb.Append(" = ");
					ValueToSqlConverter.Convert(sb, p.Value);
					sb.AppendLine();
				}

				sb.AppendLine();
			}

			return sb;
		}

        public virtual StringBuilder ReplaceParameters(StringBuilder sb, IDbDataParameter[] parameters)
        {
            var valueToSqlValueConverter = new ValueToSqlValueConverter();
            valueToSqlValueConverter.SetDefauls();
            if (parameters != null && parameters.Length > 0)
            {
                foreach (var p in parameters.OrderByDescending(param => param.ParameterName))
                {
                    sb.Replace(":" + p.ParameterName, valueToSqlValueConverter.Convert(p.Value));                            
                }
            }

            return sb;
        }

		public string ApplyQueryHints(string sql, List<string> queryHints)
		{
			var sb = new StringBuilder(sql);

			foreach (var hint in queryHints)
				sb.AppendLine(hint);

			return sb.ToString();
		}

		private        string _name;
		public virtual string  Name => _name ?? (_name = GetType().Name.Replace("SqlBuilder", ""));

	    #endregion
	}
}
