using MediaBrowser.Common.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChapterApi.options
{
    public static class ConfigurationExtension
    {
        public static ChapterApiOptions GetReportPlaybackOptions(this IConfigurationManager manager)
        {
            return manager.GetConfiguration<ChapterApiOptions>("chapter_api");
        }
        public static void SaveReportPlaybackOptions(this IConfigurationManager manager, ChapterApiOptions options)
        {
            manager.SaveConfiguration("chapter_api", options);
        }
    }

    public class ReportPlaybackOptionsFactory : IConfigurationFactory
    {
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new List<ConfigurationStore>
            {
                new ConfigurationStore
                {
                    Key = "chapter_api",
                    ConfigurationType = typeof (ChapterApiOptions)
                }
            };
        }
    }
}
