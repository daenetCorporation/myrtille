using System;
using System.Linq;
using Myrtille.Common.Interfaces;

namespace Myrtille.Plugins.V2CAuthenticationPlugin
{
    public class V2CAuthenticationPlugin : IAuthenticationPlugin
    {
        public bool CanProcess(string request)
        {
            if (request.Contains("v2cPassword"))
            {
                var reqParams = request.TrimStart('?')
                    .Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(parameter => parameter.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
                    .GroupBy(parts => parts[0],
                        parts => parts.Length > 2 ? string.Join("=", parts, 1, parts.Length - 1) : (parts.Length > 1 ? parts[1] : ""))
                    .ToDictionary(grouping => grouping.Key,
                        grouping => string.Join(",", grouping));

                return true;
            }

            return false;
        }

        public string Domain { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
