namespace LinqToDB.SqlQuery.QueryElements.Conditions
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using LinqToDB.SqlQuery.QueryElements.Conditions.Interfaces;
    using LinqToDB.SqlQuery.QueryElements.Enums;
    using LinqToDB.SqlQuery.QueryElements.Interfaces;
    using LinqToDB.SqlQuery.QueryElements.Predicates;

    public class Condition : BaseQueryElement,
                             ICondition
    {
        public Condition(bool isNot, ISqlPredicate predicate)
        {
            IsNot     = isNot;
            Predicate = predicate;
        }

        public Condition(bool isNot, ISqlPredicate predicate, bool isOr)
        {
            IsNot     = isNot;
            Predicate = predicate;
            IsOr      = isOr;
        }

        public bool          IsNot     { get; set; }
        public ISqlPredicate Predicate { get; set; }
        public bool          IsOr      { get; set; }

        public int Precedence => IsNot ? SqlQuery.Precedence.LogicalNegation :
                                     IsOr  ? SqlQuery.Precedence.LogicalDisjunction :
                                         SqlQuery.Precedence.LogicalConjunction;

        public ICloneableElement Clone(Dictionary<ICloneableElement, ICloneableElement> objectTree, Predicate<ICloneableElement> doClone)
        {
            if (!doClone(this))
                return this;

            ICloneableElement clone;

            if (!objectTree.TryGetValue(this, out clone))
                objectTree.Add(this, clone = new Condition(IsNot, (ISqlPredicate)Predicate.Clone(objectTree, doClone), IsOr));

            return clone;
        }

        public bool CanBeNull()
        {
            return Predicate.CanBeNull();
        }

        #region IQueryElement Members

        protected override void GetChildrenInternal(List<IQueryElement> list)
        {
            list.Add(Predicate);
        }

        public override EQueryElementType ElementType => EQueryElementType.Condition;

        public override StringBuilder ToString(StringBuilder sb, Dictionary<IQueryElement,IQueryElement> dic)
        {
            if (dic.ContainsKey(this))
                return sb.Append("...");

            dic.Add(this, this);

            sb.Append('(');

            if (IsNot) sb.Append("NOT ");

            Predicate.ToString(sb, dic);
            sb.Append(')').Append(IsOr ? " OR " : " AND ");

            dic.Remove(this);

            return sb;
        }

        #endregion
    }
}