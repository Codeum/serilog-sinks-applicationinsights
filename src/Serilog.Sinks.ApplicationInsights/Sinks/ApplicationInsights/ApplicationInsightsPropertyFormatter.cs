using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.ApplicationInsights
{
    internal static class ApplicationInsightsPropertyFormatter
    {
        private static readonly JsonValueFormatter JsonValueFormatter = new JsonValueFormatter();

        public static void WriteValue(string key, LogEventPropertyValue value, IDictionary<string, string> properties)
        {
            if (value == null)
            {
                AppendProperty(properties, key, string.Empty);
                return;
            }
            var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
            JsonValueFormatter.Format(value, stringWriter);
            var formattedValue = stringWriter.ToString();
            if (formattedValue.Length >= 2 && formattedValue[0] == '"' && formattedValue[formattedValue.Length - 1] == '"')
                formattedValue = formattedValue.Substring(1, formattedValue.Length - 2);
            properties[key] = formattedValue;
        }

        private static void AppendProperty(IDictionary<string, string> propDictionary, string key, string value)
        {
            if (propDictionary.ContainsKey(key))
            {
                SelfLog.WriteLine("The key {0} is not unique after simplification. Ignoring new value {1}", key, value);
                return;
            }
            propDictionary.Add(key, value);
        }
    }
}