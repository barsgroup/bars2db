namespace LinqToDB.SqlQuery.QueryElements.Enums
{
    public enum EOperator
    {
        Equal,          // =     Is the operator used to test the equality between two expressions.
        NotEqual,       // <> != Is the operator used to test the condition of two expressions not being equal to each other.
        Greater,        // >     Is the operator used to test the condition of one expression being greater than the other.
        GreaterOrEqual, // >=    Is the operator used to test the condition of one expression being greater than or equal to the other expression.
        NotGreater,     // !>    Is the operator used to test the condition of one expression not being greater than the other expression.
        Less,           // <     Is the operator used to test the condition of one expression being less than the other.
        LessOrEqual,    // <=    Is the operator used to test the condition of one expression being less than or equal to the other expression.
        NotLess         // !<    Is the operator used to test the condition of one expression not being less than the other expression.
    }
}