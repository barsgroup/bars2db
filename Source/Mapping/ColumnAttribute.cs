﻿using System;

namespace Bars2Db.Mapping
{
    /// <summary>
    ///     Associates a class with a column in a database table.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Interface,
        AllowMultiple = true)]
    public class ColumnAttribute : Attribute
    {
        private bool? _canBeNull;

        private bool? _isIdentity;

        private bool? _isPrimaryKey;

        private int? _length;

        private int? _precision;

        private int? _scale;

        private bool? _skipOnInsert;

        private bool? _skipOnUpdate;

        public ColumnAttribute()
        {
            IsColumn = true;
        }

        public ColumnAttribute(string columnName) : this()
        {
            Name = columnName;
        }

        public ColumnAttribute(string columnName, string memberName) : this()
        {
            Name = columnName;
            MemberName = memberName;
        }

        internal ColumnAttribute(string memberName, ColumnAttribute ca)
            : this(ca)
        {
            MemberName = memberName + "." + ca.MemberName.TrimStart('.');
        }

        internal ColumnAttribute(ColumnAttribute ca)
        {
            MemberName = ca.MemberName;
            Configuration = ca.Configuration;
            Name = ca.Name;
            DataType = ca.DataType;
            DbType = ca.DbType;
            Storage = ca.Storage;
            IsDiscriminator = ca.IsDiscriminator;
            PrimaryKeyOrder = ca.PrimaryKeyOrder;
            IsColumn = ca.IsColumn;
            CreateFormat = ca.CreateFormat;

            if (ca.HasSkipOnInsert()) SkipOnInsert = ca.SkipOnInsert;
            if (ca.HasSkipOnUpdate()) SkipOnUpdate = ca.SkipOnUpdate;
            if (ca.HasCanBeNull()) CanBeNull = ca.CanBeNull;
            if (ca.HasIsIdentity()) IsIdentity = ca.IsIdentity;
            if (ca.HasIsPrimaryKey()) IsPrimaryKey = ca.IsPrimaryKey;
            if (ca.HasLength()) Length = ca.Length;
            if (ca.HasPrecision()) Precision = ca.Precision;
            if (ca.HasScale()) Scale = ca.Scale;
        }

        public string Configuration { get; set; }

        /// <summary>
        ///     Gets or sets the name of a column.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets the name of an associated member name.
        /// </summary>
        public string MemberName { get; set; }

        /// <summary>
        ///     Sets transparent flag to use column without addition id property
        ///     Use it at the same time with AssociationAttribute
        /// </summary>
        public bool Transparent { get; set; }

        /// <summary>
        ///     Gets or sets the type of the database column.
        /// </summary>
        public DataType DataType { get; set; }

        /// <summary>
        ///     Gets or sets the name of the database column type.
        /// </summary>
        public string DbType { get; set; }

        /// <summary>
        ///     Use NonColumnAttribute instead.
        /// </summary>
        public bool IsColumn { get; set; }

        /// <summary>
        ///     Gets or sets a private storage field to hold the value from a column.
        /// </summary>
        public string Storage { get; set; }

        /// <summary>
        ///     Gets or sets whether a column contains a discriminator value for a LINQ to DB inheritance hierarchy.
        /// </summary>
        public bool IsDiscriminator { get; set; }

        /// <summary>
        ///     Gets or sets whether a column is insertable.
        /// </summary>
        public bool SkipOnInsert
        {
            get { return _skipOnInsert ?? false; }
            set { _skipOnInsert = value; }
        }

        /// <summary>
        ///     Gets or sets whether a column is updatable.
        /// </summary>
        public bool SkipOnUpdate
        {
            get { return _skipOnUpdate ?? false; }
            set { _skipOnUpdate = value; }
        }

        /// <summary>
        ///     Gets or sets whether a column contains values that the database auto-generates.
        /// </summary>
        public bool IsIdentity
        {
            get { return _isIdentity ?? false; }
            set { _isIdentity = value; }
        }

        /// <summary>
        ///     Gets or sets whether this class member represents a column that is part or all of the primary key of the table.
        /// </summary>
        public bool IsPrimaryKey
        {
            get { return _isPrimaryKey ?? false; }
            set { _isPrimaryKey = value; }
        }

        /// <summary>
        ///     Gets or sets the Primary Key order.
        /// </summary>
        public int PrimaryKeyOrder { get; set; }

        /// <summary>
        ///     Gets or sets whether a column can contain null values.
        /// </summary>
        public bool CanBeNull
        {
            get { return _canBeNull ?? true; }
            set { _canBeNull = value; }
        }

        /// <summary>
        ///     Gets or sets the length of the database column.
        /// </summary>
        public int Length
        {
            get { return _length ?? 0; }
            set { _length = value; }
        }

        /// <summary>
        ///     Gets or sets the precision of the database column.
        /// </summary>
        public int Precision
        {
            get { return _precision ?? 0; }
            set { _precision = value; }
        }

        /// <summary>
        ///     Gets or sets the Scale of the database column.
        /// </summary>
        public int Scale
        {
            get { return _scale ?? 0; }
            set { _scale = value; }
        }

        public string CreateFormat { get; set; }

        public bool HasSkipOnInsert()
        {
            return _skipOnInsert.HasValue;
        }

        public bool HasSkipOnUpdate()
        {
            return _skipOnUpdate.HasValue;
        }

        public bool HasIsIdentity()
        {
            return _isIdentity.HasValue;
        }

        public bool HasIsPrimaryKey()
        {
            return _isPrimaryKey.HasValue;
        }

        public bool HasCanBeNull()
        {
            return _canBeNull.HasValue;
        }

        public bool HasLength()
        {
            return _length.HasValue;
        }

        public bool HasPrecision()
        {
            return _precision.HasValue;
        }

        public bool HasScale()
        {
            return _scale.HasValue;
        }
    }
}