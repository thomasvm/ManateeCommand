using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command.Models
{
    public class ForeignKey
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string ReferencedObjectName { get; set; }

        public IEnumerable<ForeignKeyColumn> Columns { get; set; }
    }
}
