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
                Console.WriteLine("    {0} foreign keys", table.ForeignKeys.Count());
            }
        }

        private IEnumerable<Table> LoadTableMetadata()
        {
            var tables =
                Db.Fetch<Table>("SELECT ObjectId = object_id, Name = name FROM sys.tables WHERE name <> 'SchemaInfo'");

            foreach (var table in tables)
            {
                table.Columns = Db.Fetch<Column>(@"SELECT ObjectId = object_id, ColumnId = column_id, Name = name,
                                                          IsNullable = is_nullable, IsIdentity = is_identity
                                                   FROM sys.columns WHERE object_id = @0",
                                                 table.ObjectId);

                table.ForeignKeys = Db.Fetch<ForeignKey>(@"SELECT Id = object_id,
                                                                  Name = name, 
                                                                  ReferencedObjectName = OBJECT_NAME(referenced_object_id)
                                                           FROM sys.foreign_keys
                                                           WHERE parent_object_id = @0", table.ObjectId);

                var foreignKeyColumns =
                    Db.Fetch<ForeignKeyColumn>(
                        @"SELECT ForeignKeyId = constraint_object_id, 
                                 ColumnName   = sc.name,
                                 ReferencedColumnName = osc.name
                          FROM   sys.foreign_key_columns fkc
                          JOIN   sys.columns sc
                          ON     fkc.parent_object_id = sc.object_id
                          AND    fkc.parent_column_id = sc.column_id
                          JOIN   sys.columns osc
                          ON     fkc.referenced_object_id = osc.object_id
                          AND    fkc.referenced_column_id = osc.column_id
                          WHERE  parent_object_id = @0",
                        table.ObjectId);
                
                var grouped = foreignKeyColumns.GroupBy(x => x.ForeignKeyId, y => y);
                foreach(var item in grouped)
                    table.ForeignKeys.Single(x => x.Id == item.Key).Columns = item.ToList();
            }

            return tables;
        }
    }
}
