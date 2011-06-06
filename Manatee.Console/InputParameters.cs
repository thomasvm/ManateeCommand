﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Manatee.Command
{
    public class InputParameters
    {
        public string Connection { get; set; }

        public string MigrationFolder { get; set; }

        public bool GotoLast 
        { 
            get { return DesitinationVersion.HasValue; }
        }

        public int? DesitinationVersion { get; private set; }

        public bool ShowHelp { get; set; }

        public Command Command { get; set; }

        protected bool CommandSet { get; set; }

        public InputParameters()
        {
            MigrationFolder = "DB";
        }

        public bool IsValid
        {
            get
            {
                if (!CommandSet)
                    return false;

                if (string.IsNullOrEmpty(Connection))
                    return false;

                return true;
            }
        }

        public void GotoVersion(string version)
        {
            if (version.Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                DesitinationVersion = null;
                return;
            }

            int versionNumber;
            bool success = int.TryParse(version, out versionNumber);

            if (!success)
                throw new ArgumentException("Unsupported version definition", "version");

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