using System.Runtime.CompilerServices;

#if !NETSTANDARD1_3 && !NETSTANDARD1_5 && !NETSTANDARD2_0 && !NET35 && !NET40
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
    using System.Reflection;
    using NLog.Internal;

    class XmlBasedLoggingOperations
    {
        private readonly ReadOnlyCollection<Target> _allTargets;
        private XDocument _configFile;
        private String _filename;
        private XNamespace _xmlns;
        private XNamespace _xsi;

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
            _xmlns = "http://www.nlog-project.org/schemas/NLog.xsd";
            _xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");
            _filename = filename;
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
            try
            {
                var targetNode = _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "targets")
                    .Elements()
                    .SingleOrDefault(x => x.Attribute("name").Value.Equals(target.Name));

                if (targetNode == null) { return false; }

                return SetAttributes(target, targetNode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return false;
        }

        /// <summary>
        /// Creates a new target in the configuration file. 
        /// </summary>
        /// <param name="target">The new target.</param>
        /// <returns>Returns if the target could be created successfull.</returns>
        private bool CreateTarget(Target target)
        {
            try
            {
                //add a new xml node for the target
                _configFile.Descendants("targets").Last().Add(new XElement("target", new XAttribute("name", "created")));
                return SetAttributes(target, _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "targets").Elements().SingleOrDefault(x => x.Attribute("name").Value.Equals(target.Name)));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Set the xml Attributes for the target. Xml Attributes are target properties.
        /// </summary>
        /// <param name="target">The target to set the attributes.</param>
        /// <param name="targetNode">The target (as XElement)</param>
        /// <returns>Returns if the operation was successful or not.</returns>
        private bool SetAttributes(Target target, XElement targetNode)
        {
            try
            {
                //Get all properties with values
                List<PropertyInfo> properties = PropertyHelper.GetAllReadableProperties(target.GetType())
                    .Where(p => p.CustomAttributes.Where(c=>c.AttributeType == typeof(ArrayParameterAttribute)).ToList().Count == 0 && p.GetValue(target) != null && !p.Name.Equals("Name")).ToList();


                //filter out all array params
                PropertyInfo param = properties.FirstOrDefault(x => x.Name.Equals("Parameters"));
                List<PropertyInfo> arrayParams = properties
                    .Where(x => x.CustomAttributes.Count() > 0 && x.CustomAttributes
                        .FirstOrDefault(y => y.AttributeType == typeof(ArrayParameterAttribute)) != null)
                    .Where(z => z.GetValue(target) != null)
                    .ToList();

                //get the name and type property and add them as attribute
                XElement elem = new XElement("target",
                        new XAttribute("name", target.Name),
                        new XAttribute("type", target.GetType().Name.Replace("Target", ""))
                    );

                //add the properties to the target node (as attributes) (except name and type)
                foreach (PropertyInfo property in properties) { elem.Add(new XElement(property.Name, property.GetValue(target))); }

                //add all array params 
                foreach (PropertyInfo arrayParam in arrayParams)
                {
                    try
                    {
                        //get the name for the array sub node
                        String arrayName = arrayParam.CustomAttributes
                            .First(x => x.AttributeType == typeof(ArrayParameterAttribute))
                            .ConstructorArguments.ToList()[1].Value
                            .ToString();

                        Type arrayType = PropertyHelper.GetArrayItemType(arrayParam);
                        dynamic value = arrayParam.GetValue(target);

                        if (value.Count > 0)
                        {
                            foreach (dynamic entry in value)
                            {
                                elem.Add(new XElement(arrayName, new XAttribute("name", entry.Name), new XAttribute("layout", entry.Layout.OriginalText)));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }
                }

                (from node in targetNode.DescendantsAndSelf()
                 where node is XElement
                 from attr in node.Attributes()
                 where attr.IsNamespaceDeclaration && (attr.Name.LocalName == "xmlns" | attr.Name.LocalName.StartsWith("xmlns:"))
                 select attr).All(attr => { attr.Remove(); return true; });



                //add subnodes to the target for each parameter property

                //save the modified target to the xml .config file
                _configFile.Save(_filename);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}
#endif
