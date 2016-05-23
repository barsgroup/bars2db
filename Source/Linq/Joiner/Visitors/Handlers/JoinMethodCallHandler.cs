﻿using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Bars2Db.Linq.Joiner.Visitors.Entities;
using Bars2Db.Linq.Joiner.Visitors.Interfaces;

namespace Bars2Db.Linq.Joiner.Visitors.Handlers
{
    public class JoinMethodCallHandler : MethodCallHandler
    {
        public ILambdaExpressionHelper LambdaExpressionHelper { get; set; }

        /// <summary>Получить сотвествия между путями текущего запроса, и путями запросов пришедших в него</summary>
        /// <param name="methodCall">Текущий methodCall</param>
        /// <returns>Соответствия между путями текущего запроса и путями запросов пришедших в него</returns>
        public override IEnumerable<FullPathBinding> GetPathBindings(MethodCallExpression methodCall)
        {
            var bindings = LambdaExpressionHelper.GetBindingsFromResultSelector(methodCall, GetResultSelector(methodCall), GetLeftRoot(methodCall), GetRightRoot(methodCall));

            return bindings;
        }

        protected override bool CanHandleMethod(MethodCallExpression method)
        {
            return method.Method.Name == "Join";
        }

        protected override IEnumerable<FullPathInfo> GetAllMemberPaths(MethodCallExpression methodCall)
        {
            var lambdas = methodCall.GetLamdaArguments().ToArray();

            var leftSelectorPaths = LambdaExpressionHelper.GetAllMemberAccessPaths(lambdas[0], GetLeftRoot(methodCall));
            var rightSelectorPaths = LambdaExpressionHelper.GetAllMemberAccessPaths(lambdas[1], GetRightRoot(methodCall));

            var resultSelectorPaths = LambdaExpressionHelper.GetAllMemberAccessPaths(GetResultSelector(methodCall), GetLeftRoot(methodCall), GetRightRoot(methodCall));

            return leftSelectorPaths.Union(rightSelectorPaths).Union(resultSelectorPaths);
        }

        protected override IEnumerable<Expression> GetNextQueriesInternal(MethodCallExpression methodCall)
        {
            return new[] { methodCall.Arguments[0], methodCall.Arguments[1] };
        }

        private Expression GetLeftRoot(MethodCallExpression node)
        {
            return node.Arguments[0];
        }

        private static LambdaExpression GetResultSelector(MethodCallExpression methodCall)
        {
            var resultSelector = methodCall.GetLamdaArguments().Last();
            return resultSelector;
        }

        private Expression GetRightRoot(MethodCallExpression node)
        {
            return node.Arguments[1];
        }
    }
}