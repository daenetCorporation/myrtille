using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using Myrtille.Common.Interfaces;
using Newtonsoft.Json.Linq;

namespace Myrtille.Plugins.V2CAuthenticationPlugin
{
    public class V2CAuthenticationPlugin : IAuthenticationPlugin
    {
        private const string oneTimePasswordString = "oneTimePassword";

        public bool CanProcess(string request)
        {
            if (request.Contains(oneTimePasswordString))
            {
                var reqParams = request.TrimStart('?')
                    .Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(parameter => parameter.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
                    .GroupBy(parts => parts[0],
                        parts => parts.Length > 2 ? string.Join("=", parts, 1, parts.Length - 1) : (parts.Length > 1 ? parts[1] : ""))
                    .ToDictionary(grouping => grouping.Key,
                        grouping => string.Join(",", grouping));


                if (reqParams.ContainsKey(oneTimePasswordString))
                {
                    var key = reqParams[oneTimePasswordString];

                    HttpClient client = new HttpClient();
                    var address = ConfigurationManager.AppSettings["V2CAuthenticationPlugin:AuthenticationEndpoint"];

                    if (String.IsNullOrEmpty(address))
                        return false;

                    Uri uri = new Uri(address);

                    var result = client.GetAsync(uri.AbsoluteUri + key).Result;

                    if (result.IsSuccessStatusCode)
                    {
                        var content = result.Content.ReadAsStringAsync().Result;

                        var obj = JObject.Parse(content);

                        UserName = obj.Value<string>("username");
                        Password = obj.Value<string>("password");

                        return true;
                    }
                }
            }

            return false;
        }

        public string Domain { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}
