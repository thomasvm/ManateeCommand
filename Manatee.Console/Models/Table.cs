using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command.Models
{
    public class Table
    {
        private static readonly IEnumerable<string> TimestampColumns = new List<string>
                                                                           {
                                                                               "CreatedOn", "CreatedBy", "ModifiedOn", "ModifiedBy"
                                                                           };

        public string ObjectId { get; set; }

        public string Name { get; set; }

        public bool UseTimestamps 
        { 
            get
            {
                if (Columns == null)
                    return false;

                var columnList = Columns.Select(x => x.Name);
                foreach(var timestampColumn in TimestampColumns)
                {
                    if (!columnList.Contains(timestampColumn))
                        return false;
                }

                return true;
            }
        }

        public IEnumerable<Column> Columns { get; set; }

        public IEnumerable<Column> NonTimestampColumns
        {
            get
            {
                if (!UseTimestamps)
                    return Columns;

                return Columns.Where(x => !TimestampColumns.Contains(x.Name));
            }
        }
    }
}
