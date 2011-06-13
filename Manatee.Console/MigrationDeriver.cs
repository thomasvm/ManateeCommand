using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PetaPoco;
using Manatee.Command.Models;

namespace Manatee.Command
{
    public class MigrationDeriver
    {
        protected Database Db { get; set; }

        protected string Folder { get; set; }

        public MigrationDeriver(string pathToMigrationFiles = null, string connectionStringName = "")
        {
            Db = new Database(connectionStringName);

            if (string.IsNullOrEmpty(pathToMigrationFiles))
                pathToMigrationFiles = "DB";

            Folder = pathToMigrationFiles;
        }

        public void DoDerive()
        {
            var tables =
                Db.Fetch<Table>("SELECT ObjectId= object_id, Name = name FROM sys.tables WHERE name <> 'SchemaInfo'");

            foreach(var table in tables)
            {
                Console.WriteLine(table.Name);

                var columns =
                    Db.Fetch<Column>("SELECT ObjectId = object_id, Name = name FROM sys.columns WHERE object_id = @0",
                                     table.ObjectId);

                foreach(var col in columns)
                {
                    Console.WriteLine("     {0}", col.Name);
                }
            }
        }
    }
}
