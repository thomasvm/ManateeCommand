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
            IEnumerable<Table> tables = LoadTableMetadata();

            foreach(var table in tables)
            {
                Console.WriteLine("Table: {0}; timestamps: {1}", table.Name, table.UseTimestamps);
                Console.WriteLine("    {0} columns", table.Columns.Count());
            }
        }

        private IEnumerable<Table> LoadTableMetadata()
        {
            var tables =
                Db.Fetch<Table>("SELECT ObjectId= object_id, Name = name FROM sys.tables WHERE name <> 'SchemaInfo'");

            foreach (var table in tables)
            {
                table.Columns = Db.Fetch<Column>("SELECT ObjectId = object_id, Name = name FROM sys.columns WHERE object_id = @0",
                                                 table.ObjectId);
            }

            return tables;
        }
    }
}
