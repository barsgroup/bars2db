﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using Bars2Db.Data;
using Bars2Db.DataProvider;
using Bars2Db.Linq;
using Bars2Db.Mapping;
using Bars2Db.SqlProvider;

namespace Bars2Db
{
    public class DataContext : IDataContext
    {
        private DataConnection _dataConnection;

        private bool? _isMarsEnabled;

        private bool _keepConnectionAlive;

        private List<string> _nextQueryHints;

        private List<string> _queryHints;

        internal int LockDbManagerCounter;

        public DataContext() : this(DataConnection.DefaultConfiguration)
        {
        }

        public DataContext(string configurationString)
        {
            DataProvider = DataConnection.GetDataProvider(configurationString);
            ConfigurationString = configurationString ?? DataConnection.DefaultConfiguration;
            ContextID = DataProvider.Name;
            MappingSchema = DataProvider.MappingSchema;
        }

        public DataContext([Properties.NotNull] IDataProvider dataProvider, [Properties.NotNull] string connectionString)
        {
            if (dataProvider == null) throw new ArgumentNullException(nameof(dataProvider));
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

            DataProvider = dataProvider;
            ConnectionString = connectionString;
            ContextID = DataProvider.Name;
            MappingSchema = DataProvider.MappingSchema;
        }

        private DataContext(int n)
        {
        }

        public string ConfigurationString { get; private set; }
        public string ConnectionString { get; private set; }
        public IDataProvider DataProvider { get; private set; }
        public string LastQuery { get; set; }

        public bool KeepConnectionAlive
        {
            get { return _keepConnectionAlive; }
            set
            {
                _keepConnectionAlive = value;

                if (value == false)
                    ReleaseQuery();
            }
        }

        public bool IsMarsEnabled
        {
            get
            {
                if (_isMarsEnabled == null)
                {
                    if (_dataConnection == null)
                        return false;
                    _isMarsEnabled = _dataConnection.IsMarsEnabled;
                }

                return _isMarsEnabled.Value;
            }
            set { _isMarsEnabled = value; }
        }

        public string ContextID { get; set; }
        public MappingSchema MappingSchema { get; set; }
        public bool InlineParameters { get; set; }

        public List<string> QueryHints
        {
            get
            {
                if (_dataConnection != null)
                    return _dataConnection.QueryHints;

                return _queryHints ?? (_queryHints = new List<string>());
            }
        }

        public List<string> NextQueryHints
        {
            get
            {
                if (_dataConnection != null)
                    return _dataConnection.NextQueryHints;

                return _nextQueryHints ?? (_nextQueryHints = new List<string>());
            }
        }

        Func<ISqlBuilder> IDataContext.CreateSqlProvider => DataProvider.CreateSqlBuilder;

        Func<ISqlOptimizer> IDataContext.GetSqlOptimizer => DataProvider.GetSqlOptimizer;

        Type IDataContext.DataReaderType => DataProvider.DataReaderType;

        Expression IDataContext.GetReaderExpression(MappingSchema mappingSchema, IDataReader reader, int idx,
            Expression readerExpression, Type toType)
        {
            return DataProvider.GetReaderExpression(mappingSchema, reader, idx, readerExpression, toType);
        }

        bool? IDataContext.IsDBNullAllowed(IDataReader reader, int idx)
        {
            return DataProvider.IsDBNullAllowed(reader, idx);
        }

        object IDataContext.SetQuery(IQueryContext queryContext)
        {
            var ctx = GetDataConnection() as IDataContext;
            return ctx.SetQuery(queryContext);
        }

        int IDataContext.ExecuteNonQuery(object query)
        {
            var ctx = GetDataConnection() as IDataContext;
            return ctx.ExecuteNonQuery(query);
        }

        object IDataContext.ExecuteScalar(object query)
        {
            var ctx = GetDataConnection() as IDataContext;
            return ctx.ExecuteScalar(query);
        }

        IDataReader IDataContext.ExecuteReader(object query)
        {
            var ctx = GetDataConnection() as IDataContext;
            return ctx.ExecuteReader(query);
        }

        void IDataContext.ReleaseQuery(object query)
        {
            ReleaseQuery();
        }

        SqlProviderFlags IDataContext.SqlProviderFlags => DataProvider.SqlProviderFlags;

        string IDataContext.GetSqlText(object query)
        {
            if (_dataConnection != null)
                return ((IDataContext) _dataConnection).GetSqlText(query);

            var ctx = GetDataConnection() as IDataContext;
            var str = ctx.GetSqlText(query);

            ReleaseQuery();

            return str;
        }

        IDataContext IDataContext.Clone(bool forNestedQuery)
        {
            var dc = new DataContext(0)
            {
                ConfigurationString = ConfigurationString,
                ConnectionString = ConnectionString,
                KeepConnectionAlive = KeepConnectionAlive,
                DataProvider = DataProvider,
                ContextID = ContextID,
                MappingSchema = MappingSchema,
                InlineParameters = InlineParameters
            };

            if (forNestedQuery && _dataConnection != null && _dataConnection.IsMarsEnabled)
                dc._dataConnection = _dataConnection.Transaction != null
                    ? new DataConnection(DataProvider, _dataConnection.Transaction)
                    : new DataConnection(DataProvider, _dataConnection.Connection);

            dc.QueryHints.AddRange(QueryHints);
            dc.NextQueryHints.AddRange(NextQueryHints);

            return dc;
        }

        public event EventHandler OnClosing;

        void IDisposable.Dispose()
        {
            if (_dataConnection != null)
            {
                if (OnClosing != null)
                    OnClosing(this, EventArgs.Empty);

                if (_dataConnection.QueryHints.Count > 0) QueryHints.AddRange(_queryHints);
                if (_dataConnection.NextQueryHints.Count > 0) NextQueryHints.AddRange(_nextQueryHints);

                _dataConnection.Dispose();
                _dataConnection = null;
            }
        }

        internal DataConnection GetDataConnection()
        {
            if (_dataConnection == null)
            {
                _dataConnection = ConnectionString != null
                    ? new DataConnection(DataProvider, ConnectionString)
                    : new DataConnection(ConfigurationString);

                if (_queryHints != null && _queryHints.Count > 0)
                {
                    _dataConnection.QueryHints.AddRange(_queryHints);
                    _queryHints = null;
                }

                if (_nextQueryHints != null && _nextQueryHints.Count > 0)
                {
                    _dataConnection.NextQueryHints.AddRange(_nextQueryHints);
                    _nextQueryHints = null;
                }
            }

            return _dataConnection;
        }

        internal void ReleaseQuery()
        {
            if (_dataConnection != null)
            {
                LastQuery = _dataConnection.LastQuery;

                if (LockDbManagerCounter == 0 && KeepConnectionAlive == false)
                {
                    if (_dataConnection.QueryHints.Count > 0) QueryHints.AddRange(_queryHints);
                    if (_dataConnection.NextQueryHints.Count > 0) NextQueryHints.AddRange(_nextQueryHints);

                    _dataConnection.Dispose();
                    _dataConnection = null;
                }
            }
        }

        public virtual DataContextTransaction BeginTransaction(IsolationLevel level)
        {
            var dct = new DataContextTransaction(this);

            dct.BeginTransaction(level);

            return dct;
        }

        public virtual DataContextTransaction BeginTransaction(bool autoCommitOnDispose = true)
        {
            var dct = new DataContextTransaction(this);

            dct.BeginTransaction();

            return dct;
        }
    }
}