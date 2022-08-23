/*
Copyright(C) 2022

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see<http://www.gnu.org/licenses/>.
*/

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
