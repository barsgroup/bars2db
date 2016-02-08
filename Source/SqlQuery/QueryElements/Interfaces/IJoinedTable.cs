namespace LinqToDB.SqlQuery.QueryElements.Interfaces
{
    using LinqToDB.SqlQuery.QueryElements.Conditions;
    using LinqToDB.SqlQuery.QueryElements.Enums;
    using LinqToDB.SqlQuery.QueryElements.SqlElements.Interfaces;

    public interface IJoinedTable: ISqlExpressionWalkable, ICloneableElement, IQueryElement
    {
        JoinType JoinType { get; set; }

        ITableSource Table { get; set; }

        SearchCondition Condition { get; }

        bool IsWeak { get; set; }

        bool CanConvertApply { get; set; }
    }
}