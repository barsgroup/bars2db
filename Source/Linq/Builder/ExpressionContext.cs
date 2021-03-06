﻿using System;
using System.Linq.Expressions;
using Bars2Db.Expressions;
using Bars2Db.SqlQuery.QueryElements.Interfaces;

namespace Bars2Db.Linq.Builder
{
    internal class ExpressionContext : SequenceContextBase
    {
        public ExpressionContext(IBuildContext parent, IBuildContext sequence, LambdaExpression lambda)
            : base(parent, sequence, lambda)
        {
        }

        public ExpressionContext(IBuildContext parent, IBuildContext sequence, LambdaExpression lambda,
            ISelectQuery selectQuery)
            : base(parent, sequence, lambda)
        {
            Select = selectQuery;
        }

        public override Expression BuildExpression(Expression expression, int level)
        {
            throw new InvalidOperationException();
        }

        public override SqlInfo[] ConvertToSql(Expression expression, int level, ConvertFlags flags)
        {
            if (level == 0)
            {
                switch (flags)
                {
                    case ConvertFlags.Field:
                    case ConvertFlags.Key:
                    case ConvertFlags.All:
                    {
                        var root = expression.GetRootObject();

                        if (root.NodeType == ExpressionType.Parameter)
                        {
                            var ctx = Builder.GetContext(this, root);

                            if (ctx != null)
                            {
                                if (ctx != this)
                                    return ctx.ConvertToSql(expression, 0, flags);

                                return root == expression
                                    ? Sequence.ConvertToSql(null, 0, flags)
                                    : Sequence.ConvertToSql(expression, level + 1, flags);
                            }
                        }

                        break;
                    }
                }

                throw new LinqException("'{0}' cannot be converted to SQL.", expression);
            }

            throw new InvalidOperationException();
        }

        public override SqlInfo[] ConvertToIndex(Expression expression, int level, ConvertFlags flags)
        {
            throw new InvalidOperationException();
        }

        public override IsExpressionResult IsExpression(Expression expression, int level, RequestFor requestFlag)
        {
            switch (requestFlag)
            {
                case RequestFor.Root:
                    return
                        new IsExpressionResult(Lambda.Parameters.Count > 0 &&
                                               ReferenceEquals(expression, Lambda.Parameters[0]));

                case RequestFor.Table:
                case RequestFor.Association:
                case RequestFor.Object:
                case RequestFor.GroupJoin:
                case RequestFor.Field:
                case RequestFor.Expression:
                {
                    var levelExpression = expression.GetLevelExpression(level);

                    return ReferenceEquals(levelExpression, expression)
                        ? Sequence.IsExpression(null, 0, requestFlag)
                        : Sequence.IsExpression(expression, level + 1, requestFlag);
                }
            }

            return IsExpressionResult.False;
        }

        public override IBuildContext GetContext(Expression expression, int level, BuildInfo buildInfo)
        {
            if (ReferenceEquals(expression, Lambda.Parameters[0]))
                return Sequence.GetContext(null, 0, buildInfo);

            switch (expression.NodeType)
            {
                case ExpressionType.Constant:
                case ExpressionType.New:
                case ExpressionType.MemberInit:
                    return null;
            }

            return Sequence.GetContext(expression, level + 1, buildInfo);
        }
    }
}