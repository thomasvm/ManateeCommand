using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PetaPoco;
using Manatee.Command.Models;
using System.IO;

namespace Manatee.Command
{
    /// <summary>
    /// Intentionally everything in one file, this doesn't have to become
    /// an enterprise level abstracted thing.
    /// </summary>
    public class MigrationExporter
    {
        protected Database Db { get; set; }

        protected string Folder { get; set; }

        protected string Table { get; set; }

        public MigrationExporter(string pathToMigrationFiles = null, string connectionStringName = "", string table = null)
        {
            Db = new Database(connectionStringName);

            if (string.IsNullOrEmpty(pathToMigrationFiles))
                pathToMigrationFiles = "DB";

            Folder = pathToMigrationFiles;
            Table = table;
        }

        public void DoExport()
        {
            IEnumerable<Table> tables = LoadTableMetadata();

            if (!Directory.Exists(Folder))
                throw new FileNotFoundException("Folder does not exist.", Folder);
            
            int nr = 0;

            foreach(var table in tables)
                DeriveTableMigration(++nr, table);

            foreach(var table in tables.Where(t => t.ForeignKeys.Any()))
            {
                foreach(var fk in table.ForeignKeys)
                    DeriveForeignKeyMigration(++nr, table, fk);
            }

            Logger.WriteLine(ConsoleColor.Yellow, "Written {0} files to folder '{1}'.", nr, Folder);
        }

        private void DeriveTableMigration(int nr, Table table)
        {
            string name = string.Format("create_{0}", table.Name);
            var migration = CreateTableMigration(table);
            WriteMigration(nr, name, migration);
        }

        private static object CreateTableMigration(Table table)
        {
            dynamic up = new
                             {
                                 create_table = new
                                                    {
                                                        name = table.Name,
                                                        timestamps = table.UseTimestamps,
                                                        columns = (from col in table.NonTimestampColumns
                                                                   select CreateColumnDefinition(col)).ToArray()
                                                    }
                             };
            dynamic down = new
                               {
                                   drop_table= table.Name
                               };
            var migration = new {up, down };
            return migration;
        }

        private static object CreateColumnDefinition(Column col)
        {
            dynamic jsonCol = new ExpandoObject();

            jsonCol.name = col.Name;
            jsonCol.type = col.DeriveDatatype();

            if (col.IsNullable)
                jsonCol.nullable = true;

            if (!string.IsNullOrEmpty(col.DefaultName))
                jsonCol.@default = new {name = col.DefaultName, value = col.DefaultValue};

            return jsonCol;
        }

        private void DeriveForeignKeyMigration(int nr, Table table, ForeignKey fk)
        {
            var name = string.Format("create_{0}", fk.Name);
            var migration = CreateForeignKeyMigration(table, fk);
            WriteMigration(nr, name, migration);
        }

        private object CreateForeignKeyMigration(Table table, ForeignKey fk)
        {
            var up = new
                         {
                             foreign_key = new
                                               {
                                                   name = fk.Name,
                                                   from = new
                                                              {
                                                                  table = table.Name,
                                                                  columns = fk.Columns.Select(c => c.ColumnName).ToArray()
                                                              },
                                                   to = new
                                                            {
                                                                table = fk.ReferencedObjectName,
                                                                columns = fk.Columns.Select(c => c.ReferencedColumnName).ToArray()
                                                            }
                                               }
                         };
            var down = new
                           {
                               drop_constraint= new
                                                    {
                                                        table = table.Name,
                                                        name = fk.Name
                                                    }
                           };
            var migration = new { up, down };
            return migration;
        }

        #region Helpers

        private IEnumerable<Table> LoadTableMetadata()
        {
            var tables =
                Db.Fetch<Table>("SELECT ObjectId = object_id, Name = name FROM sys.tables " +
                                "WHERE name NOT IN ('SchemaInfo', 'sysdiagrams') " +
                                "AND   (name = @0 OR @0 IS NULL)" +
                                "ORDER BY name", Table);

            foreach (var table in tables)
            {
                table.Columns =
                    Db.Fetch<Column>(
                        @"SELECT ObjectId = sc.object_id, ColumnId = sc.column_id, Name = sc.name,
                                                          IsNullable = sc.is_nullable, IsIdentity = sc.is_identity, 
                                                          DataType = st.name, Length = sc.max_length,
                                                          DefaultName = OBJECT_NAME(sc.default_object_id), DefaultValue = scom.text
                                                   FROM   sys.columns sc 
                                                   JOIN   sys.types st
                                                   ON     sc.user_type_id = st.user_type_id
                                                   LEFT JOIN syscomments scom
                                                   ON     sc.default_object_id = scom.id
                                                   WHERE  sc.object_id = @0
                                                   ORDER BY sc.column_id",
                        table.ObjectId);

                table.ForeignKeys =
                    Db.Fetch<ForeignKey>(
                        @"SELECT Id = object_id,
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
                foreach (var item in grouped)
                    table.ForeignKeys.Single(x => x.Id == item.Key).Columns = item.ToList();
            }

            return tables;
        }

        private void WriteMigration(int nr, string name, dynamic migration)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHss");

            string filename = string.Format("{0}_{1}_{2}.json", timestamp, nr.ToString().PadLeft(3, '0'), name);
            string fullPath = Path.Combine(Folder, filename);

            Logger.Write(ConsoleColor.Green, "    Creating file: ");
            Logger.WriteLine(filename);

            string json = Serialize(migration);
            File.WriteAllText(fullPath, json);
        }

        private string Serialize(dynamic value)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(null);
            
            StringBuilder sb = new StringBuilder(128);
            StringWriter sw = new StringWriter(sb, CultureInfo.InvariantCulture);
            using (JsonTextWriter jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.Formatting = Formatting.Indented;
                jsonWriter.QuoteName = false;

                jsonSerializer.Serialize(jsonWriter, value);
            }

            return sw.ToString();
        }

        #endregion
    }
}
