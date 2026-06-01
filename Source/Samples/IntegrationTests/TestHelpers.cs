using System;
using System.Linq;
using System.Xml.Linq;

namespace IntegrationTests
{
    internal static class TestHelpers
    {
        public static string ReadAppConfigValue(string appConfigPath, string key)
        {
            var doc = XDocument.Load(appConfigPath);
            var element = doc.Root?.Element("appSettings")?
                .Elements("add")
                .FirstOrDefault(e => e.Attribute("key")?.Value == key);
            return element?.Attribute("value")?.Value
                ?? throw new InvalidOperationException($"Key '{key}' not found in {appConfigPath}");
        }
    }
}
