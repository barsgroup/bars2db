﻿using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
    using Extensions;
    using LinqToDB.Expressions;
    using LinqToDB.SqlQuery.QueryElements.Interfaces;
    using LinqToDB.SqlQuery.QueryElements.SqlElements;
    using LinqToDB.SqlQuery.QueryElements.SqlElements.Interfaces;

    using SqlQuery;

    class TakeSkipBuilder : MethodCallBuilder
    {
        protected override bool CanBuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
        {
            return methodCall.IsQueryable("Skip", "Take");
        }

        protected override IBuildContext BuildMethodCall(ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo)
        {
            var sequence = builder.BuildSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]));

            var arg = methodCall.Arguments[1].Unwrap();

            if (arg.NodeType == ExpressionType.Lambda)
                arg = ((LambdaExpression)arg).Body.Unwrap();

            var expr = builder.ConvertToSql(sequence, arg);

            if (methodCall.Method.Name == "Take")
            {
                BuildTake(builder, sequence, expr);
            }
            else
            {
                BuildSkip(builder, sequence, sequence.Select.Select.SkipValue, expr);
            }

            return sequence;
        }

        protected override SequenceConvertInfo Convert(
            ExpressionBuilder builder, MethodCallExpression methodCall, BuildInfo buildInfo, ParameterExpression param)
        {
            var info = builder.ConvertSequence(new BuildInfo(buildInfo, methodCall.Arguments[0]), null);

            if (info != null)
            {
                info.Expression =
                    Expression.Call(
                        methodCall.Method.DeclaringType,
                        methodCall.Method.Name,
                        new[] { info.Expression.Type.GetGenericArgumentsEx()[0] },
                        info.Expression, methodCall.Arguments[1]);
                    //methodCall.Transform(ex => ConvertMethod(methodCall, 0, info, null, ex));
                info.Parameter  = param;

                return info;
            }

            return null;
        }

        static void BuildTake(ExpressionBuilder builder, IBuildContext sequence, IQueryExpression expr)
        {
            var sql = sequence.Select;

            sql.Select.Take(expr);

            if (sql.Select.SkipValue != null &&
                builder.DataContextInfo.SqlProviderFlags.IsTakeSupported &&
                !builder.DataContextInfo.SqlProviderFlags.GetIsSkipSupportedFlag(sql))
            {
                var sqlParameter = sql.Select.SkipValue as ISqlParameter;
                var sqlValue = sql.Select.TakeValue as ISqlValue;
                if (sqlParameter != null && sqlValue != null)
                {
                    var skip = sqlParameter;
                    var parm = (ISqlParameter)sqlParameter.Clone(new Dictionary<ICloneableElement,ICloneableElement>(), _ => true);

                    parm.SetTakeConverter((int)sqlValue.Value);

                    sql.Select.Take(parm);

                    var ep = (from pm in builder.CurrentSqlParameters where pm.SqlParameter == skip select pm).First();

                    ep = new ParameterAccessor
                    {
                        Expression   = ep.Expression,
                        Accessor     = ep.Accessor,
                        SqlParameter = parm
                    };

                    builder.CurrentSqlParameters.Add(ep);
                }
                else
                    sql.Select.Take(builder.Convert(
                        sequence,
                        new SqlBinaryExpression(typeof(int), sql.Select.SkipValue, "+", sql.Select.TakeValue, Precedence.Additive)));
            }

            if (!builder.DataContextInfo.SqlProviderFlags.GetAcceptsTakeAsParameterFlag(sql))
            {
                var p = sql.Select.TakeValue as ISqlParameter;

                if (p != null)
                    p.IsQueryParameter = false;
            }
        }

        static void BuildSkip(ExpressionBuilder builder, IBuildContext sequence, IQueryExpression prevSkipValue, IQueryExpression expr)
        {
            var sql = sequence.Select;

            sql.Select.Skip(expr);

            if (sql.Select.TakeValue != null)
            {
                if (builder.DataContextInfo.SqlProviderFlags.GetIsSkipSupportedFlag(sql) ||
                    !builder.DataContextInfo.SqlProviderFlags.IsTakeSupported)
                    sql.Select.Take(builder.Convert(
                        sequence,
                        new SqlBinaryExpression(typeof(int), sql.Select.TakeValue, "-", sql.Select.SkipValue, Precedence.Additive)));

                if (prevSkipValue != null)
                    sql.Select.Skip(builder.Convert(
                        sequence,
                        new SqlBinaryExpression(typeof(int), prevSkipValue, "+", sql.Select.SkipValue, Precedence.Additive)));
            }

            if (!builder.DataContextInfo.SqlProviderFlags.GetAcceptsTakeAsParameterFlag(sql))
            {
                var p = sql.Select.SkipValue as ISqlParameter;

                if (p != null)
                    p.IsQueryParameter = false;
            }
        }
    }
}
