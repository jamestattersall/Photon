using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ProtonConsole2.DataContext;

public partial class ValueEntity(int entityId, short attributeId, short seq) : ValueBase(entityId, attributeId, seq)
{
    public int LinkedEntityId { get; set; }

}