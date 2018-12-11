using System;

namespace NLog.Config
{
    /// <summary>
    /// Marks a property as not persistable. Properties marked with this attribute could not be written to a xml config file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotPersistableAttribute : Attribute
    {
    }
}
