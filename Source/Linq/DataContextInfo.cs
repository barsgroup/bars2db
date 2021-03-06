﻿using Bars2Db.Mapping;
using Bars2Db.SqlProvider;

namespace Bars2Db.Linq
{
    internal class DataContextInfo : IDataContextInfo
    {
        public DataContextInfo(IDataContext dataContext)
        {
            DataContext = dataContext;
            DisposeContext = false;
        }

        public DataContextInfo(IDataContext dataContext, bool disposeContext)
        {
            DataContext = dataContext;
            DisposeContext = disposeContext;
        }

        public IDataContext DataContext { get; }
        public bool DisposeContext { get; }
        public string ContextID => DataContext.ContextID;

        public MappingSchema MappingSchema => DataContext.MappingSchema;

        public SqlProviderFlags SqlProviderFlags => DataContext.SqlProviderFlags;

        public ISqlBuilder CreateSqlBuilder()
        {
            return DataContext.CreateSqlProvider();
        }

        public ISqlOptimizer GetSqlOptimizer()
        {
            return DataContext.GetSqlOptimizer();
        }

        public IDataContextInfo Clone(bool forNestedQuery)
        {
            return new DataContextInfo(DataContext.Clone(forNestedQuery));
        }

        public static IDataContextInfo Create(IDataContext dataContext)
        {
#if SILVERLIGHT || NETFX_CORE
			if (dataContext == null) throw new ArgumentNullException("dataContext");
			return new DataContextInfo(dataContext);
#else
            return dataContext == null
                ? (IDataContextInfo) new DefaultDataContextInfo()
                : new DataContextInfo(dataContext);
#endif
        }
    }
}