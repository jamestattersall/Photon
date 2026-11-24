
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Primitives;
using ProtonConsole2.DataContext;
using System.Text;
using System.Text.Encodings.Web;

namespace ProtonConsole2.Proton
{
    public class MetaDataFunctions
    {
        public static List<DataContext.EntityType> GetEntityTypes()
        {
            List<DataContext.EntityType> list = [];
            using Proton.EntityDef entityDef = new();
            for (short ix = 1; ix <= entityDef.NPages; ix++)
            {
                if (entityDef.MoveToPage(ix))
                {
                    list.Add(new()
                    {
                        Id = ix,
                        Name = entityDef.Name,
                        KeyIndexTypeId = entityDef.KeyIndexDefId,
                        IdLineViewId = entityDef.IdGroup,
                        IdAttributeId = entityDef.IdItemId,
                        DefaultIndexTypeId = entityDef.DefaultIndexDefId

                    });
                }

            }
            return list;
        }

        public static List<DataContext.DataType> GetDataTypes()
        {
            string jsonData = $$"""
                [
                    {
                        "Id": {{(short)DataTypes.Text}},
                        "Name": "{{DataTypes.Text}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    },    {
                        "Id": {{(short)DataTypes.numeric}},
                        "Name": "{{DataTypes.numeric}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueNumbers)}}"
                    },    {
                        "Id": {{(short)DataTypes.Lookup}},
                        "Name": "{{DataTypes.Lookup}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueLookups)}}",
                        "LookupTable": "{{nameof(Proton2Context.Lookups)}}"
                      },    {
                        "Id": {{(short)DataTypes.EntityPtr}},
                        "Name": "{{DataTypes.EntityPtr}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueEntities)}}",
                        "LookupTable": "{{nameof(Proton2Context.Entities)}}",                
                        "AltValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    },    {
                        "Id": {{(short)DataTypes.LongText}},
                        "Name": "{{DataTypes.LongText}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueLongTexts)}}"
                    },  {
                        "Id": {{(short)DataTypes.Date}},
                        "Name": "{{DataTypes.Date}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueDates)}}"
                    }, {
                        "Id": {{(short)DataTypes.Time}},
                        "Name": "{{DataTypes.Time}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueTimes)}}",
                        "AltValueTable": "{{nameof(Proton2Context.ValueTexts)}}"
                    }, {
                        "Id": {{(short)DataTypes.QualifiedNumbers}},
                        "Name": "{{DataTypes.QualifiedNumbers}}",
                        "ValueTable": "{{nameof(Proton2Context.ValueNumbers)}}",
                        "AltValueTable": "{{nameof(Proton2Context.ValueLookups)}}",
                        "AltLookupTable": "{{nameof(Proton2Context.Lookups)}}"
                    }
                    
                ]
                """;

            return System.Text.Json.JsonSerializer.Deserialize<List<DataContext.DataType>>(jsonData)!;
        }

        public static List<UserStarter> GetUserStarters()
        {
            List<UserStarter> list = [];
            using Proton.Passwd passwd = new();
            using Proton.Menu mnu = new();
            for (short ix = 1; ix <= passwd.NPages; ix++)
            {
                if (passwd.MoveToPage(ix) && passwd.EncryptedPassword.Length > 1 && passwd.FunctionId == 1)
                {
                    short fn = passwd.FunctionParameter;
                    if (mnu.MoveToPage(fn))
                    {
                        list.Add(new()
                        {
                            Id = ix,
                            UserCode = passwd.EncryptedPassword,
                            UserName = passwd.UserName,
                            MenuId = passwd.FunctionParameter,
                            EntityTypeId = passwd.EntityTypeId,
                            IdLineViewId = passwd.IdLineScreenId,
                            IndexTypeId = passwd.IndexDefId
                        });
                    }
                }

            }

            return list;
        }


        public static List<IndexType> GetIndexTypes()
        {
            List<IndexType> list = [];
            using Proton.IndexDef indexDef = new();
            using Proton.KeyDef keyDef = new();
            {
                for (short i = 1; i <= indexDef.NPages; i++)
                {
                    if (indexDef.MoveToPage(i))
                    {
                        for (short k= 1; k<=indexDef.NPages; k++)
                        {
                            if(keyDef.MoveToPage(k) && keyDef.IndexDefId == i)
                            {
                                list.Add(new()
                                {
                                    Id = i,
                                    Name = keyDef.Name,
                                    EntityTypeId = keyDef.EntityTypeId,
                                    IdLineViewId = indexDef.IdlineScreenId,
                                    Prefix = keyDef.Prefix
                                });
                                break; // only one key per index type
                            }
                        }
                    }
                }
            }
            return list;
        }

        public static List<DataContext.Table> GetTables()
        {
            Dictionary<short, DataContext.Table> list = [];
            using Proton.Screen scrn = new();
            using Proton.TrGroup grp = new();
            using Proton.Item itm = new();
            using Proton.EntityDef ent = new();
            for (short i = 1; i <= itm.NPages; i++)
            {
                if (itm.MoveToPage(i) && itm.IsInstalled && itm.DataType > 0 && itm.EntityTypeId > 0)
                {
                    short tableId = (itm.GroupId == 0) ? (short)-itm.EntityTypeId : itm.GroupId;

                    DataContext.Table? tbl = list.GetValueOrDefault(tableId);
                    if (tbl == null)
                    {
                        tbl = new()
                        {
                            Id = tableId,
                            Name = itm.Name,
                            EntityTypeId = itm.EntityTypeId,
                            DateAttributeId = itm.DateItemId,
                        };
                        if (tableId > 0)
                        {
                            if (scrn.MoveToPage(tableId))
                            {
                                tbl.Name = scrn.Name;
                            }
                            if (grp.MoveToPage(tableId) &&
                                grp.Name.Length > tbl.Name.Length)
                            {
                                tbl.Name = grp.Name;
                            }
                            string nm = itm.Comment.Replace("date of ", null, StringComparison.CurrentCultureIgnoreCase).Replace("date", null, StringComparison.CurrentCultureIgnoreCase).Trim();
                            if (tbl.Name == null || tbl.Name.Length < nm.Length)
                            {
                                tbl.Name = nm;
                            }
                        }
                        else
                        {
                            if (ent.MoveToPage(tbl.EntityTypeId))
                            {
                                tbl.Name = ent.Name;
                            }
                        }
                        list.Add(tableId, tbl);
                    }
                }
            }

            return [.. list.Values];
        }


        public static List<DataContext.Attribute> GetAttributes()
        {
            List<DataContext.Attribute> list = [];
            using Proton.Screen scrn = new();
            using Proton.TrGroup grp = new();
            using Proton.Item itm = new();
            using Proton.Valid vald = new();
            for (short i = 1; i <= itm.NPages; i++)
            {
                if (itm.MoveToPage(i) && itm.DataType>0)
                {
                    short tableId = (itm.GroupId == 0) ? (short)-itm.EntityTypeId : itm.GroupId;

                    DataContext.Attribute attr = new()
                    {
                        Id = i,
                        EntityTypeId = itm.EntityTypeId,
                        TableId = tableId,
                        DisplayLength = itm.DisplayLength,
                        Name = itm.Name,
                        Comment = itm.Comment,
                        DateAttributeId=itm.DateItemId
                    };
                    switch (itm.DataType)
                    {
                        case 1:
                            attr.DataTypeId = (short)DataContext.DataTypes.Text;
                            break;
                        case 2:
                        case 3:
                        case 4:
                            attr.DataTypeId = (short)DataTypes.numeric;
                            break;
                        case 5:
                        case 6:
                            attr.DataTypeId = (short)DataTypes.numeric;
                            break;
                        case 7:
                            attr.DataTypeId = (short)DataTypes.Lookup;
                            attr.LookupTypeId = -1;
                            break;
                        case 8:
                            attr.DataTypeId = (short)DataTypes.Date;
                            attr.Format = (attr.DisplayLength < 10) ? "dd.MM.yy" : "dd.MM.yyyy";
                            break;
                        case 9:
                            attr.DataTypeId = (short)DataTypes.Time;
                            attr.Format = (attr.DisplayLength < 5) ? "hhmm" : "hh:mm";
                            break;
                        case 10:
                            attr.DataTypeId = (short)DataTypes.LongText;
                            break;
                        case 11:
                            attr.DataTypeId = (short)DataTypes.EntityPtr;
                            break;
                        case 12:
                            attr.DataTypeId = (byte)DataTypes.Lookup;
                            break;

                        default:
                            throw new Exception("unknown datatype");

                    }
                    if (vald.MoveToPage(i))
                    {
                        if (itm.IsCalculated)
                        {
                            attr.Quark = vald.CalcQuark;
                        } 
                        else
                        {
                            switch (itm.DataType)
                            {
                                case 2:
                                case 3:
                                case 4:
                                    attr.Max = (float)vald.Max(itm.DataType, itm.SubType);
                                    attr.Min = (float)vald.Min(itm.DataType, itm.SubType);
                                    break;
                                case 5:
                                case 6:
                                    if (vald.QualifierCodeType > 0 || vald.ModifierCodeType > 0)
                                    {
                                        attr.DataTypeId = (short)DataTypes.QualifiedNumbers;
                                        attr.LookupTypeId = Math.Max(vald.QualifierCodeType, vald.ModifierCodeType);
                                    }
                                    attr.Max = (float)vald.Max(itm.DataType, itm.SubType);
                                    attr.Min = (float)vald.Min(itm.DataType, itm.SubType);
                                    break;
                                case 7:
                                    attr.Max = -(float)vald.Min(itm.DataType, itm.SubType);
                                    attr.Min = -(float)vald.Max(itm.DataType, itm.SubType);
                                    break;

                                case 12:
                                    attr.LookupTypeId = vald.CodeTypeId;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    list.Add(attr);
                }
            }

            return list;
        }

        public static List<DataContext.Menu> GetMenus()
        {
            using Proton.Menu menu = new();
            var list = new List<DataContext.Menu>();
            for (short i = 1; i <= menu.NPages; i++)
            {
                if (menu.MoveToPage(i))
                {
                    list.Add(new()
                    {
                        Id = i,
                        Name = menu.Name
                    });
                
                }
            }
            return list;
        }

        public static List<MenuItem> GetMenuItems()
        {
            using Proton.Menu menu = new();
            var list = new List<MenuItem>();
            for (short i = 1; i <= menu.NPages; i++)
            {
                if (menu.MoveToPage(i))
                {
                    byte c = 0;
                    while (menu.MoveToNextBlock())
                    {
                        if (menu.ItemName.Length > 0)
                        {
                            list.Add(new()
                            {
                                MenuId = i,
                                Seq = c,
                                Name = menu.ItemName,
                                Function = menu.Function,
                                NextMenuId = menu.NextMenuId,
                                StartMenuId = menu.StartMenuId,
                                Parameter1 = menu.Parameter1,
                                Parameter2 = menu.Parameter2,
                                Parameter3 = menu.Parameter3,
                                Parameter4 = menu.Parameter4
                            });
                        }
                        c++; 
                    }
                }
            }

            return list;
        }

        public static List<LookupType> GetLookupTypes()
        {
            List<LookupType> codes = [];

            using (Proton.CodeDef cd = new())
            {
                codes.Add(new() { Id = -1, Name = "Proton DICT code" });
                for (short ix = 1; ix <= cd.NPages; ix++)
                {
                    if (cd.MoveToPage(ix))
                    {
                        var ct = ix;
                        if (ct > 0)
                        {
                            codes.Add(new() { Id = ix, Name = cd.Name });
                        }
                    }
                }
            }
            return codes;
        }



        public static List<Lookup> GetLookups()
        {
            List<Lookup> lookups = [];

            using (Proton.RCode rcode = new())
            {
                for (int ix = 1; ix <= rcode.NPages; ix++)
                {
                    if (rcode.MoveToPage(ix))
                    {
                      lookups.Add(new() { Id = ix, Name = rcode.Name, LookupTypeId = rcode.CodeTypeID, Code = rcode.ReadCode });
                    }
                }
            }
            using (Proton.Dict dict = new())
            {
                for (int ix = 1; ix <= dict.NPages; ix++)
                {
                    if (dict.MoveToPage(ix))
                    {
                        lookups.Add(new() { Id = -ix, Name = dict.Name, LookupTypeId = 0 });
                    }
                }
            }

            return lookups;
        }


        public static List<DataContext.View> GetViews()
        {
            List<DataContext.View> list = [];

            using (Proton.Screen scrn = new())
            using (Proton.ScrTxt scrtxt = new())
            using (Proton.Item itm = new())
            using (Proton.TrGroup grp = new())

            for (short ix = 1; ix <= scrn.NPages; ix++)
            {
                if (scrn.MoveToPage(ix))
                {
                    var scrObj = new DataContext.View()
                    {
                        Id = ix,
                        Name=scrn.Name,
                        NRows = scrn.RowCount,
                        NItems = scrn.ItemCount
                    };

                    if (grp.MoveToPage(ix) && itm.MoveToPage(grp.DateItemId))
                    {
                        scrObj.TableId = itm.GroupId == 0 ? (short)-itm.EntityTypeId : itm.GroupId;
                        scrObj.EntityTypeId = itm.EntityTypeId;
                    }
                    else
                    {
                        while (scrn.MoveToNextBlock())
                        {
                            if (itm.MoveToPage(scrn.ItemId))
                            {
                                scrObj.TableId = itm.GroupId == 0 ? (short)-itm.EntityTypeId : itm.GroupId;
                                scrObj.EntityTypeId = itm.EntityTypeId;
                                break;
                            }
                        }
                    }
         
                    if (scrObj.TableId != 0 && scrObj.EntityTypeId != 0)
                    {

                        if (scrtxt.MoveToPage(ix))
                        {
                            var sb = new StringBuilder();
                            SortedList<int,string> capts = [];
                            byte miny = 255;
                            while (scrtxt.MoveToNextBlock())
                            {
                                //get Y value to top row
                                if (scrtxt.Y>0 && scrtxt.Y < miny) miny = scrtxt.Y;
                            }
                            scrtxt.MoveToFirstBlock();
                            while (scrtxt.MoveToNextBlock())
                            {
                                if (scrtxt.Y==miny && !scrtxt.Text.StartsWith('['))
                                {
                                    capts.Add(scrtxt.X, scrtxt.Text);
                                }
                            }
                            foreach(var cap in capts.OrderBy(c=> c.Key))
                            {
                                sb.Append(cap.Value + " ");
                            }

                            scrObj.Name = sb.ToString().Trim();
                        }

                        list.Add(scrObj);
                    }
                }
            }

            return list;
        }


        public static List<ViewAttribute> GetViewAttributes()
        {
            List<ViewAttribute> list = [];

            using (Proton.Screen scrn = new())

            for (short ix = 1; ix <= scrn.NPages; ix++)
            {
                if (scrn.MoveToPage(ix) )
                {
                    short seq = 0;
                    while(scrn.MoveToNextBlock())
                    {
                        var va = new ViewAttribute()
                        {
                            ViewId = ix,
                            AttributeId = scrn.ItemId,
                            Seq = seq,
                            X = scrn.X,
                            Y = scrn.Y
                        };

                        if (va.AttributeId > 0 && va.X > 0 && va.Y > 0
                                && !list.Any(v => v.AttributeId == va.AttributeId && v.ViewId == ix)) 
                        {
                            list.Add(va);
                            seq++;
                        }
                    }
                }
            }
            return list;
        }

        public static List<ViewCaption> GetViewCaptions()
        {
            List<ViewCaption> list = [];
            using (Proton.ScrTxt scrtxt = new())
            for (short ix = 1; ix <= scrtxt.NPages; ix++)
            {
                if (scrtxt.MoveToPage(ix))
                {
                    short seq = 0;
         
                    while (scrtxt.MoveToNextBlock() )
                    {
                        var vc=new ViewCaption()                      
                        {
                            ViewId = ix,
                            Seq = seq,
                            Caption = scrtxt.Text,
                            X = scrtxt.X,
                            Y = scrtxt.Y
                        };
                        if (vc.Caption.Length > 0 && vc.X > 0 && vc.Y > 0)
                        {
                            list.Add(vc);
                            seq++;
                        }
                    }
                }
            }
            return list;
        }

    }
}
