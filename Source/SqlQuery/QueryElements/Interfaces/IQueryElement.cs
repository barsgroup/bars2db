namespace LinqToDB.SqlQuery.QueryElements.Interfaces
{
    using System.Collections.Generic;
    using System.Text;

    using LinqToDB.SqlQuery.QueryElements.Enums;

    public interface IQueryElement //: ICloneableElement
    {
        EQueryElementType ElementType { get; }

        StringBuilder ToString(StringBuilder sb, Dictionary<IQueryElement, IQueryElement> dic);

        string SqlText { get; }
    }
}
