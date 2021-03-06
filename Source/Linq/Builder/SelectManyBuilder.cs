﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Bars2Db.Expressions;
using Bars2Db.Extensions;
using Bars2Db.SqlQuery;
using Bars2Db.SqlQuery.QueryElements;
using Bars2Db.SqlQuery.QueryElements.Enums;
using Bars2Db.SqlQuery.QueryElements.Interfaces;
using Bars2Db.SqlQuery.QueryElements.SqlElements.Interfaces;

namespace Bars2Db.Linq.Builder
{
    internal class SelectManyBuilder : MethodCallBuilder
    {
        protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall,
            BuildInfo buildInfo)
        {
            return methodCall.IsQueryable("SelectMany") && methodCall.Arguments.Count == 3 &&
                   ((LambdaExpression) methodCall.Arguments[1].Unwrap()).Parameters.Count == 1;
        }

        protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall,
            BuildInfo buildInfo)
        {
            var sequence = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));
            var collectionSelector = (LambdaExpression) methodCall.Arguments[1].Unwrap();
            var resultSelector = (LambdaExpression) methodCall.Arguments[2].Unwrap();

            if (!sequence.Select.GroupBy.IsEmpty)
            {
                sequence = new SubQueryContext(sequence);
            }

            var context = new SelectManyContext(buildInfo.Parent, collectionSelector, sequence);
            var expr = collectionSelector.Body.Unwrap();

            var collectionInfo = new BuildInfo(context, expr, new SelectQuery());
            var collection = builder.BuildSequence(collectionInfo);
            var leftJoin = collection is DefaultIfEmptyBuilder.DefaultIfEmptyContext;
            var sql = collection.Select;

            var sequenceTables = new HashSet<ISqlTableSource>(sequence.Select.From.Tables.First.Value.GetTables());
            var newQuery = sql != collectionInfo.SelectQuery &&
                           QueryVisitor.FindFirstOrDefault<ISelectQuery>(sql, e => e == collectionInfo.SelectQuery) ==
                           null;
            var crossApply = null !=
                             QueryVisitor.FindFirstOrDefault<IQueryExpression>(
                                 sql,
                                 e =>
                                     e.ElementType == EQueryElementType.TableSource &&
                                     sequenceTables.Contains((ISqlTableSource) e) ||
                                     e.ElementType == EQueryElementType.SqlField &&
                                     sequenceTables.Contains(((ISqlField) e).Table) ||
                                     e.ElementType == EQueryElementType.Column &&
                                     sequenceTables.Contains(((IColumn) e).Parent));

            var groupJoinSubQueryContext = collection as JoinBuilder.GroupJoinSubQueryContext;
            if (groupJoinSubQueryContext != null)
            {
                var groupJoin = groupJoinSubQueryContext.GroupJoin;

                var joinedTables = groupJoin.Select.From.Tables.First.Value.Joins.First.Value;
                joinedTables.JoinType = EJoinType.Inner;
                joinedTables.IsWeak = false;
            }

            if (newQuery)
            {
                context.Collection = new SubQueryContext(collection, sequence.Select, false);
                return new SelectContext(buildInfo.Parent, resultSelector, sequence, context);
            }

            if (!crossApply)
            {
                if (!leftJoin)
                {
                    context.Collection = new SubQueryContext(collection, sequence.Select, true);
                    return new SelectContext(buildInfo.Parent, resultSelector, sequence, context);
                }

                var join = SelectQuery.OuterApply(sql);
                sequence.Select.From.Tables.First.Value.Joins.AddLast(join.JoinedTable);
                context.Collection = new SubQueryContext(collection, sequence.Select, false);

                return new SelectContext(buildInfo.Parent, resultSelector, sequence, context);
            }

            var tableContext = collection as TableBuilder.TableContext;
            if (tableContext != null)
            {
                var join = tableContext.SqlTable.TableArguments != null &&
                           tableContext.SqlTable.TableArguments.Count > 0
                    ? (leftJoin
                        ? SelectQuery.OuterApply(sql)
                        : SelectQuery.CrossApply(sql))
                    : (leftJoin
                        ? SelectQuery.LeftJoin(sql)
                        : SelectQuery.InnerJoin(sql));

                join.JoinedTable.Condition.Conditions.AddRange(sql.Where.Search.Conditions);
                join.JoinedTable.CanConvertApply = false;

                sql.Where.Search.Conditions.Clear();

                var collectionParent = collection.Parent as TableBuilder.TableContext;

                // Association.
                //
                if (collectionParent != null && collectionInfo.IsAssociationBuilt)
                {
                    QueryVisitor.FindFirstOrDefault<ITableSource>(sequence.Select.From,
                        t => t.Source == collectionParent.SqlTable);
                }
                else
                {
                    sequence.Select.From.Tables.First.Value.Joins.AddLast(join.JoinedTable);
                }

                context.Collection = new SubQueryContext(collection, sequence.Select, false);
                return new SelectContext(buildInfo.Parent, resultSelector, sequence, context);
            }
            else
            {
                var join = leftJoin
                    ? SelectQuery.OuterApply(sql)
                    : SelectQuery.CrossApply(sql);
                sequence.Select.From.Tables.First.Value.Joins.AddLast(join.JoinedTable);

                context.Collection = new SubQueryContext(collection, sequence.Select, false);
                return new SelectContext(buildInfo.Parent, resultSelector, sequence, context);
            }
        }

        protected override SequenceConvertInfo Convert(ExpressionBuilder builder, MethodCallExpression methodCall,
            BuildInfo buildInfo, ParameterExpression param)
        {
            return null;
        }

        public class SelectManyContext : SelectContext
        {
            private IBuildContext _collection;

            public SelectManyContext(IBuildContext parent, LambdaExpression lambda, IBuildContext sequence)
                : base(parent, lambda, sequence)
            {
            }

            public IBuildContext Collection
            {
                get { return _collection; }
                set
                {
                    _collection = value;
                    _collection.Parent = this;
                }
            }

            public override Expression BuildExpression(Expression expression, int level)
            {
                if (expression == null)
                    return Collection.BuildExpression(expression, level);

                var root = expression.GetRootObject();

                if (root == Lambda.Parameters[0])
                    return base.BuildExpression(expression, level);

                return Collection.BuildExpression(expression, level);
            }

            public override void BuildQuery<T>(Query<T> query, ParameterExpression queryParameter)
            {
                if (Collection == null)
                    base.BuildQuery(query, queryParameter);

                throw new NotImplementedException();
            }

            public override SqlInfo[] ConvertToIndex(Expression expression, int level, ConvertFlags flags)
            {
                if (Collection != null)
                {
                    if (expression == null)
                        return Collection.ConvertToIndex(expression, level, flags);

                    var root = expression.GetRootObject();

                    if (root != Lambda.Parameters[0])
                        return Collection.ConvertToIndex(expression, level, flags);
                }

                return base.ConvertToIndex(expression, level, flags);
            }

            public override SqlInfo[] ConvertToSql(Expression expression, int level, ConvertFlags flags)
            {
                if (Collection != null)
                {
                    if (expression == null)
                        return Collection.ConvertToSql(expression, level, flags);

                    var root = expression.GetRootObject();

                    if (root != Lambda.Parameters[0])
                        return Collection.ConvertToSql(expression, level, flags);
                }

                return base.ConvertToSql(expression, level, flags);
            }

            public override IBuildContext GetContext(Expression expression, int level, BuildInfo buildInfo)
            {
                if (Collection != null)
                {
                    if (expression == null)
                        return Collection.GetContext(expression, level, buildInfo);

                    var root = expression.GetRootObject();

                    if (root != Lambda.Parameters[0])
                        return Collection.GetContext(expression, level, buildInfo);
                }

                return base.GetContext(expression, level, buildInfo);
            }

            public override IsExpressionResult IsExpression(Expression expression, int level, RequestFor requestFlag)
            {
                if (Collection != null)
                {
                    if (expression == null)
                        return Collection.IsExpression(expression, level, requestFlag);

                    var root = expression.GetRootObject();

                    if (root != Lambda.Parameters[0])
                        return Collection.IsExpression(expression, level, requestFlag);
                }

                return base.IsExpression(expression, level, requestFlag);
            }
        }
    }
}