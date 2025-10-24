using Microsoft.Extensions.Configuration;
using System.IO;

namespace TestBookletProcessor.WPF
{
    public static class ConfigurationHelper
    {
        public static IConfigurationRoot LoadConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            return builder.Build();
        }
    }
}