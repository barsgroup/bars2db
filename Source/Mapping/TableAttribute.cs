﻿using System;

namespace Bars2Db.Mapping
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
    public class TableAttribute : Attribute
    {
        public TableAttribute()
        {
            IsColumnAttributeRequired = true;
        }

        public TableAttribute(string tableName) : this()
        {
            Name = tableName;
        }

        public string Configuration { get; set; }
        public string Name { get; set; }
        public string Schema { get; set; }
        public string Database { get; set; }
        public bool IsColumnAttributeRequired { get; set; }
    }
}