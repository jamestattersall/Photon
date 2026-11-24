using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.protonToSql
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
            var tPages = vrx.NPages;
            for (int i = 1; i <= tPages; i++)
            {
                if (patsts.MoveToPage(i) && vrx.MoveToPage(i))
                {
       
                    tableUtilities.DataRows.Add(i, 0, "", patsts.Updated);
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

                    int oldEntityId = 0;
                    byte[] bytes = new byte[keyLength];
                    Memory<byte> oldKeyText = new(bytes);
                    while (index.MoveToNextBlock())
                    {
                        var keyText = index.KeyTextRaw;
                        var entityId = index.EntityId;
                        if(
                           // !keyText.IsNullOrEmpty() && 
                            !keyText.Span.SequenceEqual(oldKeyText.Span) && 
                            !(entityId == oldEntityId)
                            )
                        {
                            keyText.CopyTo(oldKeyText);
                            oldEntityId = entityId;
                            tableUtilities.DataRows.Add( i, ProtonDbFileReader.GetString(keyText), entityId);
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

        public static void UpdateEntityNames()
        {
            string sql = @"
UPDATE Entities
SET Name=i.Term, EntityTypeId=a.EntityTypeId
FROM Attributes a
INNER JOIN EntityTypes et on et.IdAttributeId=a.Id
INNER JOIN Indexes i ON i.IndexTypeId=et.DefaultIndexTypeId 
INNER JOIN Entities e ON e.Id=i.EntityId";
            using Proton2Context ctx = new();
            ctx.Database.ExecuteSqlRaw(sql);
        }
    }
}
