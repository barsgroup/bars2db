﻿namespace LinqToDB.SqlEntities
{
    using System;

    partial class Sql
    {
        [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
        public class EnumAttribute : Attribute
        {
        }
    }
}
