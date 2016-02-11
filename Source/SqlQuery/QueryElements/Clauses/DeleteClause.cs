namespace LinqToDB.SqlQuery.QueryElements.Clauses
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using LinqToDB.SqlQuery.QueryElements.Enums;
    using LinqToDB.SqlQuery.QueryElements.Interfaces;
    using LinqToDB.SqlQuery.QueryElements.SqlElements;
    using LinqToDB.SqlQuery.QueryElements.SqlElements.Interfaces;

    public class DeleteClause : BaseQueryElement, ISqlExpressionWalkable, ICloneableElement
    {
        public ISqlTable Table { get; set; }

        #region ICloneableElement Members

        public ICloneableElement Clone(Dictionary<ICloneableElement, ICloneableElement> objectTree, Predicate<ICloneableElement> doClone)
        {
            if (!doClone(this))
                return this;

            var clone = new DeleteClause();

            if (Table != null)
                clone.Table = (ISqlTable)Table.Clone(objectTree, doClone);

            objectTree.Add(this, clone);

            return clone;
        }

        #endregion

        #region ISqlExpressionWalkable Members

        [Obsolete]
        IQueryExpression ISqlExpressionWalkable.Walk(bool skipColumns, Func<IQueryExpression,IQueryExpression> func)
        {
            if (Table != null)
                ((ISqlExpressionWalkable)Table).Walk(skipColumns, func);

            return null;
        }

        #endregion

        #region IQueryElement Members

        public override void GetChildren(LinkedList<IQueryElement> list)
        {
            list.AddLast(Table);
        }

        public override EQueryElementType ElementType => EQueryElementType.DeleteClause;

        public override StringBuilder ToString(StringBuilder sb, Dictionary<IQueryElement,IQueryElement> dic)
        {
            sb.Append("DELETE FROM ");

            ((IQueryElement)Table)?.ToString(sb, dic);

            sb.AppendLine();

            return sb;
        }

        #endregion
    }
}