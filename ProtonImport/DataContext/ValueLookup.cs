using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class ValueLookup(int entityId, short attributeId, short seq) : ValueBase( entityId,  attributeId,  seq)
{
    public int LookupId { get; set; }

}