using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command.Models
{
    public class Column
    {
        public string ObjectId { get; set; }

        public int ColumnId { get; set; }

        public string Name { get; set; }

        public string Datatype { get; set; }

        public bool IsNullable { get; set; }

        public bool IsIdentity { get; set; }

        public bool IsPkCandidate 
        { 
            get 
            {
                return IsIdentity && Datatype == "int" && !IsNullable;
            } 
        }
    }
}
