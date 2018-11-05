#if !NETSTANDARD1_3 && !NETSTANDARD1_5
namespace NLog.Config.ConfigFileOperations
{
    using System;
    using System.Xml;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Xml.Linq;

    using NLog.Common;
    using NLog.Targets;
    using System.Linq;

    class XmlBasedLoggingOperations
    {
        private readonly ReadOnlyCollection<Target> _allTargets;
        private XDocument _configFile;
        private String filename;

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
        private XDocument LoadConfigurationFile(string filename)
        {
            //XDocument doc = new XDocument();

            //XmlReaderSettings settings = new XmlReaderSettings();
            //settings.NameTable = new NameTable();

            //XmlNamespaceManager ns = new XmlNamespaceManager(settings.NameTable);
            //ns.AddNamespace("xsi", "http://www.w3.org/2001/XMLSchema-instance");

            //XmlParserContext context = new XmlParserContext(null, ns, "", XmlSpace.Default);

            //XmlReader test = XmlReader.Create(filename, settings, context);

            //using (XmlReader reader = XmlReader.Create(filename, settings, context))
            //{
            //    doc = XDocument.Load(reader);
            //}

            this.filename = filename;
            return XDocument.Load(filename);
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
            var targetNode = _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "targets")
                .Elements()
                .SingleOrDefault(x => x.Attribute("name").Value.Equals(target.Name));

            if (targetNode == null) { return false; }



            //convert the target in the derived target class
            targetNode.RemoveAttributes();

            var attributes = target.GetType().GetProperties().Where(x => x.GetCustomAttributes(true).Where(y=>y.GetType() == typeof(RequiredParameterAttribute))!=null);

            _configFile.Save(filename);


            //return false if the target is not in the config file
            //if (node == null) { return false; }

            return false;
        }

        private void RemoveAttributes()
        {

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
#endif
