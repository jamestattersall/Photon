using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProtonConsole2.DataContext
{
    [PrimaryKey(nameof(RowId))]
    public abstract class Row(int rowId, int entityId)
    {        
        public int RowId { get; set; } = rowId;
        public int EntityId { get; set; } = entityId;
    }

    [PrimaryKey(nameof(RowId), nameof(AttributeId))]
    public abstract class ValueBase_new(int rowId, short attributeId)
    {
        public int RowId { get; set; } = rowId;

        public short AttributeId { get; set; } = attributeId;

        //row ordinal number

        public virtual Row? Row  { get; set; }
        public virtual Attribute? Attribute { get; set; }
    }

     public partial class Value(int rowId, short attributeId, string value) : ValueBase_new(rowId, attributeId)
    {
        public string JsonValue { get; set; } = value;
    }


}
