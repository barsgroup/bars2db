namespace Bars2Db.Common
{
    public static class Configuration
    {
        public static bool IsStructIsScalarType = true;
        public static bool AvoidSpecificDataProviderAPI;

        public static class Linq
        {
            public static bool PreloadGroups = true;
            public static bool IgnoreEmptyUpdate;
            public static bool AllowMultipleQuery;
            public static bool GenerateExpressionTest;
        }
    }
}