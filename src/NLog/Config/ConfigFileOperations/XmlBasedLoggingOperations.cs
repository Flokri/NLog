using System.Runtime.CompilerServices;

#if !NETSTANDARD1_3 && !NETSTANDARD1_5 && !NETSTANDARD2_0 && !NET35 && !NET40
namespace NLog.Config.ConfigFileOperations
{
    using System;
    using System.Linq;
    using System.Xml.Linq;
    using System.Reflection;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using NLog.Config;
    using NLog.Targets;
    using NLog.Internal;

    class XmlBasedLoggingOperations
    {
        private XDocument _configFile;
        private String _filename;
        private XNamespace _ns;

        public XmlBasedLoggingOperations(string filename)
        {
            _configFile = LoadConfigurationFile(filename);
            _ns = "http://www.nlog-project.org/schemas/NLog.xsd";
        }

        /// <summary>
        /// Load the xml based configuration file and stores it in a xml document. 
        /// </summary>
        /// <param name="filename">The filename (full path) to the original xml based configuration file.</param>
        /// <returns>A xml document representing the original nlog configiguration file.</returns>
        private XDocument LoadConfigurationFile(string filename)
        {
            _filename = filename;
            return XDocument.Load(filename);
        }

        /// <summary>
        /// Will save the target to the config file.
        /// The method checks if the target is new or only a modified (name property).
        /// </summary>
        /// <param name="target">The target taht should be saved.</param>
        /// <returns>Returns if the process was sucessfull or not.</returns>
        public bool SaveTarget(Target target)
        {
            return CreateAndSaveTarget(target);
        }

        public bool SaveRule(LoggingRule rule)
        {
            return CreateAndSaveRule(rule);
        }

        /// <summary>
        /// Will save all targets to the base config file.
        /// </summary>
        /// <param name="targets">A List of all current targets.</param>
        /// <param name="rules">A list of all current rules.</param>
        public void SaveAll(List<Target> targets, List<LoggingRule> rules)
        {
            targets.ForEach(t =>
            {
                SaveTarget(t);
            });

            rules.ForEach(r =>
            {
                SaveRule(r);
            });
        }

        /// <summary>
        /// Set the xml Attributes for the target. Xml Attributes are target properties.
        /// </summary>
        /// <param name="target">The target to set the attributes.</param>
        /// <returns>Returns if the operation was successful or not.</returns>
        private bool CreateAndSaveTarget(Target target)
        {
            try
            {
                //Get all properties with values
                List<PropertyInfo> properties = PropertyHelper.GetAllReadableProperties(target.GetType())
                    .Where(p => p.CustomAttributes.Where(c => c.AttributeType == typeof(ArrayParameterAttribute)).ToList().Count == 0 && p.GetValue(target) != null && !p.Name.Equals("Name")).ToList();

                //filter out all array params
                List<PropertyInfo> arrayParams = PropertyHelper.GetAllReadableProperties(target.GetType())
                    .Where(x => x.CustomAttributes.Count() > 0 && x.CustomAttributes
                        .FirstOrDefault(y => y.AttributeType == typeof(ArrayParameterAttribute)) != null)
                    .Where(z => z.GetValue(target) != null)
                    .ToList();

                //get the name and type property and add them as attribute
                XElement elem = new XElement(_ns + "target",
                        new XAttribute("name", target.Name),
                        new XAttribute("type", target.GetType().Name.Replace("Target", ""))
                    );

                //add the properties to the target node (as attributes) (except name and type)
                foreach (PropertyInfo property in properties) { elem.Add(new XElement(_ns + property.Name, GetClearedPropertyValues(property.GetValue(target)))); }

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
                                elem.Add(new XElement(_ns + arrayName, new XAttribute("name", entry.Name), new XAttribute("layout", entry.Layout.OriginalText)));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        continue;
                    }
                }

                //delete the old target node (if exists)
                if (_configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "targets").Elements().SingleOrDefault(x => x.Attribute("name").Value.Equals(target.Name)) != null)
                {
                    _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "targets").Elements().SingleOrDefault(x => x.Attribute("name").Value.Equals(target.Name)).Remove();
                }

                //save the modified target to the xml .config file
                _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "targets").Add(elem);
                _configFile.Save(_filename);

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

        private bool CreateAndSaveRule(LoggingRule rule)
        {
            try
            {
                //Get all properties with values
                List<PropertyInfo> properties = PropertyHelper.GetAllReadableProperties(rule.GetType())
                    .Where(p => p.GetValue(rule) != null && !p.Name.Equals("RuleName")).ToList();

                //Get the namepattern of the rule
                string name = "";
                PropertyInfo namepattern = properties.FirstOrDefault(p => p.Name.Equals("LoggerNamePattern"));
                if (namepattern != null)
                {
                    name = namepattern.GetValue(rule).ToString();
                }

                //Get the log levels for the rule
                string levels = "";
                PropertyInfo levelProp = properties.FirstOrDefault(p => p.Name.Equals("Levels"));
                if (namepattern != null)
                {
                    levels = GetRuleLevels(levelProp, rule);
                }

                //Get the the targets for this rule
                string writeTo = "";
                PropertyInfo targetProp = properties.FirstOrDefault(p => p.Name.Equals("Targets"));
                if (targetProp != null)
                {
                    int c = 0;
                    List<Target> targets = (List<Target>)targetProp.GetValue(rule);
                    targets.ForEach(t =>
                    {
                        if (c == targets.Count - 1)
                        {
                            writeTo += t.Name;
                        }
                        else
                        {
                            writeTo += t.Name + ",";
                        }
                        c++;
                    });
                }

                //get the name and type property and add them as attribute
                XElement elem = new XElement(_ns + "logger",
                        new XAttribute("name", name),
                        new XAttribute("levels", levels),
                        new XAttribute("writeTo", writeTo)
                    );

                //Check if there exists an equal rule in the base config file, if not save the rule
                if (_configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "logger")
                    .Elements()
                    .SingleOrDefault(x =>
                        x.Attribute("name").Value.Equals(rule.LoggerNamePattern) &&
                        x.Attribute("").Value.Equals(writeTo) &&
                        x.Attribute("levels").Value.Equals(levels)) == null)
                {
                    _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "logger").Add(elem);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        private string GetRuleLevels(PropertyInfo levelsProp, LoggingRule rule)
        {
            //cast the object from GetValue to it's type and then convert it into a list
            List<LogLevel> levels = new List<LogLevel>((ReadOnlyCollection<LogLevel>)levelsProp.GetValue(rule));

            string levelsString = "";
            int c = 0;
            //loop every log level and check if there is a max and min level or the levels are specified separately
            levels.ForEach(l =>
            {
                if (c == levels.Count - 1)
                {
                    levelsString += l;
                }
                else
                {
                    levelsString += l + ",";
                }
                c++;
            });

            return levelsString;
        }

        private bool SetRuleAttributes(LoggingRule rule, XElement ruleNode)
        {
            return false;
        }

        /// <summary>
        /// Remove the "'" (tick) at the beginning and endign from a string param.
        /// </summary>
        /// <param name="value">The property value to "clear".</param>
        /// <returns>Returns the property without the start and ending "'" (ticks).</returns>
        private object GetClearedPropertyValues(object value)
        {
            try
            {
                String temp = value.ToString();
                if (temp.StartsWith("'") && temp.EndsWith("'"))
                {
                    temp = temp.Remove(0, 1);
                    temp = temp.Remove(temp.Length - 1, 1);
                }

                return temp;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}
#endif
