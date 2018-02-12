using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myrtille.Plugins.V2CAuthenticationPlugin
{
    public static class UrlHelper
    {
        public static Dictionary<string, string> DecodeUrlParams(this string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

        }
    }
}
