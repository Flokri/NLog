using System.Runtime.CompilerServices;

namespace NLog.Config.ConfigFileOperations
{
    using System;
    using System.Xml;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using NLog.Common;
    using NLog.Targets;
    using System.Linq;

    class XmlBasedLoggingOperations
    {
        private readonly ReadOnlyCollection<Target> _allTargets;
        private XmlDocument _configFile;

        public XmlBasedLoggingOperations(string filename, ReadOnlyCollection<Target> AllTargets)
        {
            _allTargets = AllTargets;
            _configFile = LoadConfigurationFile(filename);
        }

        /// <summary>
        /// Load the xml based configuration file and stores it in a xml document. 
        /// </summary>
        /// <param name="filename">The filename (full path) to the original xml based configuration file.</param>
        /// <returns>A xml document representing the original nlog configiguration file.</returns>
        private XmlDocument LoadConfigurationFile(string filename)
        {
#if !NETSTANDARD1_3 && !NETSTANDARD1_5
            XmlDocument doc = new XmlDocument();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.NameTable = new NameTable();

            XmlNamespaceManager ns = new XmlNamespaceManager(settings.NameTable);
            ns.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");

            XmlParserContext context = new XmlParserContext(null, ns, "", XmlSpace.Default);

            XmlReader test = XmlReader.Create(filename, settings, context);

            using (XmlReader reader = XmlReader.Create(filename, settings, context))
            {
                doc.Load(reader);
            }
            return doc;
#else
            return null;
#endif
        }

        public bool AddTarget(Target target)
        {
            if (_allTargets == null) { return false; }

            if (_allTargets.FirstOrDefault(x => x.Name.Equals(target.Name)) == null) { return CreateTarget(target); }
            else { return ModifyTarget(target); }
        }

        /// <summary>
        /// Modifies a target which is already in the configuration file
        /// </summary>
        /// <param name="target">The target to modify. </param>
        /// <returns>Returns if the target could be modified successfull.</returns>
        private bool ModifyTarget(Target target)
        {
            //Get the right xml node
            var node = _configFile.ChildNodes.Cast<XmlNode>().Where(x => x.Name.Equals("nlog"));

            //return false if the target is not in the config file
            if (node == null) { return false; }

            return false;
        }

        /// <summary>
        /// Creates a new target in the configuration file. 
        /// </summary>
        /// <param name="target">The new target.</param>
        /// <returns>Returns if the target could be created successfull.</returns>
        private bool CreateTarget(Target target)
        {
            return false;
        }
    }
}
