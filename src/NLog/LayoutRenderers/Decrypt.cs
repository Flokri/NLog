using NLog.Config;
using System;
using System.Security.Cryptography;
using System.Text;

namespace NLog.LayoutRenderers
{
#if !NETSTANDARD1_3 && !NETSTANDARD1_5 && !NETSTANDARD2_0 && !NET35 && !NET40
    /// <summary>
    /// A Layout Renderer which decrypts a string for user based dpapi encryption.
    /// </summary>
    [LayoutRenderer("decrypt")]
    public class DecryptRenderer : LayoutRenderer
    {
        /// <summary>
        /// Holds the cipher text.
        /// </summary>
        [RequiredParameter]
        public string Cipher { get; set; }

        /// <summary>
        /// Append the decrypted string.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="logEvent"></param>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            builder.Append(DecyptUsingDPAPI());
        }

        /// <summary>
        /// Decrypts a string using the user dpapi controller.
        /// </summary>
        /// <returns></returns>
        private string DecyptUsingDPAPI()
        {
            if (String.IsNullOrEmpty(Cipher))
            {
                return "";
            }

            byte[] enryptedBytes = Convert.FromBase64String(Cipher);
            byte[] decrypted = ProtectedData.Unprotect(enryptedBytes, null, DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(decrypted);
        }
    }
#endif
}
