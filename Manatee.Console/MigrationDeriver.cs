using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PetaPoco;
using Manatee.Command.Models;
using System.IO;

namespace Manatee.Command
{
    public class MigrationDeriver
    {
        protected Database Db { get; set; }

        protected string Folder { get; set; }

        protected string Table { get; set; }

        public MigrationDeriver(string pathToMigrationFiles = null, string connectionStringName = "", string table = null)
        {
            Db = new Database(connectionStringName);

            if (string.IsNullOrEmpty(pathToMigrationFiles))
                pathToMigrationFiles = "DB";

            Folder = pathToMigrationFiles;
            Table = table;
        }

        public void DoDerive()
        {
            IEnumerable<Table> tables = LoadTableMetadata();

            if (!Directory.Exists(Folder))
                throw new FileNotFoundException("Folder does not exist.", Folder);
            
            int nr = 0;

            foreach(var table in tables)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHss");

                string filename = string.Format("{2}_{0}_create_{1}.json", (++nr).ToString().PadLeft(3, '0'), table.Name, timestamp);
                string fullPath = Path.Combine(Folder, filename);
                
                using(new ColorPrinter(ConsoleColor.Green))
                    Console.WriteLine("Creating file: '{0}'", filename);

                dynamic up = new
                                 {
                                     create_table = new
                                                        {
                                                            name = table.Name,
                                                            timestamps = table.UseTimestamps,
                                                            columns = (from col in table.NonTimestampColumns
                                                                       select new { name = col.Name, type = col.DeriveDatatype() }).ToArray()
                                                        }
                                 };

                dynamic down = new
                                   {
                                       drop_table= table.Name
                                   };

                dynamic migration = new ExpandoObject();
                migration.up = up;
                migration.down = down;
                
                string json = JsonConvert.SerializeObject(migration, Formatting.Indented);
                File.WriteAllText(fullPath, json);
            }
            
            using(new ColorPrinter(ConsoleColor.Yellow))
                Console.WriteLine("Written {0} files to folder '{1}'.", nr, Folder);
        }

        private IEnumerable<Table> LoadTableMetadata()
        {
            var tables =
                Db.Fetch<Table>("SELECT ObjectId = object_id, Name = name FROM sys.tables " +
                                "WHERE name NOT IN ('SchemaInfo', 'sysdiagrams') " +
                                "AND   (name = @0 OR @0 IS NULL)" +
                                "ORDER BY name", Table);

            foreach (var table in tables)
            {
                table.Columns = Db.Fetch<Column>(@"SELECT ObjectId = sc.object_id, ColumnId = sc.column_id, Name = sc.name,
                                                          IsNullable = sc.is_nullable, IsIdentity = sc.is_identity, 
                                                          DataType = st.name, Length = sc.max_length
                                                   FROM   sys.columns sc 
                                                   JOIN   sys.types st
                                                   ON     sc.user_type_id = st.user_type_id
                                                   WHERE  sc.object_id = @0
                                                   ORDER BY sc.column_id",
                                                 table.ObjectId);

                table.ForeignKeys = Db.Fetch<ForeignKey>(@"SELECT Id = object_id,
                                                                  Name = name, 
                                                                  ReferencedObjectName = OBJECT_NAME(referenced_object_id)
                                                           FROM sys.foreign_keys
                                                           WHERE parent_object_id = @0", 
                                                           table.ObjectId);

                // load all foreign key column at once
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
                
                // now split them over the several foreign keys
                var grouped = foreignKeyColumns.GroupBy(x => x.ForeignKeyId, y => y);
                foreach(var item in grouped)
                    table.ForeignKeys.Single(x => x.Id == item.Key).Columns = item.ToList();
            }

            return tables;
        }
    }
}
