using Microsoft.EntityFrameworkCore;
using ProtonImport.DataContext;
using ProtonImport.Proton;
using Serilog;
using System.Data;
using System.Diagnostics;

namespace ProtonImport.ProtonToSql
{

    internal class ValuesLoader : IDisposable
    {
        static readonly Dictionary<int, AttributeFn?> AttributeFns = [];

        private readonly Vrx vrx = new();
        private readonly Patsts patsts = new();
        private readonly Data data = new();
        private readonly FrText frText = new();
        private readonly IndexReader indexReader = new();

        private readonly IValueTableUtilities[] tableUtilities ;

        private int abnormalNumbers = 0;

        public struct ValueIndex
        {
            public int EntityId { get; set; }
            public short AttributeId { get; set; }
            public short Seq { get; set; }
        }

        public long NEntities { get; private set; }
        public long NDataPages { get; private set; }
        public int DataPageCount => data.PageCounter;
        public int EntitiesLoaded { get; private set; } = 0;
        public int EntitiesUpdated { get; private set; } = 0;
        public int EntitiesSkipped { get; private set; } = 0;

        public ValuesLoader()
        {
            tableUtilities = new IValueTableUtilities[Enum.GetNames<ValueTable>().Length];

            tableUtilities[(int)ValueTable.ValueTexts] = new ValueTableUtilities<ValueText>();
            tableUtilities[(int)ValueTable.ValueLongTexts] = new ValueTableUtilities<ValueLongText>();
            tableUtilities[(int)ValueTable.ValueNumbers] = new ValueTableUtilities<ValueNumber>();
            tableUtilities[(int)ValueTable.ValueDates] = new ValueTableUtilities<ValueDate>();
            tableUtilities[(int)ValueTable.ValueTimes] = new ValueTableUtilities<ValueTime>();
            tableUtilities[(int)ValueTable.ValueLookups] = new ValueTableUtilities<ValueLookup>();
            tableUtilities[(int)ValueTable.ValueEntities] = new ValueTableUtilities<ValueEntity>();


            NEntities = vrx.NPages;
            NDataPages = data.NPages;
            AttributeFns.Clear();

            using Proton.Item item = new();
            using Proton.Valid valid = new();
            for (int i = 1; i <=item.NPages; i++)
            {
                if (item.MoveToPage(i) &&
                    item.DataType > 0 &&
                    !Utilities.ConfigurationManager.AppSettings.ExcludeItems.Contains(i)
                    )
                {
                    var qual = false;
                    var mod = false;

                    if (valid.MoveToPage(i))
                    {
                        qual = valid.QualifierCodeType > 0;
                        mod = valid.ModifierCodeType > 0;
                    }

                    AttributeFn? proc = null;
                    proc = item.DataType switch
                    {
                        1 => new(tableUtilities[(short)ValueTable.ValueTexts],
                                                        ProcessString),
                        2 => new(tableUtilities[(short)ValueTable.ValueNumbers],
                                                        (item.SubType == 0) ? ProcessUint8 : ProcessInt8),
                        3 => new(tableUtilities[(short)ValueTable.ValueNumbers],
                                                        (item.SubType == 0) ? ProcessUint16 : ProcessInt16),
                        4 => new(tableUtilities[(short)ValueTable.ValueNumbers],
                                                        (item.SubType == 0) ? ProcessUint32 : ProcessInt32),
                        5 => new(tableUtilities[(short)ValueTable.ValueNumbers],
                                                        (qual || mod) ? ProcessCompositeSingle : ProcessSingle,
                                                        item.SubType, tableUtilities[(short)ValueTable.ValueLookups]),
                        6 => new(tableUtilities[(short)ValueTable.ValueNumbers],
                                                        (qual || mod) ? ProcessCompositeDouble : ProcessDouble,
                                                        item.SubType, tableUtilities[(short)ValueTable.ValueLookups]),
                        7 => new(tableUtilities[(short)ValueTable.ValueLookups],
                                                        ProcessDict),
                        8 => new(tableUtilities[(short)ValueTable.ValueDates],
                                                        ProcessDate),
                        9 => new(tableUtilities[(short)ValueTable.ValueTimes],
                                                        ProcessCompositeTime,
                                                        0, tableUtilities[(short)ValueTable.ValueTexts]),
                        10 => new(tableUtilities[(short)ValueTable.ValueLongTexts],
                                                        ProcessFreeText),
                        11 => new(tableUtilities[(short)ValueTable.ValueTexts],
                                                        ProcessEntityPtr,
                                                        item.SubType, tableUtilities[(short)ValueTable.ValueEntities]),
                        12 => new(tableUtilities[(short)ValueTable.ValueLookups],
                                                        ProcessCode),
                        _ => throw new Exception("unknown datatype " + item.DataType.ToString()),
                    };
                    AttributeFns.Add(i, proc);
                }
            }

        }


        enum ValueTable
        {
            ValueTexts = 0,
            ValueLongTexts = 1,
            ValueNumbers = 2,
            ValueDates = 3,
            ValueTimes = 4,
            ValueLookups = 5,
            ValueEntities = 6,
        }


        /// <summary>
        /// Numeric data in Proton DATA.dbs may be trimmed by removing trailing zeros
        /// This function adds the trailing zero bytes back so it can be converted normally
        /// </summary>
        private static ReadOnlyMemory<byte> GetMemory(ReadOnlyMemory<byte> mem, int length)
        {
            if (mem.Length == length) return mem;

            if (mem.Length > length) return mem[..length];

            //add trailing zeros
            var m = new Memory<byte>(new byte[length]);

            mem.CopyTo(m);

            return m;
        }


        public bool LoadDataRows(int entityId, DateTime? lastUpdated = null)
        {
            if (vrx.MoveToPage(entityId) &&
                patsts.MoveToPage(entityId) &&
                (lastUpdated == null ||  patsts.Updated > lastUpdated))
            {
                ValueIndex valueIndex = new() { EntityId = entityId };

                var start = vrx.FirstDataPageId;
                if (data.MoveToPage(start))
                {
                    valueIndex.AttributeId = 0;
                    AttributeFn? fn = null;
                    bool blockIsValid = true;
                    short skippedAttributeId = 0;
                    while (blockIsValid && data.ItemId > 0)
                    {
                        var newAttributeId = data.ItemId;
                        valueIndex.Seq = data.Seq;
                        if (newAttributeId > valueIndex.AttributeId)
                        {
                            valueIndex.AttributeId = newAttributeId;
                            fn = AttributeFns.GetValueOrDefault(valueIndex.AttributeId);
                        }
                        else if (newAttributeId < valueIndex.AttributeId)
                        {
                            fn = null;
                            if (skippedAttributeId == 0)
                            {
                                Log.Warning($"Items not in ascending order. Entity:{entityId}, any values for item {valueIndex.AttributeId} with rows higher than {valueIndex.Seq} will not be imported. ");

                            }
                            if (skippedAttributeId != newAttributeId)
                            {
                                skippedAttributeId = newAttributeId;
                                Log.Warning($"Skipping item {newAttributeId}.");
                            }
                        }

                        if (fn != null && !data.RawValue.IsEmpty)
                        {
                           fn.Exec(valueIndex, data.RawValue);
                        }
                        blockIsValid = data.MoveToNextBlock();
                    }
                    foreach (IValueTableUtilities u in tableUtilities)
                    {
                        u.AddProcessedEntity(entityId);
                    }
                    if(lastUpdated == null)
                    {
                        EntitiesLoaded++;
                    } else
                    {
                        EntitiesUpdated++;
                    }
                    return true;
                }
            }
            EntitiesSkipped++;
            return false;
        }

        private class AttributeFn(IValueTableUtilities utils, Action<DataRowCollection, ValueIndex, ReadOnlyMemory<byte>, short, DataRowCollection?> action, short subTypeId = 0, IValueTableUtilities? utils2 = null)
        {
            //store immutable parameters. So no need to supply them when calling the Action function.
            private readonly IValueTableUtilities Utils = utils;
            private readonly short SubtypeId = subTypeId;
            private readonly IValueTableUtilities? Utils2 = utils2;

            private readonly Action<DataRowCollection, ValueIndex, ReadOnlyMemory<byte>, short, DataRowCollection?> Action = action;

            public void Exec(ValueIndex valueIndex, ReadOnlyMemory<byte> data)
            {
                try
                {
                    Action!(Utils.DataRows, valueIndex, data, SubtypeId, Utils2?.DataRows);
                }
                catch (Exception e)
                {
                    Log.Error($"Error processing entity {valueIndex.EntityId}, attribute {valueIndex.AttributeId}, seq {valueIndex.Seq}: {e.Message}");
                }
            }
        }

        private void ProcessString(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
           dataRows.Add( valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetString(data));
        }

        private void ProcessUint8(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, data.Span[0]);
        }

        private void ProcessInt8(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt8(data[..1]));
        }

        private void ProcessUint16(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt16(GetMemory(data, 2)));
        }

        private void ProcessInt16(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt16(GetMemory(data, 2)));
        }

        private void ProcessUint32(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt32(GetMemory(data, 4)));
        }

        private void ProcessInt32(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt32(GetMemory(data, 4)));
        }

        /// <summary>
        /// NaN/infinity cannot be stored in SQL and are not real readings, so the value is skipped.
        /// Logged at Debug per value (could be many); a total is logged as a warning at the end of the load.
        /// </summary>
        private bool CheckFloat(Single res, ValueIndex valueIndex)
        {
            if (!Single.IsFinite(res))
            {
                abnormalNumbers++;
                Log.Debug($"Abnormal number {res} skipped, entity:{valueIndex.EntityId}, attribute:{valueIndex.AttributeId}, Seq:{valueIndex.Seq}");
                return false;
            }
            return true;
        }

        private void ProcessSingle(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId, DataRowCollection? dataRows2 = null)
        {

            Single res = ProtonDbFileReader.GetSingle(GetMemory(data, 4));
            if (CheckFloat(res, valueIndex))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, Math.Round(res, subTypeId));
            }
        }

        private void ProcessDouble(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId , DataRowCollection? dataRows2 = null)
        {
            Single res = ProtonDbFileReader.GetDouble(GetMemory(data, 8));
            if (CheckFloat(res, valueIndex))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, Math.Round(res,subTypeId));
            }
        }

        private void ProcessCompositeSingle(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId, DataRowCollection? dataRows2 = null)
        {
            var com = GetCompositeSingle(data);
            if (com?.Number != null && CheckFloat((Single)com.Number!, valueIndex))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, Math.Round((Single)com.Number, subTypeId));
            }
            if (com?.QualifierCodeId > 0)
            {
                dataRows2?.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.QualifierCodeId);
            }
        }

        private void ProcessCompositeDouble(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId, DataRowCollection? dataRows2 = null)
        {
            var com = GetCompositeDouble(data);

            if (com?.Number != null && CheckFloat((Single)com.Number!, valueIndex))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, Math.Round((Single)com.Number, subTypeId));
            }
            if (com?.QualifierCodeId > 0)
            {
                dataRows2?.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.QualifierCodeId);
            }
        }

        private void ProcessDict(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            var id = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (id > 0)
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, -id);
            }
        }

        private void ProcessCode(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0)
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, id);
            }
        }

        private void ProcessDate(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            var dt = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (dt > 0)
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.ProtonBaseDate.AddDays(dt));
            }
        }

        private void ProcessCompositeTime(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            var com = GetCompositeTime(data);
            if (com.ReplacementText == "")
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.Time);
            }
            else
            {
                dataRows2?.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.ReplacementText);
            }
        }

        private void ProcessFreeText(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0 && frText.MoveToPage(id))
            {
                if (frText.EntityId == valueIndex.EntityId)
                {
                    dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, frText.Text(id));
                }
            }
        }

        private void ProcessEntityPtr(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId = 0, DataRowCollection? dataRows2 = null)
        {
            var key = ProtonDbFileReader.GetString(data);
            if (! string.IsNullOrEmpty(key))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, key);

                var eId = indexReader.GetEntityId(subTypeId, key);

                if (eId > 0)
                {
                    dataRows2!.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, eId);
                }
            }
        }

        private static CompositeFloat? GetCompositeSingle(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 1 && data.Span[0] == 0) return null;
            CompositeFloat cf = new();
            if (data.Length > 8)
            {
                cf.QualifierCodeId = ProtonDbFileReader.GetInt32(GetMemory(data[8..], 4));

                if (cf.QualifierCodeId > 0)
                {
                    int qr = ProtonDbFileReader.GetInt16(data.Slice(4, 2));
                    if (qr == 1)
                    {
                        return cf;
                    }
                }
            }
            cf.Number = ProtonDbFileReader.GetSingle(GetMemory(data, 4));
           
            return cf;
        }

        private static CompositeFloat? GetCompositeDouble(ReadOnlyMemory<byte> data)
        {
            if (data.Length == 1 && data.Span[0] == 0) return null;
            CompositeFloat cf = new();
            if (data.Length > 12)
            {
                cf.QualifierCodeId = ProtonDbFileReader.GetInt32(GetMemory(data[12..], 4));
                if (cf.QualifierCodeId > 0)
                {
                    int qr = ProtonDbFileReader.GetInt16(data[8..10]);
                    if (qr == 1)
                    {
                        return cf;
                    }
                }
            }
          
            cf.Number = (Single?)ProtonDbFileReader.GetDouble(GetMemory(data, 8));
            
            return cf;
        }

        private static CompositeTime GetCompositeTime(ReadOnlyMemory<byte> data)
        {
            var timeOffset = ProtonDbFileReader.GetUInt32(GetMemory(data, 4));

            return timeOffset switch
            {
                0x20000000 => new CompositeTime() { ReplacementText = "PRE" },
                0x40000000 => new CompositeTime() { ReplacementText = "POST" },
                0x80000000 => new CompositeTime() { Time = TimeOnly.MinValue, ReplacementText = "" },
                _ => new CompositeTime() { Time = TimeOnly.MinValue.AddMinutes(timeOffset / 60000), ReplacementText = "" },
            };
        }


        /// <summary>
        /// Rows per table per batch. Large batches keep the number of SQL round trips and
        /// staging syncs low; memory is roughly two batches per value table.
        /// </summary>
        public const int DefaultBatchRows = 50_000;

        /// <summary>
        /// Full import/update pipeline: values, then indexes, then entities (with their names).
        /// Used by both the scheduled (no-argument) run and the settings menu.
        /// </summary>
        public static void ImportAll(int nRows = DefaultBatchRows)
        {
            if (!Utilities.ConfigurationManager.AppSettings.NoLoad)
            {
                DatabaseSchema.Apply();
            }
            using ValuesLoader dsl = new();
            dsl.LoadValues(nRows);
            if (!Utilities.ConfigurationManager.AppSettings.NoLoad &&
                (dsl.EntitiesLoaded > 0 || dsl.EntitiesUpdated > 0))
            {
                EntityLoader.LoadIndexes(nRows);
                EntityLoader.LoadEntities(nRows);
            }
        }

        public void LoadValues(int nRows)
        {
            var st = Stopwatch.StartNew();
            EntitiesSkipped = 0;
            EntitiesLoaded = 0;
            EntitiesUpdated = 0;
            abnormalNumbers = 0;
            bool noLoad = Utilities.ConfigurationManager.AppSettings.NoLoad;

            foreach (IValueTableUtilities u in tableUtilities)
            {
                u.SwitchTables();
            }

            int counter = 0;

            using Proton2Context ctx = new();

            // null => first load: every entity is read. Otherwise only entities changed since the last import.
            DateTime? latest = null;
            if (ctx.Entities.Any())
            {
                latest = ctx.Entities.Max(e => e.LastUpdated);
            }

            if (!noLoad)
            {
                ctx.Database.ExecuteSql($"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
            }

            long nEntities = 0;
            var prog = new Utilities.Progress(20);

            // the batch each table is currently writing to SQL in the background (one per table)
            Task[] pending = [.. tableUtilities.Select(_ => Task.CompletedTask)];

            Action<int> loadFunction = noLoad ? Noload : Process;

            if (Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities.Count == 0)
            {
                nEntities = vrx.NPages;
                Log.Information($"Loading values for {nEntities} entities..");
                prog.WriteProgressBar(0);

                for (int i = 1; i <= nEntities; i++)
                {
                    loadFunction(i);
                }
            }
            else
            {
                nEntities = Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities.Count;
                Log.Information($"Loading values for {nEntities} entities..");
                prog.WriteProgressBar(0);
                foreach (int i in Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities)
                {
                    loadFunction(i);
                }
            }

            if (!noLoad)
            {
                // wait for batches still being written, then send what remains in every table
                Task.WaitAll(pending);
                for (int t = 0; t < tableUtilities.Length; t++)
                {
                    var u = tableUtilities[t];
                    u.SwitchTables();
                    pending[t] = Task.Run(() =>
                    {
                        u.BulkLoad();
                        u.SyncFromStaging();
                    });
                }
                Task.WaitAll(pending);

                foreach (IValueTableUtilities u in tableUtilities)
                {
                    u.DropStaging();
                }
            }

            prog.WriteProgressBar(1);
            Console.WriteLine();
            st.Stop();
            if (abnormalNumbers > 0)
            {
                Log.Warning($"{abnormalNumbers} abnormal numbers (NaN/infinity) skipped; see Debug log entries for details");
            }
            if (noLoad)
            {
                Log.Information($"{counter} entities scanned in {st.Elapsed:hh\\:mm\\:ss}");
            }
            else
            {
                Log.Information($"{EntitiesLoaded} Entities loaded");
                Log.Information($"{EntitiesUpdated} Entities updated");
                Log.Information($"{EntitiesSkipped} Entities skipped (already up to date)");
                Log.Information($"in {st.Elapsed:hh\\:mm\\:ss}");
            }

            void Noload(int i)
            {
                counter++;
                LoadDataRows(i, latest);
                // scan only: discard what was read so nothing accumulates or reaches SQL
                foreach (IValueTableUtilities u in tableUtilities)
                {
                    u.ClearReading();
                }
                prog.WriteProgressBar(counter / (float)nEntities);
            }


            void Process(int i)
            {
                counter++;

                for (int t = 0; t < tableUtilities.Length; t++)
                {
                    var u = tableUtilities[t];
                    if (u.DataRows.Count >= nRows)
                    {
                        // Hand the full table to a background writer and keep reading.
                        // At most one batch per table is in flight: wait for the previous one first.
                        pending[t].Wait();
                        u.SwitchTables();
                        pending[t] = Task.Run(() =>
                        {
                            u.BulkLoad();
                            u.SyncFromStaging();
                        });
                    }
                }

                LoadDataRows(i, latest);

                prog.WriteProgressBar(counter / (float)nEntities);
            }
        }


        private class CompositeTime
        {
            public TimeOnly Time { get; set; }
            public string ReplacementText { get; set; } = "";
            public string ToString(string format)
            {
                if (ReplacementText == "")
                {
                    return Time.ToString(format);
                }
                else return ReplacementText;
            }
        }

        private class CompositeFloat
        {
            public Single? Number { get; set; }
            public int QualifierCodeId { get; set; }
        }
        public void Dispose()
        {
            vrx.Dispose();
            frText.Dispose();
            data.Dispose();
            patsts.Dispose();
            indexReader.Dispose();

            foreach (IValueTableUtilities util in tableUtilities)
            {
               util.Dispose();
            }
        }
    }
}

