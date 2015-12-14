﻿using System;
using System.Data.SqlClient;
using System.Linq;

using LinqToDB.Data;
using LinqToDB.Mapping;

using Microsoft.SqlServer.Types;

namespace TestApp
{
	class Program
	{
		[Table(Database="TestData", Name="AllTypes2")]
		public class AllTypes2
		{
			[Column(DbType="int"),   PrimaryKey, Identity] public int             ID                     { get; set; } // int
			[Column(DbType="date"),              Nullable] public DateTime?       dateDataType           { get; set; } // date
			[Column(DbType="datetimeoffset(7)"), Nullable] public DateTimeOffset? datetimeoffsetDataType { get; set; } // datetimeoffset(7)
			[Column(DbType="datetime2(7)"),      Nullable] public DateTime?       datetime2DataType      { get; set; } // datetime2(7)
			[Column(DbType="time(7)"),           Nullable] public TimeSpan?       timeDataType           { get; set; } // time(7)
			[Column(DbType="hierarchyid"),       Nullable] public SqlHierarchyId  hierarchyidDataType    { get; set; } // hierarchyid
			[Column(DbType="geography"),         Nullable] public SqlGeography    geographyDataType      { get; set; } // geography
			[Column(DbType="geometry"),          Nullable] public SqlGeometry     geometryDataType       { get; set; } // geometry
		}

		static void Main(string[] args)
		{
			SqlServerTypes.Utilities.LoadNativeAssemblies(AppDomain.CurrentDomain.BaseDirectory);

			using (var db = new DataConnection())
			{
				var list = db.GetTable<AllTypes2>().ToList();
			}
		}
	}
}
