using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using ProtonImport.DataContext;
using Serilog;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EntityType = Microsoft.EntityFrameworkCore.Metadata.Internal.EntityType;

namespace ProtonImport.ProtonToSql
{

    interface IValueTableUtilities : IDisposable
    {
        void SyncFromStaging();
        void BulkLoad();
        void SwitchTables();
        void MergeTables();
        DataRowCollection DataRows { get;  }

        /// <summary>
        /// Record that an entity has been fully re-read into the current batch, so its stale rows
        /// can be deleted even if it now has no rows in this table.
        /// </summary>
        void AddProcessedEntity(int entityId);

        /// <summary>Discard rows read so far without sending them to SQL.</summary>
        void ClearReading();
    }

    public static class MyExtensions
    {
        public static IQueryable<object> Set(this DbContext _context, Type t)
        {
            // Get the "Set" method and ensure it is not null
            var setMethod = _context.GetType().GetMethod("Set", Type.EmptyTypes) ?? throw new InvalidOperationException($"Could not find 'Set' method on type '{_context.GetType().FullName}'.");

            // Make the generic method and ensure it is not null
            var genericSetMethod = setMethod.MakeGenericMethod(t) ?? throw new InvalidOperationException($"Could not make generic 'Set' method for type '{t.FullName}'.");

            // Invoke the method and check for null result
            var result = genericSetMethod.Invoke(_context, null) ?? throw new InvalidOperationException($"Invocation of 'Set<{t.FullName}>' returned null.");
            return (IQueryable<object>)result;
        }
    }

    internal abstract class TableUtilities<T> : IDisposable, IValueTableUtilities where T : class 
    {
        protected static readonly string tableName;
        protected static readonly string SqlCreateStagingTable;
        protected static readonly string SqlDeleteStagingTable;
        protected static readonly string sqlCreatePk;
        protected static readonly string SqlUpsert;
        protected static  string? sqlSyncFromStaging;
        protected static PropertyInfo[] propertyInfos;
        protected static List<string> nonKeyColumnNames = [];
        protected static List<string> keyColumnNames = [];
        protected static string stagingTableName;
        protected static DataTable emptyDataTable = new();
        public SqlConnection cnn;
        public SqlBulkCopy sqlBulkCopy;
        protected SqlCommand cdCreateStagingTable;
        protected SqlCommand cdSyncFromStaging;
        protected DataTable dtReading = new();
        protected DataTable dtWriting = new();  //used for asynchronous loads with simultaneous reading from Proton .dbs and writing to SQL db 
        protected bool NeedsStagingTable = true;
        protected bool forSync = true;
        protected bool loadFailed = false;  // set when a bulk write fails, so the following sync is skipped
        protected static Proton2Context ctx = new();


        static TableUtilities()
        {
            var t = typeof(T);

            var entityType = ctx.Model.FindEntityType(t);
            string? tn = null;
            if (entityType != null)
            {
                tn = Microsoft.EntityFrameworkCore.RelationalEntityTypeExtensions.GetTableName(entityType);
            }
            if (tn == null) throw new Exception($"Type {t.FullName} not in datasets");
            tableName = tn;
            stagingTableName = Sql.StagingTableName(tableName);


            //exclude ref properties apart from strings properties, order by derived properties first.
            propertyInfos = [.. t.GetProperties().Where(p => !p.PropertyType.IsClass || p.PropertyType == typeof(string)).OrderBy(o => o.DeclaringType == t)];

            System.Attribute[] attrs = System.Attribute.GetCustomAttributes(t);

            foreach (System.Attribute attr in attrs)
            {
                if (attr is PrimaryKeyAttribute a)
                {
                    keyColumnNames = [.. a.PropertyNames.Select(n => $"[{n}]")];
                }
            }

            if (keyColumnNames.Count == 0)
            {
                foreach (PropertyInfo p in propertyInfos)
                {
                    foreach (System.Attribute attr in p.GetCustomAttributes())
                    {
                        if (attr is KeyAttribute a)
                        {
                            keyColumnNames.Add($"[{p.Name}]");
                        }
                    }
                }
            }

            foreach (PropertyInfo p in propertyInfos)
            {
                var sname = $"[{p.Name}]";
                if (!keyColumnNames.Contains(sname))
                {
                    nonKeyColumnNames.Add(sname);
                }
            }
            
            // Simplified object initialization for DataColumn in static TableUtilities<T> constructor
            emptyDataTable = new DataTable();
            foreach (var pi in propertyInfos)
            {
                emptyDataTable.Columns.Add(new DataColumn(pi.Name, Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType));
            }

            SqlCreateStagingTable = Sql.SqlCreateStagingTable(tableName);
            sqlCreatePk = Sql.SqlCreateStagingPrimaryKey(tableName, [.. keyColumnNames]);
            SqlUpsert = Sql.SqlUpsert(tableName, [.. keyColumnNames], [.. nonKeyColumnNames]);
            SqlDeleteStagingTable = Sql.SqlDeleteStagingTable(tableName);
        }

        public TableUtilities()
        {
            cnn = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            sqlBulkCopy = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString(), SqlBulkCopyOptions.TableLock);
            if (cnn.State != ConnectionState.Open) cnn.Open();

            dtReading = emptyDataTable.Clone();
            NeedsStagingTable = true;
            cdCreateStagingTable = new SqlCommand(Sql.SqlCreateStagingTable(tableName), cnn)
            {
                CommandTimeout = 500
            };
            cdSyncFromStaging = new SqlCommand(sqlSyncFromStaging, cnn)
            {
                CommandTimeout = 500
            };

            forSync = ctx.Set<T>().Any();

        }

        public virtual void SwitchTables()
        {
            dtWriting = dtReading.Copy();
            dtReading.Clear();
        }

        public void MergeTables()
        {
            dtWriting = dtReading;
        }

        public virtual void AddProcessedEntity(int entityId) { }

        public virtual void ClearReading() => dtReading.Clear();

        private void CreateDataTable(List<T> values)
        {
            foreach (T val in values)
            {
                var vals = new object[propertyInfos.Length];
                for (int i = 0; i < propertyInfos.Length; i++)
                {
                    vals[i] = propertyInfos[i].GetValue(val)!;
                }
                dtReading.Rows.Add(vals);
            }
        }

        public void BulkLoad(List<T> values)
        {
            CreateDataTable(values);
            BulkLoad();
            if(forSync) SyncFromStaging();
        }

        public DataRowCollection DataRows => dtReading.Rows;
  
        public virtual void BulkLoad()
        {

            if (forSync && NeedsStagingTable) CreateStagingTable();

            try
            {
                sqlBulkCopy.DestinationTableName = forSync ? stagingTableName : tableName;
                sqlBulkCopy.WriteToServer(dtWriting);
            }
            catch (Exception ex)
            {
                loadFailed = true;
                Log.Error(ex, $"Unable to write {sqlBulkCopy.DestinationTableName} to server.");
            }
        }

        protected virtual void CreateStagingTable()
        {
            try
            {
                if (cnn.State != ConnectionState.Open)  cnn.Open();

                cdCreateStagingTable.ExecuteNonQuery();
                NeedsStagingTable = false;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, @$"Unable to create {stagingTableName}");
            }
        }

        public void SyncFromStaging()
        {
            if (loadFailed)
            {
                // staging holds an incomplete batch; syncing it would delete rows that failed to load
                Log.Error($"Skipping sync of {stagingTableName} because the bulk write failed. Rows in this batch were not imported.");
                loadFailed = false;
                NeedsStagingTable = true;   // recreating the staging table truncates it
                return;
            }
            if (forSync)
            {
                if (cnn.State != ConnectionState.Open) cnn.Open();
                try
                {
                    cdSyncFromStaging.ExecuteNonQuery();
                    NeedsStagingTable = true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, @$"Unable to sync {stagingTableName}
                        {sqlSyncFromStaging}
                    ");
                }
            }
        }

        public virtual void Dispose()
        {
            cnn.Dispose();
            sqlBulkCopy.Close();
            cdCreateStagingTable.Dispose();
            cdSyncFromStaging.Dispose();
        }

    }


    internal class ValueTableUtilities<T> : TableUtilities<T> where T : class
    {
        private static readonly string idsTableName = Sql.StagingEntityIdsTableName(tableName);
        private readonly SqlBulkCopy idsBulkCopy;
        private readonly SqlCommand cdCreateIdsTable;
        private DataTable idsReading = NewIdsTable();
        private DataTable idsWriting = NewIdsTable();

        static ValueTableUtilities()
        {
            string sqlDeleteFromStaging = Sql.SqlValuesDeleteFromStaging(tableName, [.. keyColumnNames]);

            StringBuilder sb = new();
            sb.AppendLine(sqlCreatePk);
            sb.AppendLine(sqlDeleteFromStaging);
            sb.AppendLine(SqlUpsert);
            sb.AppendLine(SqlDeleteStagingTable);
            sb.AppendLine(Sql.SqlDeleteStagingEntityIdsTable(tableName));
            sqlSyncFromStaging = sb.ToString();
        }

        private static DataTable NewIdsTable()
        {
            DataTable dt = new();
            dt.Columns.Add("EntityId", typeof(int));
            return dt;
        }

        public ValueTableUtilities()
        {
            SwitchTables();
            idsBulkCopy = new(cnn) { DestinationTableName = idsTableName };
            cdCreateIdsTable = new SqlCommand(Sql.SqlCreateStagingEntityIdsTable(tableName), cnn)
            {
                CommandTimeout = 500
            };
        }

        public override void AddProcessedEntity(int entityId) => idsReading.Rows.Add(entityId);

        public override void ClearReading()
        {
            dtReading.Clear();
            idsReading.Clear();
        }

        public override void SwitchTables()
        {
            base.SwitchTables();
            idsWriting = idsReading;
            idsReading = NewIdsTable();
        }

        protected override void CreateStagingTable()
        {
            base.CreateStagingTable();
            try
            {
                cdCreateIdsTable.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                loadFailed = true;
                Log.Fatal(ex, $"Unable to create {idsTableName}");
            }
        }

        public override void BulkLoad()
        {
            base.BulkLoad();
            if (!forSync || loadFailed) return;
            try
            {
                idsBulkCopy.WriteToServer(idsWriting);
            }
            catch (Exception ex)
            {
                loadFailed = true;
                Log.Error(ex, $"Unable to write {idsTableName} to server.");
            }
        }

        public override void Dispose()
        {
            idsBulkCopy.Close();
            cdCreateIdsTable.Dispose();
            base.Dispose();
        }
    }

    internal class MetaTableUtilities<T> : TableUtilities<T> where T : class
    {
        public SqlCommand cdTruncateTable;

        static MetaTableUtilities()
        {
            string sqlDeleteFromStaging = Sql.SqlMetadataDeleteFromStaging(tableName, [.. keyColumnNames]);

            StringBuilder sb = new();
            sb.AppendLine(sqlCreatePk);
            sb.AppendLine(sqlDeleteFromStaging);
            sb.AppendLine(SqlUpsert);
            sb.AppendLine(SqlDeleteStagingTable);
            sqlSyncFromStaging = sb.ToString();

        }

        public MetaTableUtilities()
        {
            dtWriting = dtReading; //disable async
            cdTruncateTable = new SqlCommand(Sql.SqlTruncateTable(tableName), cnn)
            {
                CommandTimeout = 500
            };
        }

        public void TruncateTable()
        {
            cdTruncateTable.ExecuteNonQuery();
            forSync = false;
        }
    }
}
