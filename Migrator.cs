using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.Common;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Manatee
{
    public class Migrator
    {  
        internal class Database
        {
            private DbProviderFactory _factory;
            private string _connectionString;

            public Database(string connectionStringName)
            {
                SetupConnectionAndFactory(connectionStringName);
            }

            public object QueryValue(string query)
            {
                using (var con = OpenConnection())
                {
                    var command = CreateCommand(con, query);
                    return command.ExecuteScalar();
                }
            }

            public void Execute(string query)
            {
                using (var con = OpenConnection())
                {
                    var command = CreateCommand(con, query);
                    command.ExecuteNonQuery();
                }
            }

            private DbCommand CreateCommand(DbConnection connection, string sql)
            {
                var command = _factory.CreateCommand();
                command.Connection = connection;
                command.CommandText = sql;
                return command;
            }

            private DbConnection OpenConnection()
            {
                var connection = _factory.CreateConnection();
                connection.ConnectionString = _connectionString;
                connection.Open();
                return connection;
            }

            private void SetupConnectionAndFactory(string connectionStringName)
            {
                if (connectionStringName == "")
                {
                    connectionStringName = ConfigurationManager.ConnectionStrings[0].Name;
                }

                var providerName = "System.Data.SqlClient";
                if (ConfigurationManager.ConnectionStrings[connectionStringName] != null)
                {
                    if (!string.IsNullOrEmpty(ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName))
                    {
                        providerName = ConfigurationManager.ConnectionStrings[connectionStringName].ProviderName;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Can't find a connection string with the name '" + connectionStringName + "'");
                }

                _factory = DbProviderFactories.GetFactory(providerName);
                _connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
            }
        }

        // json -> dynamic decoder adopted from Shawn Weisfeld, http://bit.ly/jPqVsQ
        internal static class Json
        {
            public static dynamic Decode(string json)
            {
                var serializer = new JavaScriptSerializer();
                serializer.RegisterConverters(new[] { new DynamicJsonConverter() });

                dynamic obj = serializer.Deserialize(json, typeof(object));

                return obj;
            }

            private sealed class DynamicJsonConverter : JavaScriptConverter
            {
                public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
                {
                    if (dictionary == null)
                        throw new ArgumentNullException("dictionary");

                    return type == typeof(object) ? new DynamicJsonObject(dictionary) : null;
                }

                public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
                {
                    throw new NotImplementedException();
                }

                public override IEnumerable<Type> SupportedTypes
                {
                    get { return new ReadOnlyCollection<Type>(new List<Type>(new[] { typeof(object) })); }
                }
            }

            private sealed class DynamicJsonObject : DynamicObject
            {
                private readonly IDictionary<string, object> _dictionary;

                public DynamicJsonObject(IDictionary<string, object> dictionary)
                {
                    if (dictionary == null)
                        throw new ArgumentNullException("dictionary");
                    _dictionary = dictionary;
                }

                public override bool TryGetMember(GetMemberBinder binder, out object result)
                {
                    if (!_dictionary.TryGetValue(binder.Name, out result))
                    {
                        // return null to avoid exception.  caller can check for null this way...
                        result = null;
                        return true;
                    }

                    var dictionary = result as IDictionary<string, object>;
                    if (dictionary != null)
                    {
                        result = new DynamicJsonObject(dictionary);
                        return true;
                    }

                    var arrayList = result as ArrayList;
                    if (arrayList != null && arrayList.Count > 0)
                    {
                        if (arrayList[0] is IDictionary<string, object>)
                            result = new List<object>(arrayList.Cast<IDictionary<string, object>>().Select(x => new DynamicJsonObject(x)));
                        else
                            result = new List<object>(arrayList.Cast<object>());
                    }

                    return true;
                }
            }
        }

        private int _currentVersion;
        private Database _db;

        public Migrator(string pathToMigrationFiles, string connectionStringName = "")
        {
            _db = new Database(connectionStringName);
            Migrations = LoadMigrations(pathToMigrationFiles);
            EnsureSchema(_db);

            _currentVersion = (int)_db.QueryValue("SELECT Version from SchemaInfo");
        }

        public IDictionary<string, dynamic> Migrations { get; private set; }

        public int CurrentVersion
        {
            get { return _currentVersion; }
        }

        public void Migrate(int to)
        {
            if (_currentVersion < to)
            {
                //UP
                for (int i = _currentVersion; i < to; i++)
                {
                    //grab the next version - we start the loop with the current
                    var migration = Migrations.Values.ElementAt(i);

                    foreach(string command in GetCommands(migration.up))
                        _db.Execute(command);

                    //increment the version
                    _db.Execute("UPDATE SchemaInfo SET Version = Version +1");
                }
            }
            else
            {
                //DOWN
                for (int i = _currentVersion; i > to; i--)
                {
                    if (i - 1 >= Migrations.Values.Count())
                        continue;

                    //get the migration and execute it
                    var migration = Migrations.Values.ElementAt(i - 1);
                    
                    if (migration.down == null) //(!DynamicExtentions.HasProperty(migration, "down"))
                    {
                        foreach(var cmd in ReadMinds(migration.up))
                        {
                            if (!string.IsNullOrEmpty(cmd))
                                _db.Execute(cmd);
                        }
                    }
                    else
                    {
                        foreach (string command in GetCommands(migration.down))
                            _db.Execute(command);
                    }
                    //decrement the version
                    _db.Execute("UPDATE SchemaInfo SET Version = Version - 1");
                }
            }
        }

        /// <summary>
        /// This is where the shorthand types are deciphered. Fix/love/tweak as you will
        /// </summary>
        private static string SetColumnType(string colType)
        {
            return colType.Replace("pk", "int PRIMARY KEY IDENTITY(1,1)")
                .Replace("money", "decimal(8,2)")
                .Replace("date", "datetime")
                .Replace("string", "nvarchar(255)")
                .Replace("boolean", "bit")
                .Replace("text", "nvarchar(MAX)")
                .Replace("guid", "uniqueidentifier");
        }

        /// <summary>
        /// Build a list of columns from the past-in array in the JSON file
        /// </summary>
        private static string BuildColumnList(dynamic columns)
        {
            //holds the output
            var sb = new System.Text.StringBuilder();
            var counter = 0;
            foreach (dynamic col in columns)
            {
                //name
                sb.AppendFormat(", [{0}] ", col.name);

                //append on the type. Don't do this in the formatter since the replacer might return no change at all
                sb.Append(SetColumnType(col.type));

                //nullability - don't set if this is the Primary Key
                if (col.type != "pk")
                {
                    if (col.nullable != null)
                    {
                        if (col.nullable)
                        {
                            sb.Append(" NULL ");
                        }
                        else
                        {
                            sb.Append(" NOT NULL ");
                        }
                    }
                    else
                    {
                        sb.Append(" NOT NULL ");
                    }
                }

                counter++;
                //this format will indent the column
                if (counter < columns.Count)
                {
                    sb.Append("\r\n    ");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Strip out the leading comma. Wish there was a more elegant way to do this 
        /// and no, Regex doesn't count
        /// </summary>
        private static string StripLeadingComma(string columns)
        {
            if (columns.StartsWith(", "))
            {
                return columns.Substring(2, columns.Length - 2);
            }
            return columns;
        }

        /// <summary>
        /// create unique name for index based on table and columns specified
        /// </summary>
        private static string CreateIndexName(dynamic ix)
        {
            var sb = new System.Text.StringBuilder();
            foreach (dynamic c in ix.columns)
            {
                sb.AppendFormat("{1}{0}", c.Replace(" ", "_"), (sb.Length == 0 ? "" : "_")); // ternary to only add underscore if not first iteration
            }
            return string.Format("IX_{0}_{1}", ix.table_name, sb.ToString());
        }

        /// <summary>
        /// create string for columns
        /// </summary>
        private static string CreateIndexColumnString(dynamic columns)
        {
            var sb = new System.Text.StringBuilder();
            foreach (dynamic c in columns)
            {
                sb.AppendFormat("{1} [{0}] ASC", c, (sb.Length == 0 ? "" : ",")); // ternary to only add comma if not first iteration
            }
            return sb.ToString();
        }

        private static IEnumerable<string> GetCommands(dynamic op)
        {
            if (op is IEnumerable)
            {
                foreach (dynamic opItem in op)
                    yield return GetCommand(opItem);

                yield break;
            }

            yield return GetCommand(op);
        }

        /// <summary>
        /// This is the main "builder" of the DDL SQL and it's tuned for SQL CE. 
        /// The idea is that you build your app using SQL CE, then upgrade it to SQL Server when you need to
        /// </summary>
        private static string GetCommand(dynamic op)
        {
            //the "op" here is an "up" or a "down". It's dynamic as that's what the JSON parser
            //will return. The neat thing about this parser is that the dynamic result will
            //return null if the key isn't present - so it's a simple null check for the operations/keys we need.
            //this will allow you to expand and tweak this migration stuff as you like

            var result = "";
            var pkName = "Id";
            //what are we doing?

            if (op == null)
            {
                return "-- no DOWN specified. If this is a CREATE table or ADD COLUMN - it will be generated for you";
            }
            
            if (op.GetType() == typeof(string))
            {
                return SetColumnType(op).Replace("{", "").Replace("}", "");
            }

            //CREATE
            if (op.create_table != null)  //(DynamicExtentions.HasProperty(op, "create_table"))
            {
                var columns = BuildColumnList(op.create_table.columns);

                //add some timestamps?
                if (op.create_table.timestamps != null && op.create_table.timestamps != false)
                {
                    columns += "\n    , CreatedBy nvarchar(250) NOT NULL\n    , CreatedOn datetime DEFAULT getdate() NOT NULL\n    , ModifiedBy nvarchar(250) NOT NULL\n    , ModifiedOn datetime DEFAULT getdate() NOT NULL";
                }

                //make sure we have a PK :)
                if (!columns.Contains("PRIMARY KEY") & !columns.Contains("IDENTITY"))
                {
                    columns = "Id int PRIMARY KEY IDENTITY(1,1) NOT NULL \n    " + columns;
                }
                else
                {
                    foreach (var col in op.create_table.columns)
                    {
                        if (col.type.ToString() == "pk")
                        {
                            pkName = col.name;
                            break;
                        }
                    }
                }
                columns = StripLeadingComma(columns);
                result = string.Format("CREATE TABLE [{0}]\r\n     ({1}) ", op.create_table.name, columns);

                //DROP 
            }
            else if (op.drop_table != null)
            {
                return "DROP TABLE " + op.drop_table;
                //ADD COLUMN
            }
            else if (op.add_column != null)
            {
                result = string.Format("ALTER TABLE [{0}] ADD {1} ", op.add_column.table, StripLeadingComma(BuildColumnList(op.add_column.columns)));
                //DROP COLUMN
            }
            else if (op.remove_column != null)
            {
                result = string.Format("ALTER TABLE [{0}] DROP COLUMN [{1}]", op.remove_column.table, op.remove_column.name);
                //CHANGE
            }
            else if (op.change_column != null)
            {
                result = string.Format(
                    "ALTER TABLE [{0}] ALTER COLUMN {1}", op.change_column.table, StripLeadingComma(BuildColumnList(op.change_column.columns)));
                //ADD INDEX
            }
            else if (op.foreign_key != null)
            {
                dynamic foreignKey = op.foreign_key;

                string fromColumns = string.Join(", ", foreignKey.from.columns);
                string toColumns = string.Join(", ", foreignKey.to.columns);
                
                result = string.Format(
                    "ALTER TABLE [{0}] ADD CONSTRAINT [{1}] FOREIGN KEY ({2}) REFERENCES [{3}]({4})", 
                    foreignKey.from.table, foreignKey.name, fromColumns, 
                    foreignKey.to.table, toColumns
                    );
            }
            else if (op.drop_constraint != null)
            {
                result = string.Format("ALTER TABLE [{0}] DROP CONSTRAINT [{1}]", op.drop_constraint.table, op.drop_constraint.name);
            }
            else if (op.add_index != null)
            {
                result = string.Format(
                    "CREATE NONCLUSTERED INDEX [{0}] ON [{1}] ({2} )",
                    CreateIndexName(op.add_index),
                    op.add_index.table_name,
                    CreateIndexColumnString(op.add_index.columns));
                //REMOVE INDEX
            }
            else if (op.remove_index != null)
            {
                result = string.Format("DROP INDEX {0}.{1}", op.remove_index.table_name, CreateIndexName(op.remove_index));
            }
            else if (op.execute != null)
            {
                result = op.execute;
            }

            return result;
        }

        /// <summary>
        /// This is the migration file loader. It uses a SortedDictionary that will sort on the key (which is the file name). 
        /// So be sure to name your file with a descriptive, sortable name. A good way to do this is the year_month_day_time:
        /// 2011_04_23_1233.js
        /// </summary>
        private static SortedDictionary<string, dynamic> LoadMigrations(string migrationPath)
        {
            //read in the files in the db/migrations directory
            var migrationDir = new System.IO.DirectoryInfo(migrationPath);
            var result = new SortedDictionary<string, dynamic>();

            var files = migrationDir.GetFiles();
            foreach (var file in files)
            {
                using (var t = new StreamReader(file.FullName))
                {
                    var bits = t.ReadToEnd();

                    //Uh oh! Did you get an error? JSON can be tricky - you have to be sure you quote your values
                    //as javascript only recognizes strings, booleans, numerics, or arrays of those things.
                    //if you always use a string.
                    dynamic decoded = Json.Decode(bits); //new JsonReader().Read(bits);
                    result.Add(Path.GetFileNameWithoutExtension(file.FullName), decoded);
                }
            }
            return result;
        }

        /// <summary>
        /// This loads up a special table that keeps track of what version your DB is on. It's one table with one field
        /// </summary>
        private static void EnsureSchema(Database db)
        {
            //does schema info exist?
            int exists = (int)db.QueryValue("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES where TABLE_NAME='SchemaInfo'");
            if (exists == 0)
            {
                db.Execute("CREATE TABLE SchemaInfo (Version INT)");
                db.Execute("INSERT INTO SchemaInfo(Version) VALUES(0)");
            }
        }

        /// <summary>
        /// If a "down" isn't declared, this handy function will try and figure it out for you
        /// </summary>
        private static IEnumerable<string> ReadMinds(dynamic migration)
        {
            var commands = new List<string>();

            if (migration is IEnumerable<object>)
            {
                IEnumerable<object> list = (IEnumerable<object>)migration;
                foreach (var item in list.Reverse())
                    commands.Add(ReadSingleMind(item));
            }
            else
            {
                commands.Add(ReadSingleMind(migration));
            }
            return commands;
        }

        private static string ReadSingleMind(dynamic op)
        {
            //CREATE
            if (op.create_table != null)
            {
                return string.Format("DROP TABLE [{0}]", op.create_table.name);
                //DROP COLUMN
            }
            else if (op.add_column != null)
            {
                return string.Format("ALTER TABLE [{0}] DROP COLUMN {1}", op.add_column, op.add_column.columns[0].name);
            }
            else if (op.add_index != null)
            {
                // DROP INDEX
                return string.Format("DROP INDEX {0}.{1}", op.add_index.table_name, CreateIndexName(op.add_index));
            }
            else if (op.foreign_key != null)
            {
                return string.Format("ALTER TABLE [{0}] DROP CONSTRAINT [{1}]", op.foreign_key.from.table, op.foreign_key.name);
            }
            return "";
        }
    }
}