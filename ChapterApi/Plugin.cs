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

using ChapterApi.lib;
using ChapterApi.options;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ChapterApi
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage, IDisposable
    {
        public override string Name => "Chapter API";
        public override Guid Id => new Guid("64d8705e-c1e2-401f-9b64-2592aebde8eb");
        public override string Description => "View and edit chapters";
        public PluginConfiguration PluginConfiguration => Configuration;

        private readonly ILogger _logger;
        private readonly JobManager _jm;
        private readonly ILibraryManager _libraryManager;

        public Plugin(
            ILogManager logger,
            IApplicationPaths applicationPaths,
            IItemRepository ir,
            IJsonSerializer jsonSerializer,
            IServerConfigurationManager config,
            IXmlSerializer xmlSerializer,
            ILibraryManager libraryManager) : base(applicationPaths, xmlSerializer)
        {
            _logger = logger.GetLogger("ChapterApi - Plugin");
            _libraryManager = libraryManager;
            _jm = JobManager.GetInstance(_logger, ir);
            _logger.Info("Plugin Loaded");

            try
            {
                LoadIntroData(config, applicationPaths, jsonSerializer);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load IntroInfo data : " + ex.Message);
            }
            
        }

        private void LoadIntroData(IServerConfigurationManager config, IApplicationPaths applicationPaths, IJsonSerializer jsonSerializer)
        {
            ChapterApiOptions config_data = config.GetReportPlaybackOptions();
            if (string.IsNullOrEmpty(config_data.IntroDataPath))
            {
                string new_data_path = Path.Combine(applicationPaths.DataPath, "ChapterApiIntroData");
                DirectoryInfo ndp = new DirectoryInfo(new_data_path);
                if (!ndp.Exists)
                {
                    ndp.Create();
                }
                config_data.IntroDataPath = ndp.FullName;
                config.SaveConfiguration("chapter_api", config_data);
            }

            DirectoryInfo di = new DirectoryInfo(config_data.IntroDataPath);
            if (di.Exists)
            {
                IntroDataManager idm = new IntroDataManager(_logger, jsonSerializer, _libraryManager);
                Dictionary<string, List<IntroInfo>> intro_data = idm.LoadIntroDataFromPath(di);
                _jm.SetIntroData(intro_data);
            }
        }

        public void Dispose()
        {
            _jm.StopWorkerThread();
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "chapters",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.chapters.html",
                    EnableInMainMenu = true
                },
                new PluginPageInfo
                {
                    Name = "chapters.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.chapters.js"
                },
                new PluginPageInfo
                {
                    Name = "summary",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.summary.html",
                },
                new PluginPageInfo
                {
                    Name = "summary.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.summary.js"
                },
                new PluginPageInfo
                {
                    Name = "detect",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.detect.html",
                },
                new PluginPageInfo
                {
                    Name = "detect.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.detect.js"
                },
                new PluginPageInfo
                {
                    Name = "options",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.options.html",
                },
                new PluginPageInfo
                {
                    Name = "options.js",
                    EmbeddedResourcePath = GetType().Namespace + ".Pages.options.js"
                }
            };
        }

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".Media.logo.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }

    }
}
