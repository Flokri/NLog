#if !NETSTANDARD1_3 && !NETSTANDARD1_5 && !NETSTANDARD2_0 && !NET35 && !NET40
namespace NLog.Config.ConfigFileOperations
{
    using NLog.Common;
    using NLog.Internal;
    using NLog.Targets;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;

    class XmlBasedLoggingOperations
    {
        private XDocument _configFile;
        private String _filename;
        private readonly XNamespace _ns;

        /// <summary>
        /// Load the xml config file into a new XDcument and set the correct namespace for the config file. 
        /// </summary>
        /// <param name="filename"></param>
        public XmlBasedLoggingOperations(string filename)
        {
            _configFile = LoadConfigurationFile(filename);

            //the namespace is important for creating new XElements. 
            //If it's missing the elements will have a empyt "xmlns" namespace entry and will be invalid when re-reading the file
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

        /// <summary>
        /// Saves a rule to the xml config file.
        /// </summary>
        /// <param name="rule">The rule which should be saved.</param>
        /// <returns>Returns if the save process was successful (true) or not (false).</returns>
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
            RemoveAllTargets();
            targets.ForEach(t =>
            {
                SaveTarget(t);
            });

            RemoveAllRules();
            rules.ForEach(r =>
            {
                SaveRule(r);
            });
        }

        private void RemoveAllTargets() => _configFile.Descendants().SingleOrDefault(t => t.Name.LocalName == "targets").RemoveAll();

        private void RemoveAllRules() => _configFile.Descendants().SingleOrDefault(r => r.Name.LocalName == "rules").RemoveAll();

        /// <summary>
        /// Converts input to Type of default value or given as typeparam T
        /// </summary>
        /// <typeparam name="T">typeparam is the type in which value will be returned, it could be any type eg. int, string, bool, decimal etc.</typeparam>
        /// <param name="input">Input that need to be converted to specified type</param>
        /// <param name="value">defaultValue will be returned in case of value is null or any exception occures</param>
        /// <returns>Input is converted in Type of default value or given as typeparam T and returned</returns>
        public T To<T>(object input, T value)
        {
            var result = value;
            try
            {
                if (input == null || input == DBNull.Value)
                    return result;
                if (typeof(T).IsEnum)
                {
                    result = (T)Enum.ToObject(typeof(T), To(input, Convert.ToInt32(value)));
                }
                else
                {
                    result = (T)Convert.ChangeType(input, typeof(T));
                }
            }
            catch (Exception e)
            {
                InternalLogger.Error(e.Message);
            }

            return result;
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
                    .Where(p => p.CustomAttributes.Where(c => c.AttributeType == typeof(ArrayParameterAttribute) ||
                                                         c.AttributeType == typeof(DefaultValueAttribute) || 
                                                         c.AttributeType == typeof(NotPersistableAttribute))
                                                         .ToList()
                    .Count == 0 && p.GetValue(target) != null && !p.Name.Equals("Name"))
                    .ToList();

                //get all default properties which does not equal to the default (initial) value
                List<PropertyInfo> changedDefaultProperties = new List<PropertyInfo>();
                PropertyHelper.GetAllReadableProperties(target.GetType()).Where(p => p.CustomAttributes.Where(c => c.AttributeType == typeof(DefaultValueAttribute)).ToList().Count() > 0)
                    .ToList()
                    .ForEach(p =>
                    {
                     CustomAttributeTypedArgument data = p.CustomAttributes.First(c => c.AttributeType == typeof(DefaultValueAttribute)).ConstructorArguments[0];

                        MethodInfo method = typeof(XmlBasedLoggingOperations).GetMethod("To");
                        MethodInfo generic = method.MakeGenericMethod(data.ArgumentType);


                        if (!p.GetValue(target).Equals(generic.Invoke(this, new object[] { data.Value, null })))
                        {
                            changedDefaultProperties.Add(p);
                        }
                    });

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

                //set the array param properties
                SetTargetArrayParam(target, arrayParams, ref elem);

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

        /// <summary>
        /// Sets all the property which are typeof ArrayParameterAttribute to the new/modified target <paramref name="elem"/>.
        /// </summary>
        /// <param name="target">The target object.</param>
        /// <param name="arrayParams">A list of all array property params.</param>
        /// <param name="elem">The newly created target (as XElement).</param>
        private void SetTargetArrayParam(Target target, List<PropertyInfo> arrayParams, ref XElement elem)
        {
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
                    //write the exception to the internal log
                    InternalLogger.Error(e.Message);
                    continue;
                }
            }
        }

        /// <summary>
        /// Save a rule to the xml config file. The rule only will be saved if there is not already a equal rule in the config file. 
        /// </summary>
        /// <param name="rule">The rule object.</param>
        /// <returns>Returns if the rule was saved to the config file (true) or not (false).</returns>
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
                if (namepattern != null) { name = namepattern.GetValue(rule).ToString(); }

                //Get the log levels for the rule
                string levels = "";
                PropertyInfo levelProp = properties.FirstOrDefault(p => p.Name.Equals("Levels"));
                if (namepattern != null) { levels = GetRuleLevels(levelProp, rule); }

                //Get the the targets for this rule
                string writeTo = GetRuleTargetsAsString(rule, properties.FirstOrDefault(p => p.Name.Equals("Targets")));


                //get the name and type property and add them as attribute
                XElement elem = new XElement(_ns + "logger",
                        new XAttribute("name", name),
                        new XAttribute("levels", levels),
                        new XAttribute("writeTo", writeTo)
                    );


                //Check if there exists an equal rule in the base config file, if not save the rule
                List<XElement> existingRules = _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "rules")
                    .Elements()
                    .Where(x =>
                        x.Attribute("name").Value.Equals(rule.LoggerNamePattern) &&
                        x.Attribute("writeTo").Value.Equals(writeTo) &&
                        x.Attribute("levels").Value.Equals(levels)).ToList();
                //TODO implement a better check when needed 
                //CheckIfRuleExists(existingRules, elem, levels);

                //only save when not available
                if (existingRules == null || existingRules.Count == 0)
                {
                    _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "rules").Add(elem);
                    _configFile.Save(_filename);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        /// <summary>
        /// Checks if there exists an equal rule in the xml config file.
        /// </summary>
        /// <param name="existingRules">The rule that seems like its the sama eas the one you like to persist.</param>
        /// <param name="newRule">The rule you want to persist.</param>
        /// <param name="levels">A String contains all the log levels the rule should log to. (the levels have to be seperatet by ',' like "Info,Error")</param>
        /// <returns>Returns if the new rule was saved (true) or not (false).</returns>
        private bool CheckIfRuleExists(List<XElement> existingRules, XElement newRule, String levels)
        {
            if (existingRules != null && existingRules.Count() > 0)
            {
                foreach (XElement existingRule in existingRules)
                {
                    XAttribute specificLevel;
                    XAttribute minLevel;
                    XAttribute maxLevel;

                    specificLevel = existingRule.Attributes().FirstOrDefault(a => a.Name.LocalName.ToLower().Equals("levels"));
                    if (specificLevel != null && specificLevel.Value.Equals(levels))
                    {
                        return false;
                    }

                    //checks every combination of log levels
                    minLevel = existingRule.Attributes().FirstOrDefault(a => a.Name.LocalName.ToLower().Equals("minlevel"));
                    maxLevel = existingRule.Attributes().FirstOrDefault(a => a.Name.LocalName.ToLower().Equals("maxlevel"));
                    levels = levels.ToLower();
                    String[] splittedLevels = levels.Split(',');
                    if ((minLevel != null && maxLevel != null) && splittedLevels.Length == 2 && (minLevel.Value.ToLower().Equals(splittedLevels[0]) && maxLevel.Value.ToLower().Equals(splittedLevels[1])))
                    {
                        return false;
                    }
                    //else if (minLevel != null && splittedLevels.Length >= 1 && minLevel.Name.LocalName.ToLower().Equals("minlevel") && minLevel.Value.ToLower().Equals(splittedLevels[0]))
                    //{
                    //    return false;
                    //}
                    //else if (maxLevel != null && splittedLevels.Length >= 1 && maxLevel.Name.LocalName.ToLower().Equals("maxlevel") && maxLevel.Value.ToLower().Equals(splittedLevels[splittedLevels.Length - 1]))
                    //{
                    //    return false;
                    //}

                    if (levels.Equals("") && specificLevel == null && minLevel == null && maxLevel == null)
                    {
                        return false;
                    }
                }

                _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "rules").Add(newRule);
                _configFile.Save(_filename);
            }
            else
            {
                _configFile.Descendants().SingleOrDefault(p => p.Name.LocalName == "rules").Add(newRule);
                _configFile.Save(_filename);
            }

            return true;
        }

        /// <summary>
        /// Get the log levels of a rule. (seperated by ',' like "Info,Error")
        /// </summary>
        /// <param name="levelsProp">The log level property of the rule.</param>
        /// <param name="rule">The logging rule object.</param>
        /// <returns>Returns a string with all log levels the rule should log to.</returns>
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


        /// <summary>
        /// Find all targets a rule is valid for and returns these targets as string (target names seperated with ',' like "Target1,Target2").
        /// </summary>
        /// <param name="rule">The rule object.</param>
        /// <param name="targetProp">The target property of the rule object.</param>
        /// <returns>Returns a string with all target names the rule is valid for.</returns>
        private string GetRuleTargetsAsString(LoggingRule rule, PropertyInfo targetProp)
        {
            string writeTo = "";
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

            return writeTo;
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
