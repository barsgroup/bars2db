﻿using System.Linq.Expressions;

namespace LinqToDB.Linq.Builder
{
    using LinqToDB.SqlQuery.QueryElements.Interfaces;
    using LinqToDB.SqlQuery.QueryElements.SqlElements.Interfaces;

    abstract class SequenceContextBase : IBuildContext
    {
        protected SequenceContextBase(IBuildContext parent, IBuildContext sequence, LambdaExpression lambda)
        {
            Parent      = parent;
            Sequence    = sequence;
            Builder     = sequence.Builder;
            Lambda      = lambda;
            Select = sequence.Select;

            Sequence.Parent = this;

            Builder.Contexts.Add(this);
        }

#if DEBUG
        public string _sqlQueryText => Select == null ? "" : Select.SqlText;
#endif

        public IBuildContext     Parent      { get; set; }
        public IBuildContext     Sequence    { get; set; }
        public ExpressionBuilder Builder     { get; set; }
        public LambdaExpression  Lambda      { get; set; }
        public ISelectQuery      Select { get; set; }

        Expression IBuildContext.Expression => Lambda;

        public virtual void BuildQuery<T>(Query<T> query, ParameterExpression queryParameter)
        {
            var expr   = BuildExpression(null, 0);
            var mapper = Builder.BuildMapper<T>(expr);

            query.SetQuery(mapper);
        }

        public abstract Expression         BuildExpression(Expression expression, int level);
        public abstract SqlInfo[]          ConvertToSql   (Expression expression, int level, ConvertFlags flags);
        public abstract SqlInfo[]          ConvertToIndex (Expression expression, int level, ConvertFlags flags);
        public abstract IsExpressionResult IsExpression   (Expression expression, int level, RequestFor requestFlag);
        public abstract IBuildContext      GetContext     (Expression expression, int level, BuildInfo buildInfo);

        public virtual int ConvertToParentIndex(int index, IBuildContext context)
        {
            return Parent == null ? index : Parent.ConvertToParentIndex(index, context);
        }

        public virtual void SetAlias(string alias)
        {
        }

        public virtual IQueryExpression GetSubQuery(IBuildContext context)
        {
            return null;
        }

        protected bool IsSubQuery()
        {
            for (var p = Parent; p != null; p = p.Parent)
                if (p.IsExpression(null, 0, RequestFor.SubQuery).Result)
                    return true;
            return false;
        }
    }
}
