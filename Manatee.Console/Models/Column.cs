using System;
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

        public int Length { get; set; }

        public bool IsNullable { get; set; }

        public bool IsIdentity { get; set; }

        public string DefaultName { get; set; }
        public string DefaultValue { get; set; }

        public bool IsPkCandidate 
        { 
            get 
            {
                return IsIdentity && Datatype == "int" && !IsNullable;
            } 
        }

        private int GetLength()
        {
            if (Datatype == "nvarchar")
                return Length/2;
            return Length;
        }

        public string DeriveDatatype()
        {
            if (IsPkCandidate)
                return "pk";

            var datatype = Datatype;

            switch(datatype)
            {
                case("nvarchar"):
                case("varchar"):
                    if (Length == -1)
                        return "text";
                    if (GetLength() == 255)
                        return "string";

                    return string.Format("{0}({1})", datatype, GetLength());
                case("decimal"):
                    datatype = "money";
                    break;
                case("bit"):
                    datatype = "boolean";
                    break;
                case("datetime"):
                    datatype = "date";
                    break;
                case("uniqueidentifier"):
                    datatype = "guid";
                    break;
            }

            return datatype;
        }
    }
}
