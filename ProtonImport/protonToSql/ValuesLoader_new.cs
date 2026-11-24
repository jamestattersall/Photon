using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonToSql;
using Serilog;
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace ProtonConsole2.protonToSql
{

    internal class ValuesLoader_new : IDisposable
    {
        static readonly Dictionary<int, AttributeFn?> AttributeFns = [];

        private readonly Vrx vrx = new();
        private readonly Patsts patsts = new();
        private readonly Data data = new();
        private readonly FrText frText = new();

        private readonly IValueTableUtilities[] tableUtilities;

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

        public ValuesLoader_new()
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
            for (int i = 1; i <= item.NPages; i++)
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
                (lastUpdated == null || patsts.Updated > lastUpdated))
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
                    if (lastUpdated == null)
                    {
                        EntitiesLoaded++;
                    }
                    else
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
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetString(data));
        }

        private void ProcessUint8(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, data.Span[0]);
        }

        private void ProcessInt8(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0, DataRowCollection? dataRows2 = null)
        {
            dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt8(data[..1]));
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

        private static bool CheckFloat(Single res, ValueIndex valueIndex)
        {
            if (!Single.IsFinite(res))
            {
                Log.Warning($"Abnormal number {res}, entity:{valueIndex.EntityId}, attribute:{valueIndex.AttributeId}, Seq:{valueIndex.Seq}");
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

        private void ProcessDouble(DataRowCollection dataRows, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId, DataRowCollection? dataRows2 = null)
        {
            Single res = ProtonDbFileReader.GetDouble(GetMemory(data, 8));
            if (CheckFloat(res, valueIndex))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, Math.Round(res, subTypeId));
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
            using IndexReader ih = new();
            if (!string.IsNullOrEmpty(key))
            {
                dataRows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, key);

                var eId = ih.GetEntityId(subTypeId, key);

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


        public void LoadValues(int nRows)
        {
            var st = Stopwatch.StartNew();
            EntitiesSkipped = 0;
            EntitiesLoaded = 0;
            EntitiesUpdated = 0;
            foreach (IValueTableUtilities u in tableUtilities)
            {
                u.SwitchTables();
            }

            int counter = 0;

            using Proton2Context ctx = new();

            DateTime latest = DateTime.MinValue;
            if (ctx.Entities.Any())
            {
                latest = ctx.Entities.Max(e => e.LastUpdated);
            }

            ctx.Database.ExecuteSql($"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

            using SqlBulkCopy bkc = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString(),
                SqlBulkCopyOptions.TableLock & SqlBulkCopyOptions.KeepIdentity)
            {
                BatchSize = nRows * 2
            };

            long nEntities = 0;
            int nUpdated = 0;
            var prog = new Utilities.Progress(20);

            Action<int> loadFunction = Utilities.ConfigurationManager.AppSettings.NoLoad ? Noload : Process;

            if (Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities.Count == 0)
            {
                nEntities = vrx.NPages;
                //nEntities = 500;
                Log.Information($"Loading values for {nEntities} entities.." + DateTime.Now.ToShortDateString());
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

            List<Task> tasks = [];

            foreach (IValueTableUtilities u in tableUtilities)
            {
                //save to SQL any remaining rows in writing table not yet saved
                //run simultaneously
                tasks.Add(Task.Run(() =>
                {
                    //send to SQL db 
                    u.BulkLoad();
                    u.SyncFromStaging();
                }));
            }

            Task.WaitAll(tasks);
            tasks.Clear();

            foreach (IValueTableUtilities u in tableUtilities)
            {
                //switch to the reading table, populated from data from Proton .dbs at the ultimate iteration
                u.SwitchTables();
                tasks.Add(Task.Run(() =>
                {
                    //save to SQL any remaining rows in table not yet saved
                    //run simultaneously
                    u.BulkLoad();
                    u.SyncFromStaging();
                }));
            }

            Task.WaitAll(tasks);

            prog.WriteProgressBar(1);
            Console.WriteLine();
            st.Stop();
            if (Utilities.ConfigurationManager.AppSettings.NoLoad)
            {
                Log.Information($"{nUpdated} entities scanned in {st.Elapsed:hh\\:mm\\:ss}");
            }
            else
            {
                Log.Information($"{EntitiesUpdated} Entities updated or loaded");
                Log.Information($"{EntitiesSkipped} Entities skipped (already up to date)");
                Log.Information($"in {st.Elapsed:hh\\:mm\\:ss}");

            }

            void Noload(int i)
            {
                counter++;
                LoadDataRows(i, latest);
                prog.WriteProgressBar(counter / (float)nEntities);
            }


            void Process(int i)
            {
                counter++;

                List<Task> tasks = [];
                List<IValueTableUtilities> toSwitchTables = [];
                //tasks in list to run sumultaneously
                foreach (IValueTableUtilities u in tableUtilities)
                {
                    //DataTables already populated from previous load from Proton .dbs files
                    if (u.DataRows.Count > nRows)
                    {
                        toSwitchTables.Add(u);
                        //simultaneously process 'full' datatables
                        tasks.Add(Task.Run(() =>
                        {
                            //send to SQL db 
                            u.BulkLoad();
                            u.SyncFromStaging();
                        }));
                    }
                }
                tasks.Add(Task.Run(() =>
                {
                    //simultaneous new load from Proton .dbs files
                    LoadDataRows(i, latest);
                }));

                Task.WaitAll(tasks); //wait (block main thread) until all tasks complete

                foreach (IValueTableUtilities u in toSwitchTables)
                {
                    //for data sent to SQL db at current iteration.
                    //copy rows in table read from Proton .dbs to table to be sent to SQL at next iteration
                    //and clear table to recieve data from Proton .dbs at next iteration
                    u.SwitchTables();
                }


                nUpdated++;

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

            foreach (IValueTableUtilities util in tableUtilities)
            {
                util.Dispose();
            }
        }
    }
}

