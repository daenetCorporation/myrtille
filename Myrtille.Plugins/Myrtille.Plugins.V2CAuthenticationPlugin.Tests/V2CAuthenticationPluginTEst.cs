using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Myrtille.Plugins.V2CAuthenticationPlugin.Tests
{
    [TestClass]
    public class V2CAuthenticationPluginTest
    {
        private V2CAuthenticationPlugin v2CAuthenticationPlugin;

        [TestInitialize]
        public void TestInitialize()
        {
            v2CAuthenticationPlugin = new V2CAuthenticationPlugin();
        }

        [TestMethod]
        public void TestCanProcess()
        {
            // Change to your test token value
            var token = "98337affc7ad4937be429cdc966fc719";
            var requestString = $"__EVENTTARGET=&__EVENTARGUMENT=&oneTimePassword={token}&connect=Connect%21";

            Assert.IsTrue(v2CAuthenticationPlugin.CanProcess(requestString));

            Assert.IsNotNull(v2CAuthenticationPlugin.UserName);
            Assert.IsNotNull(v2CAuthenticationPlugin.Password);
        }
    }
}
