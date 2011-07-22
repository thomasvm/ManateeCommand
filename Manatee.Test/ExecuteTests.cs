using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Manatee.Test
{
    [TestFixture]
    public class ExecuteTests
    {
        [Test]
        public void Execute_WithArray_ConcatenesValues()
        {
            var migrator = new Migrator();

            string json = @"{execute: [
                                'SELECT current_timestamp, ',
                                'system_user'
                            ]}";

            string command = migrator.GetCommandFromJson(json);

            Assert.AreEqual(string.Format(@"SELECT current_timestamp, {0}system_user{0}", Environment.NewLine), command);
        }

        [Test]
        public void Execute_WithSingleValue_TakesValue()
        {
            var migrator = new Migrator();

            string json = @"{execute: 'SELECT current_timestamp' }";

            string command = migrator.GetCommandFromJson(json);

            Assert.AreEqual("SELECT current_timestamp", command);
        }
    }
}
