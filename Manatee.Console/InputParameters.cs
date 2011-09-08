using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command
{
    public enum GotoMode
    {
        Specific,
        Previous,
        Last
    }

    public class InputParameters
    {
        public string Connection { get; set; }

        public string MigrationFolder { get; set; }

        public GotoMode GotoMode { get; private set; }

        public int? DesitinationVersion { get; private set; }

        public string Table { get; set; }

        public bool ShowHelp { get; set; }

        public Command Command { get; set; }

        public string Name { get; set; }

        protected bool CommandSet { get; set; }

        public InputParameters()
        {
            MigrationFolder = "migrations";
        }

        public bool IsValid
        {
            get
            {
                if (!CommandSet)
                    return false;

                if (Command == Command.Gen)
                    return !string.IsNullOrEmpty(Name);

                if (string.IsNullOrEmpty(Connection))
                    return false;
                
                return true;
            }
        }

        public void GotoVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return;

            // handle special cases
            var specials = new[]
                               {
                                   new {Key = "last", Mode = GotoMode.Last },
                                   new {Key = "previous", Mode = GotoMode.Previous },
                               };

            foreach(var item in specials)
            {
                if (version.Equals(item.Key, StringComparison.InvariantCultureIgnoreCase))
                {
                    GotoMode = item.Mode;
                    return;
                }
            }

            int versionNumber;
            bool success = int.TryParse(version, out versionNumber);

            if (!success)
                throw new ArgumentException("Unsupported version definition", "version");

            GotoMode = GotoMode.Specific;
            DesitinationVersion = versionNumber;
        }

        public void ParseCommand(string s)
        {
            Command command;
            bool success = Enum.TryParse<Command>(s, true, out command);

            if (!success)
                throw new ArgumentException(string.Format("Unknown command: {0}", s));

            Command = command;
            CommandSet = true;
        }
    }
}
