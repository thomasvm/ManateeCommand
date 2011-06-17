using System;
using System.Collections.Generic;
using System.Linq;
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
                { "command=|c=", "Command, possible values can be 'list', 'goto' or 'derive'. List is the default.", s => settings.ParseCommand(s) }, 
                { "path=|p=", "Migrations path", s => settings.MigrationFolder = s }, 
                { "force|f", "Force output", s=> settings.Force = true},
                { "con=", "Name of the connection", s => settings.Connection = s }, 
                { "version=|v=", "Destination version, must be a number or 'last'", s => settings.GotoVersion(s) }, 
            };

            try
            {
                options.Parse(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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
                Console.WriteLine(e.Message);
            }
        }

        private static void DoManatee(InputParameters settings)
        {
            if(settings.Command == Command.Derive)
            {
                var deriver = new MigrationDeriver(settings.MigrationFolder, settings.Connection);
                deriver.DoDerive();
            }

            var migrator = new Migrator(settings.MigrationFolder, settings.Connection);

            // List
            if(settings.Command == Command.List)
            {
                Console.WriteLine("Current Version: {0}", migrator.CurrentVersion);
                int counter = 0;
                foreach(var migration in migrator.Migrations)
                {
                    Console.WriteLine("{0}: {1}", ++counter, migration.Key);
                }
                return;
            }

            // Execute
            if(settings.Command == Command.Goto)
            {
                var destinationVersion = settings.DesitinationVersion ?? migrator.Migrations.Count;
                Console.WriteLine("Current Version: {0}", migrator.CurrentVersion);
                Console.WriteLine("Going to Version: {0}", destinationVersion);

                migrator.Migrate(destinationVersion);
            }
        }
    }
}
