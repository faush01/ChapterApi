using ChapterApi.lib;
using ChapterApi.options;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChapterApi.Tasks
{
    public class TaskGetLatest : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly IServerConfigurationManager _config;
        private readonly IJsonSerializer _jsonSerializer; 
        private readonly ILibraryManager _libraryManager;
        private readonly IItemRepository _ir;

        private readonly JobManager _jm;

        string IScheduledTask.Name => "Update Intro DB";
        string IScheduledTask.Key => "ChapterApiUpdateIntroDB";
        string IScheduledTask.Description => "Downloads the latest and reloads the Intro DB";
        string IScheduledTask.Category => "Chapter API";

        public TaskGetLatest(
            ILogManager logger,
            IServerConfigurationManager config,
            IJsonSerializer jsonSerializer,
            IItemRepository itemRepository,
            ILibraryManager libraryManager)
        {
            _logger = logger.GetLogger("ChapterApi - TaskGetLatest");
            _config = config;
            _jsonSerializer = jsonSerializer;
            _libraryManager = libraryManager;
            _ir = itemRepository;

            _jm = JobManager.GetInstance(_logger, _ir);
        }

        IEnumerable<TaskTriggerInfo> IScheduledTask.GetDefaultTriggers()
        {
            /*
            var trigger = new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromHours(3.5).Ticks //3:30am
            };
            return new[] { trigger };
            */
            return new TaskTriggerInfo[0];
        }

        async Task IScheduledTask.Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            await Task.Run(() =>
            {
                _logger.Info("Downloading and reloading new Intro DB data");

                DownloadLatest();
                ReloadIntroData();

            }, cancellationToken);
        }

        private bool ReloadIntroData()
        {
            ChapterApiOptions config = _config.GetReportPlaybackOptions();

            DirectoryInfo di = new DirectoryInfo(config.IntroDataPath);
            if (di.Exists == false)
            {
                _logger.Info("IntroDataPath not set");
                return false;
            }

            IntroDataManager idm = new IntroDataManager(_logger, _jsonSerializer, _libraryManager);
            Dictionary<string, List<IntroInfo>> intro_data = idm.LoadIntroDataFromPath(di);
            _jm.SetIntroData(intro_data);

            int item_count = 0;
            int series_count = 0;
            foreach (string key in intro_data.Keys)
            {
                series_count++;
                foreach (IntroInfo info in intro_data[key])
                {
                    item_count++;
                }
            }

            _logger.Info("Reloaded Intro Data : " + series_count + " series " + item_count + " items");

            return true;
        }

        private bool DownloadLatest()
        {
            ChapterApiOptions config = _config.GetReportPlaybackOptions();

            if(String.IsNullOrEmpty(config.IntroDataPath))
            {
                _logger.Info("IntroDataPath not set");
                return false;
            }

            DirectoryInfo di = new DirectoryInfo(config.IntroDataPath);
            if(!di.Exists)
            {
                _logger.Info("IntroDataPath does not exist");
                return false;
            }

            if(string.IsNullOrEmpty(config.IntroDataExternalUrl))
            {
                _logger.Info("Intro data Url not set");
                return false;
            }

            _logger.Info("Downloading from : " + config.IntroDataExternalUrl);

            string temp_local_path = Path.Combine(config.IntroDataPath, "theme_service_data.zip_temp");
            FileInfo fi = new FileInfo(temp_local_path);
            if (fi.Exists)
            {
                fi.Delete();
            }

            _logger.Info("Downloading to : " + temp_local_path);

            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(config.IntroDataExternalUrl, temp_local_path);
                }
            }
            catch (Exception e)
            {
                _logger.Info("Intro data failed to download : " + e.Message);
                return false;
            }

            fi = new FileInfo(temp_local_path);
            if (!fi.Exists)
            {
                _logger.Info("Intro data file failed to downloaded");
                return false;
            }

            string local_path = Path.Combine(config.IntroDataPath, "theme_service_data.zip");
            FileInfo nfi = new FileInfo(local_path);
            if (nfi.Exists)
            {
                nfi.Delete();
            }

            _logger.Info("Renaming to : " + local_path);

            fi.MoveTo(nfi.FullName);

            nfi = new FileInfo(local_path);

            _logger.Info("Downloaded : theme_service_data.zip - size :" + nfi.Length);

            return true;
        }

    }
}
