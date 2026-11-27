using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProtonImport.DataContext;
using ProtonImport.Proton;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonImport.ProtonToSql
{
    internal static class EntityLoader
    {
        public static void LoadLookups(int nRows)
        {

            using MetaTableUtilities<Lookup> tableUtilities = new ();
        
            using Proton.Dict dict = new();
            using Proton.RCode code = new();
            using Proton2Context ctx = new();

            int c = 0;
            var prog = new Utilities.Progress(20);
        
            Log.Information("Loading Lookups..");
            
            prog.WriteProgressBar(0);
            var nDicts = dict.NPages;
            var tPages = nDicts + code.NPages;
            for (int i = 1; i <= dict.NPages; i++)
            {
                if (dict.MoveToPage(i))
                {
                    tableUtilities.DataRows.Add((-i), -1, dict.Name, "");
                
                    c++;
                    if (c > nRows)
                    {
                        tableUtilities.BulkLoad();
                        tableUtilities.DataRows.Clear();
                        c = 0;

                        prog.WriteProgressBar(i / (float)tPages);
                    }
                }
            }
            for (int i = 1; i <= code.NPages; i++)
            {
                if (code.MoveToPage(i))
                {
                    tableUtilities.DataRows.Add(i, code.CodeTypeID, code.Name, code.ReadCode);
                    c++;
                    if (c > nRows)
                    {
                        tableUtilities.BulkLoad();
                        tableUtilities.DataRows.Clear();
                        c = 0;

                        prog.WriteProgressBar((i + nDicts) / (float)tPages);
                    }
                }
            }
            tableUtilities.BulkLoad();

            tableUtilities.SyncFromStaging();
           
            prog.WriteProgressBar(1);
        }


        public static void LoadEntities(int nRows)
        {
            using MetaTableUtilities<DataContext.Entity> tableUtilities = new();
            
            using Proton.Patsts patsts = new();
            using Proton.Vrx vrx = new();
            using Proton2Context ctx = new();
            int nEntities = ctx.Entities.Count();
            bool exists = nEntities > 0;
           

            int c = 0;
            int maxId = exists ? ctx.Entities.Max(e => e.Id) : 0;
            var prog = new Utilities.Progress(20);
            if (exists)
            {
                Log.Information("Updating Entities..");
            }
            else Log.Information("Loading Entities..");
            prog.WriteProgressBar(0);
            var names = GetEntityNames();
            var tPages = vrx.NPages;
            for (int i = 1; i <= tPages; i++)
            {
                if (patsts.MoveToPage(i) && vrx.MoveToPage(i))
                {
                    // stage the real name and type, so the upsert only touches entities that changed
                    var (entityTypeId, name) = names.GetValueOrDefault(i, ((short)0, ""));
                    tableUtilities.DataRows.Add(i, entityTypeId, name, patsts.Updated);
                    if (exists && i > maxId)
                    {
                       exists = false;
                    }
                    c++;
                    if (c > nRows)
                    {
                        tableUtilities.BulkLoad();
                        tableUtilities.DataRows.Clear();
                        c = 0;
                        
                        prog.WriteProgressBar(i / (float)tPages);
                    }
                }
            }
            tableUtilities.BulkLoad();
            tableUtilities.SyncFromStaging();

            prog.WriteProgressBar(1);
            Console.WriteLine();
            if (ctx.Entities.Any())
            {
                var updated = ctx.Entities.Max(e => e.LastUpdated);
                Log.Information($"{ctx.Entities.Count() - nEntities} new entities loaded");
                Log.Information($"Entities updated to {updated}");
            }

        }


        public static void LoadIndexes(int nRows)
        {
            using MetaTableUtilities<DataContext.Index> tableUtilities = new();
            
            using Proton.Index index = new();
            using Proton.KeyDef keydef = new();
            using Proton.IndexDef indexdef = new();
            using Proton2Context ctx = new();


            var prog = new Utilities.Progress(20);
    
            int c = 0;
            int tc = 0;
            int cc = 0;

           Log.Information("Deleting old indexes..");
            tableUtilities.TruncateTable();

            for (int i = 1; i <= index.NPages; i++)
            {
                if (index.MoveToPage(i))
                {
                    tc += index.BlocksOnPage;
                }
            }


            Log.Information("Loading indexes..");
            prog.WriteProgressBar(0);
            for (short i = 1; i <= indexdef.NPages; i++)
            {
                if (indexdef.MoveToPage(i) && index.MoveToPage(indexdef.IndexIdStart))
                {
                    var keyLength = indexdef.KeyLength;

                    index.SetBlockLength(keyLength);

                    // Skip only exact (term, entity) duplicates, which would break the Indexes primary key.
                    // The same term may belong to several entities (e.g. two patients called SMITH)
                    // and one entity may have several terms. Normalised like SQL compares terms
                    // (case-insensitive, trailing spaces ignored).
                    HashSet<(string Term, int EntityId)> seen = [];
                    while (index.MoveToNextBlock())
                    {
                        var term = ProtonDbFileReader.GetString(index.KeyTextRaw);
                        var entityId = index.EntityId;
                        if (seen.Add((term.TrimEnd().ToUpperInvariant(), entityId)))
                        {
                            tableUtilities.DataRows.Add( i, term, entityId);
                            c++;
                            cc++;
                            if (c > nRows)
                            {
                                tableUtilities.BulkLoad();
                                tableUtilities.DataRows.Clear();
                                c = 0;
                                prog.WriteProgressBar((float)cc / (float)tc);
                            }
                        }
                    }
                }
            }
            tableUtilities.BulkLoad(); 
            tableUtilities.SyncFromStaging();
            prog.WriteProgressBar(1);
            Console.WriteLine();

        }

        /// <summary>
        /// Name (term in its entity type's default index) and entity type for every indexed entity.
        /// Requires Indexes to be loaded first. If an entity has several terms, the first in
        /// term order is used, so the choice is the same on every run.
        /// </summary>
        private static Dictionary<int, (short EntityTypeId, string Name)> GetEntityNames()
        {
            const string sql = @"
SELECT i.EntityId, a.EntityTypeId, i.Term
FROM Attributes a
INNER JOIN EntityTypes et ON et.IdAttributeId = a.Id
INNER JOIN Indexes i ON i.IndexTypeId = et.DefaultIndexTypeId
ORDER BY i.EntityId, i.Term";

            Dictionary<int, (short, string)> names = [];
            using SqlConnection cnn = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            cnn.Open();
            using SqlCommand cmd = new(sql, cnn) { CommandTimeout = 500 };
            using SqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                names.TryAdd(reader.GetInt32(0), (reader.GetInt16(1), reader.GetString(2)));
            }
            return names;
        }
    }
}
