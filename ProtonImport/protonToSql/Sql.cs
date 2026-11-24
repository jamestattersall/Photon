namespace ProtonConsole2.protonToSql
{
    internal static class Sql
    {
        const string stagingPrefix = "staging_";

        private static string EqualSet(string[] keyColumnNames)
        {
            List<string> res = [];
            foreach (string val in keyColumnNames)
            {
                res.Add($"s.{val} = t.{val}");
            }
            return string.Join(" AND ", res);
        }

        private static string UpdateSet(string[] nonKeyColumnNames)
        {
            List<string> res = [];
            foreach (string val in nonKeyColumnNames)
            {
                res.Add($"{val} = s.{val}");
            }
            return string.Join(", ", res);
        }

        private static string NonEqualSet(string[] nonKeyColumnNames)
        {

            List<string> res = [];
            foreach (string val in nonKeyColumnNames)
            {
                res.Add($"(ISNULL(t.{val},'') <> s.{val})");
            }
            return string.Join(" OR ", res);
        }

        public static string StagingTableName(string tableName) => $@"{stagingPrefix}{tableName}";

        public static string SqlValuesDeleteFromStaging(string tableName, string[] keyColumnNames)
        {

            var stagingTableName = StagingTableName(tableName);
            var joinStr = EqualSet(keyColumnNames);

            return $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s ON {joinStr}
WHERE s.EntityId is null
AND t.EntityId IN (SELECT EntityId FROM {stagingTableName} GROUP BY EntityId)";
        }

        public static string SqlMetadataDeleteFromStaging(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);

            var joinStr = EqualSet(keyColumnNames);

            return $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s 
ON {joinStr}
WHERE s.{keyColumnNames[0]} is null";
        }


        public static string SqlCreateStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
TRUNCATE TABLE [{stagingTableName}]
ELSE
SELECT TOP 0 * 
INTO [{stagingTableName}]
FROM [{tableName}]";
        }

        public static string SqlCreateStagingPrimaryKey(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID(N'PK_staging_{tableName}')IS NULL
ALTER TABLE [{stagingTableName}] ADD CONSTRAINT PK_staging_{tableName} PRIMARY KEY CLUSTERED ({string.Join(", ", keyColumnNames)})";

        }

        public static string SqlUpsert(string tableName, string[] keyColumnNames, string[] nonKeyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);
            var joinStr = EqualSet(keyColumnNames);
            var strNonEqual = NonEqualSet(nonKeyColumnNames);
            var updateStr = UpdateSet(nonKeyColumnNames);


            return $@"
UPDATE [{tableName}]
SET {updateStr}
FROM [{tableName}] t
INNER JOIN [{stagingTableName}] s ON {joinStr}
WHERE ({strNonEqual})

INSERT INTO [{tableName}]
SELECT s.* 
FROM [{stagingTableName}] s
LEFT JOIN [{tableName}] t ON {joinStr}
WHERE t.{keyColumnNames[0]} is null";

        }

        public static string SqlDeleteStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
DROP TABLE [{stagingTableName}]";
        }

        public static string SqlTruncateStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
TRUNCATE TABLE [{stagingTableName}]
";
        }

        public static string SqlTruncateTable(string tablename)
        {
            return $@"
TRUNCATE TABLE [{tablename}]
";
        }

        public static string NoCheckConstraint()
        {
            return $"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'";
        }

        public static string SqlMaxEntityId(string tablename)
        {
            return $"SELECT MAX(EntityId) FROM {tablename}";
        }

     
    }
}
