using ChapterApi.lib;
using ChapterApi.options;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ChapterApi
{
    public class LibraryMonitor : IServerEntryPoint
    {

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IItemRepository _ir;
        private readonly JobManager _jm;
        private readonly IFfmpegManager _ffmpeg;
        private readonly IServerConfigurationManager _config;

        private readonly object _syncLock = new object();
        private readonly HashSet<long> _episodesAdded = new HashSet<long>();
        private Timer _updateTimer = null;
        private const int _updateDuration = 60000;

        public LibraryMonitor(
                ILibraryManager libraryManager,
                ILogManager logger,
                IJsonSerializer jsonSerializer,
                IItemRepository itemRepository,
                IFfmpegManager ffmpeg,
                IServerConfigurationManager config)
        {
            _logger = logger.GetLogger("ChapterApi - LibraryMonitor");
            _libraryManager = libraryManager;
            _jsonSerializer = jsonSerializer;
            _ir = itemRepository;
            _ffmpeg = ffmpeg;
            _config = config;

            _jm = JobManager.GetInstance(_logger, _ir);
        }

        public void Run()
        {
            _logger.Info("Adding Add Item Event Monitor");
            _libraryManager.ItemAdded += ItemAdded;
            _libraryManager.ItemUpdated += ItemUpdated;
        }

        public void Dispose()
        {
            _libraryManager.ItemAdded -= ItemAdded;
            _libraryManager.ItemUpdated -= ItemUpdated;

            if (_updateTimer != null)
            {
                _updateTimer.Dispose();
                _updateTimer = null;
            }
        }

        void ItemUpdated(object sender, ItemChangeEventArgs e)
        {
            BaseItem item = e.Item;
            if (item == null || item.GetType() != typeof(Episode) || item.IsVirtualItem)
            {
                return;
            }

            ChapterApiOptions config = _config.GetReportPlaybackOptions();
            if (config.ProcessUpdatedItems == false)
            {
                return;
            }

            ProcessEpisodeEvent(item);
        }

        void ItemAdded(object sender, ItemChangeEventArgs e)
        {
            BaseItem item = e.Item;
            if (item == null || item.GetType() != typeof(Episode) || item.IsVirtualItem)
            {
                return;
            }

            ChapterApiOptions config = _config.GetReportPlaybackOptions();
            if (config.ProcessAddedItems == false)
            {
                return;
            }

            ProcessEpisodeEvent(item);
        }

        private void ProcessEpisodeEvent(BaseItem item)
        {
            _logger.Info("Episode Event : " + item.InternalId);

            lock (_syncLock)
            {
                _episodesAdded.Add(item.InternalId);

                if (_updateTimer == null)
                {
                    _updateTimer = new Timer(ProcessItemsTimerCallback, null, _updateDuration, Timeout.Infinite);
                }
                else
                {
                    _updateTimer.Change(_updateDuration, Timeout.Infinite);
                }
            }
        }

        private void ProcessItemsTimerCallback(object state)
        {
            _logger.Info("ProcessItemsTimerCallback");

            lock (_syncLock)
            {
                Dictionary<string, List<Episode>> grouped_items = GroupItems(_episodesAdded);

                foreach(string key in grouped_items.Keys)
                {
                    AddEpisodeJob(key, grouped_items[key]);
                }
                
                _episodesAdded.Clear();
            }
        }

        Dictionary<string, List<Episode>> GroupItems(HashSet<long> item_ids)
        {
            Dictionary<string, List<Episode>> grouped_items = new Dictionary<string, List<Episode>>();
            string imdb_name = MetadataProviders.Imdb.ToString();

            _logger.Info("Grouping Items : " + string.Join(",", item_ids));

            foreach (long item_id in item_ids)
            {
                BaseItem item = _libraryManager.GetItemById(item_id);
                if (item != null && item.GetType() == typeof(Episode))
                {
                    Episode episode = item as Episode;
                    if (episode != null && episode.Series != null && episode.Series.ProviderIds != null)
                    {
                        string imdb_id = null;
                        if (episode.Series.ProviderIds.ContainsKey(imdb_name))
                        {
                            imdb_id = episode.Series.ProviderIds[imdb_name];
                        }
                        imdb_id = imdb_id ?? "";
                        imdb_id = imdb_id.Trim().ToLower();
                        if(!string.IsNullOrEmpty(imdb_id))
                        {
                            // we have the imdb now add the epp
                            if(grouped_items.ContainsKey(imdb_id) == false)
                            {
                                grouped_items.Add(imdb_id, new List<Episode>());
                            }
                            grouped_items[imdb_id].Add(episode);
                        }
                    }
                }
            }

            return grouped_items;
        }

        void AddEpisodeJob(string imdb, List<Episode> episodes)
        {
            Dictionary<string, List<IntroInfo>> intro_data = _jm.GetIntroData();
            if (intro_data.ContainsKey(imdb) == false)
            {
                _logger.Debug("No intro data for series : " + imdb);
                return;
            }

            List<IntroInfo> intro_cp_info_items = new List<IntroInfo>();
            foreach (IntroInfo episode_intros in intro_data[imdb])
            {
                episode_intros.cp_data_bytes = Convert.FromBase64String(episode_intros.cp_data);

                string cp_md5_string = "";
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(episode_intros.cp_data_bytes);
                    cp_md5_string = BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
                }

                if (episode_intros.cp_data_md5 == cp_md5_string)
                {
                    intro_cp_info_items.Add(episode_intros);
                }
                else
                {
                    _logger.Debug("Mismatch on Intro MD5 : " + episode_intros.cp_data_md5);
                }
            }

            if(intro_cp_info_items.Count == 0)
            {
                _logger.Debug("No valid intro data");
            }

            DetectionJob job = new DetectionJob();
            job.ffmpeg_path = _ffmpeg.FfmpegConfiguration.EncoderPath;
            job.intro_info_list = intro_cp_info_items;
            job.auto_insert = true;

            ChapterApiOptions config = _config.GetReportPlaybackOptions();
            job.keep_finished_for = config.KeepFinishdJobFor;
            job.threshold = config.DetectionThreshold;

            string series_name = "";
            foreach (Episode episode in episodes)
            {
                if(string.IsNullOrEmpty(series_name))
                {
                    series_name = episode.SeriesName;
                }

                DetectionJobItem job_item = new DetectionJobItem();
                job_item.item = episode;

                string s_no = (episode.ParentIndexNumber ?? 0).ToString("D2");
                string e_no = (episode.IndexNumber ?? 0).ToString("D2");
                string item_name = "s" + s_no + "e" + e_no + " - " + episode.Name;
                job_item.name = item_name;
                job.items.Add(job_item);
            }

            job.name = series_name + " (Auto)";

            _jm.AddJob(job);
        }

    }
}
