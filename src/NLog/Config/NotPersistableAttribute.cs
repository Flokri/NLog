using System;

namespace NLog.Config
{
    /// <summary>
    /// Marks a property as not persistable. Properties marked with this attribute could not be written to a xml config file.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
#if NET4_0 || NET4_5 && !NETSTANDARD1_0
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
    class NotPersistableAttribute : Attribute
    {
        public NotPersistableAttribute() { }
    }
}
