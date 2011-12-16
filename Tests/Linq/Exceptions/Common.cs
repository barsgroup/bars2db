﻿using System;
using System.Collections.Generic;

using LinqToDB;
using LinqToDB.Common;
using LinqToDB.Data;
using LinqToDB.SqlBuilder;

using NUnit.Framework;

namespace Tests.Exceptions
{
	using Model;

	[TestFixture]
	public class Common : TestBase
	{
		class MyDbManager : TestDbManager
		{
			public MyDbManager() : base("Sql2008") {}

			protected override SqlQuery ProcessQuery(SqlQuery sqlQuery)
			{
				if (sqlQuery.IsInsert && sqlQuery.Insert.Into.Name == "Parent")
				{
					var expr =
						new QueryVisitor().Find(sqlQuery.Insert, e =>
						{
							if (e.ElementType == QueryElementType.SetExpression)
							{
								var se = (SqlQuery.SetExpression)e;
								return ((SqlField)se.Column).Name == "ParentID";
							}

							return false;
						}) as SqlQuery.SetExpression;

					if (expr != null)
					{
						var value = ConvertTo<int>.From(((IValueContainer)expr.Expression).Value);

						if (value == 555)
						{
							var tableName = "Parent1";
							var dic       = new Dictionary<IQueryElement,IQueryElement>();

							sqlQuery = new QueryVisitor().Convert(sqlQuery, e =>
							{
								if (e.ElementType == QueryElementType.SqlTable)
								{
									var oldTable = (SqlTable)e;

									if (oldTable.Name == "Parent")
									{
										var newTable = new SqlTable(oldTable) { Name = tableName, PhysicalName = tableName };

										foreach (var field in oldTable.Fields.Values)
											dic.Add(field, newTable.Fields[field.Name]);

										return newTable;
									}
								}

								IQueryElement ex;
								return dic.TryGetValue(e, out ex) ? ex : null;
							});
						}
					}
				}

				return sqlQuery;
			}
		}

		[Test, ExpectedException(typeof(DataException), ExpectedMessage = "Invalid object name 'Parent1'.")]
		public void ReplaceTableTest()
		{
			using (var db = new MyDbManager())
			{
				var n = 555;

				db.Parent.Insert(() => new Parent
				{
					ParentID = n,
					Value1   = n
				});

				db.Parent.Delete(p => p.ParentID == n);
			}
		}
	}
}
