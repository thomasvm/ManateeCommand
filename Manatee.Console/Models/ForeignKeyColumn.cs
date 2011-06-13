using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command.Models
{
    public class ForeignKeyColumn
    {
        public int ForeignKeyId { get; set; }

        public string ColumnName { get; set; }

        public string ReferencedColumnName { get; set; }
    }
}
