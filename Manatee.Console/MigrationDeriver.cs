using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using PetaPoco;
using Manatee.Command.Models;
using System.IO;

namespace Manatee.Command
{
    public class MigrationDeriver
    {
        protected Database Db { get; set; }

        protected string Folder { get; set; }

        protected bool Force { get; set; }

        public MigrationDeriver(string pathToMigrationFiles = null, string connectionStringName = "", bool force = false)
        {
            Db = new Database(connectionStringName);

            if (string.IsNullOrEmpty(pathToMigrationFiles))
                pathToMigrationFiles = "DB";

            Folder = pathToMigrationFiles;
            Force = force;
        }

        public void DoDerive()
        {
            IEnumerable<Table> tables = LoadTableMetadata();

            if (!Directory.Exists(Folder))
                throw new FileNotFoundException("Folder does not exist.", Folder);

            bool canWipeFolder = Force;
            var files = Directory.GetFiles(Folder);
            if (!Force && files.Any())
            {
                Console.Write("The {0} folder is not empty? Do want to clear its contents? (y|n):", Folder);
                canWipeFolder = Console.ReadLine() == "y";
            }
            
            int nr = 0;

            foreach(var table in tables)
            {
                string filename = string.Format("{0} - Create {1}.js", nr++.ToString().PadLeft(3, '0'), table.Name);
                string fullPath = Path.Combine(Folder, filename);

                if (File.Exists(fullPath))
                {
                    if (canWipeFolder)
                        throw new Exception(string.Format("File '{0}' already exists", fullPath));

                    File.Delete(fullPath);
                }

                Console.WriteLine("Creating file: '{0}'", fullPath);
            }
        }

        private IEnumerable<Table> LoadTableMetadata()
        {
            var tables =
                Db.Fetch<Table>("SELECT ObjectId = object_id, Name = name FROM sys.tables WHERE name NOT IN ('SchemaInfo', 'sysdiagrams') ORDER BY name");

            foreach (var table in tables)
            {
                table.Columns = Db.Fetch<Column>(@"SELECT ObjectId = sc.object_id, ColumnId = sc.column_id, Name = sc.name,
                                                          IsNullable = sc.is_nullable, IsIdentity = sc.is_identity, 
                                                          DataType = st.name
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
