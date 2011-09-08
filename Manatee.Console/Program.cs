using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Options;

namespace Manatee.Command
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var settings = new InputParameters();

            var options = new OptionSet
            {
                { "help|h", "Show Help", s => settings.ShowHelp = true }, 
                { "command=|c=", "Command, possible values can be 'list', 'goto', 'gen' or 'export'. List is the default.", s => settings.ParseCommand(s) }, 
                { "path=|p=", "Migrations path", s => settings.MigrationFolder = s }, 
                { "table=|t=", "Table to filter on when deriving", s => settings.Table = s }, 
                { "con=", "Name of the connection", s => settings.Connection = s }, 
                { "version=|v=|to=", "Destination version, must be a number or 'last'", s => settings.GotoVersion(s) }, 
                { "name=|n=", "Only taken into account when command is 'gen'. Name of the new migration", s => settings.Name = s }, 
            };

            try
            {
                options.Parse(args);
            }
            catch (Exception e)
            {
                Logger.WriteLine(ConsoleColor.Red, e.Message);
                settings.ShowHelp = true;
            }

            if (settings.ShowHelp || !settings.IsValid)
            {
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            try
            {
                DoManatee(settings);
            }
            catch (Exception e)
            {
                Logger.WriteLine(ConsoleColor.Red, "Command failed: {0}", e.Message);
#if DEBUG
                Logger.WriteLine(e.StackTrace);
#endif
            }
        }

        private static void DoManatee(InputParameters settings)
        {
            // Export
            if(settings.Command == Command.Export)
            {
                Logger.WriteLine(ConsoleColor.Green, "Exporting migrations from connection {0}", settings.Connection);

                var deriver = new MigrationExporter(settings.MigrationFolder, settings.Connection, settings.Table);
                deriver.DoExport();
                return;
            }

            // Generate
            if (settings.Command == Command.Gen)
            {
                Logger.WriteLine(ConsoleColor.Green, "Generating new migration file");

                var fileName = string.Format("{0}_{1}.json", DateTime.Now.ToString("yyyyMMdd_HHmm"), settings.Name);
                var content = @"{
    up: {
     
    },
    down: {
    }
}";
                var outputPath = Path.Combine(settings.MigrationFolder, fileName);
                File.WriteAllText(outputPath, content, Encoding.UTF8);

                Logger.WriteLine("Generated new file: {0}", fileName);
                return;
            }

            var migrator = new Migrator(settings.MigrationFolder, settings.Connection);

            // List
            if(settings.Command == Command.List)
            {
                Logger.WriteLine(ConsoleColor.Green, "Listing migrations from Folder {0} and connection {1}", settings.MigrationFolder, settings.Connection);
                Logger.WriteLine("    Current Version: {0}", migrator.CurrentVersion);
                int counter = 0;
                foreach(var migration in migrator.Migrations)
                {
                    counter++;
                    bool isCurrent = migrator.CurrentVersion == counter;
                    var msg = string.Format("    {0}: {1}{2}", counter, migration.Key, isCurrent ? " (current)" : string.Empty);
                    if (isCurrent)
                        Logger.WriteLine(ConsoleColor.Green, msg);
                    else 
                        Logger.WriteLine("    {0}: {1}", counter, migration.Key);
                }
                return;
            }

            // Execute
            if(settings.Command == Command.Goto)
            {
                Logger.WriteLine(ConsoleColor.Green, "Executing migrations from Folder {0} and connection {1}", settings.MigrationFolder, settings.Connection);

                var destinationVersion = DetermineVersion(settings, migrator);
                destinationVersion = Math.Min(destinationVersion, migrator.Migrations.Count);
                Logger.WriteLine("    Current Version: {0}", migrator.CurrentVersion);
                Logger.WriteLine("    Going to Version: {0}", destinationVersion);

                migrator.Migrate(destinationVersion);
                Logger.WriteLine(ConsoleColor.Yellow, "Migrations successful");
            }
        }

        static int DetermineVersion(InputParameters parameters, Migrator m)
        {
            switch(parameters.GotoMode)
            {
                default:
                case GotoMode.Specific:
                    return parameters.DesitinationVersion.GetValueOrDefault(0);
                case GotoMode.Previous:
                    return Math.Max(m.Migrations.Count - 1, 0);
                case GotoMode.Last:
                    return m.Migrations.Count;
            }
        }

        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Resolver;
        }

        static Assembly Resolver(object sender, ResolveEventArgs args)
        {
            var asName = new AssemblyName(args.Name);
            if (!asName.Name.Equals("Newtonsoft.Json"))
                return null;

            Assembly a1 = Assembly.GetExecutingAssembly();
            Stream s = a1.GetManifestResourceStream("Manatee.Command.Newtonsoft.Json.dll");
            byte[] block = new byte[s.Length];
            s.Read(block, 0, block.Length);
            Assembly a2 = Assembly.Load(block);
            return a2;
        }
    }
}
