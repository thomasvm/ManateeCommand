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
                { "command=|c=", "Command, possible values can be 'list' or 'exec'. List is the default.", s => settings.ParseCommand(s) }, 
                { "folder=|f=", "Migrations folder", s => settings.MigrationFolder = s }, 
                { "con=", "Name of the connection", s => settings.Connection = s }, 
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
            var migrator = new Migrator(settings.MigrationFolder, settings.Connection);
            if(settings.Command == Command.List)
            {
                Console.WriteLine("Current Version: {0}", migrator.CurrentVersion);
                int counter = 0;
                foreach(var migration in migrator.Migrations)
                {
                    Console.WriteLine("{0}: {1}", ++counter, migration.Key);
                    Console.WriteLine("--------------");
                    Console.WriteLine(migration.Value);
                    Console.WriteLine();
                }
            }
        }
    }
}
