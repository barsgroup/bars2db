﻿namespace LinqToDB.SqlProvider
{
    using LinqToDB.SqlQuery.QueryElements;
    using LinqToDB.SqlQuery.QueryElements.Interfaces;

    public class SqlProviderFlags
	{
		public bool IsParameterOrderDependent      { get; set; }
		public bool AcceptsTakeAsParameter         { get; set; }
		public bool AcceptsTakeAsParameterIfSkip   { get; set; }
		public bool IsTakeSupported                { get; set; }
		public bool IsSkipSupported                { get; set; }
		public bool IsSkipSupportedIfTake          { get; set; }
		public bool IsDistinctOrderBySupported     { get; set; }
		public bool IsSubQueryTakeSupported        { get; set; }
		public bool IsSubQueryColumnSupported      { get; set; }
		public bool IsCountSubQuerySupported       { get; set; }
		public bool IsIdentityParameterRequired    { get; set; }
		public bool IsApplyJoinSupported           { get; set; }
		public bool IsInsertOrUpdateSupported      { get; set; }
		public bool CanCombineParameters           { get; set; }
		public bool IsGroupByExpressionSupported   { get; set; }
		public int  MaxInListValuesCount           { get; set; }
		public bool IsUpdateSetTableAliasSupported { get; set; }
		public bool IsSybaseBuggyGroupBy           { get; set; }

		public bool GetAcceptsTakeAsParameterFlag(ISelectQuery selectQuery)
		{
			return AcceptsTakeAsParameter || AcceptsTakeAsParameterIfSkip && selectQuery.Select.SkipValue != null;
		}

		public bool GetIsSkipSupportedFlag(ISelectQuery selectQuery)
		{
			return IsSkipSupported || IsSkipSupportedIfTake && selectQuery.Select.TakeValue != null;
		}
	}
}
