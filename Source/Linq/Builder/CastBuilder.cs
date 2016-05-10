﻿using System.Linq.Expressions;
using Bars2Db.Expressions;

namespace Bars2Db.Linq.Builder
{
    internal class CastBuilder : MethodCallBuilder
    {
        protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall,
            BuildInfo buildInfo)
        {
            return methodCall.IsQueryable("Cast");
        }

        protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall,
            BuildInfo buildInfo)
        {
            var sequence = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));

            return new CastContext(sequence, methodCall);
        }

        protected override SequenceConvertInfo Convert(
            ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo, ParameterExpression param)
        {
            return null;
        }

        private class CastContext : PassThroughContext
        {
            private readonly MethodCallExpression _methodCall;

            public CastContext(IBuildContext context, MethodCallExpression methodCall)
                : base(context)
            {
                _methodCall = methodCall;
            }

            public override void BuildQuery<T>(Query<T> query, ParameterExpression queryParameter)
            {
                var expr = BuildExpression(null, 0);
                var mapper = Builder.BuildMapper<T>(expr);

                query.SetQuery(mapper);
            }

            public override Expression BuildExpression(Expression expression, int level)
            {
                var expr = base.BuildExpression(expression, level);
                var type = _methodCall.Method.GetGenericArguments()[0];

                if (expr.Type != type)
                    expr = Expression.Convert(expr, type);

                return expr;
            }
        }
    }
}