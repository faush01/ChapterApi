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
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ChapterApi
{
    [Route("/chapter_api/get_series_list", "GET", Summary = "Get list of series")]
    [Authenticated]
    public class GetSeriesList : IReturn<Object>
    {
    }

    [Route("/chapter_api/get_season_list", "GET", Summary = "Get list of seasons")]
    [Authenticated]
    public class GetSeasonList : IReturn<Object>
    {
        public int id { get; set; }
    }

    [Route("/chapter_api/get_episode_list", "GET", Summary = "Get list of episodes")]
    [Authenticated]
    public class GetEpisodeList : IReturn<Object>
    {
        public int id { get; set; }
    }

    [Route("/chapter_api/add_detection_job", "POST", Summary = "Add detection job")]
    [Authenticated]
    public class AddDetectionJob : IReturn<Object>
    {
        public string ZipData { get; set; }
        public string IntroInfo { get; set; }
        public int ItemId { get; set; }
        public string JobType { get; set; }
        public bool AutoInsert { get; set; } = false;
    }

    [Route("/chapter_api/get_job_list", "GET", Summary = "Get list of jobs")]
    [Authenticated]
    public class GetJobList : IReturn<Object>
    {
    }

    [Route("/chapter_api/get_job_info", "GET", Summary = "Get job info")]
    [Authenticated]
    public class GetJobInfo : IReturn<Object>
    {
        public string id { get; set; }
    }

    [Route("/chapter_api/cancel_job", "GET", Summary = "Cancel a job")]
    [Authenticated]
    public class CancelJob : IReturn<Object>
    {
        public string id { get; set; }
    }

    [Route("/chapter_api/remove_job", "GET", Summary = "Remove a job")]
    [Authenticated]
    public class RemoveJob : IReturn<Object>
    {
        public string id { get; set; }
    }

    [Route("/chapter_api/insert_chapters", "GET", Summary = "Insert detected chapters")]
    [Authenticated]
    public class InsertChapters : IReturn<Object>
    {
        public string id { get; set; }
    }

    [Route("/chapter_api/get_job_item", "GET", Summary = "Gets info for a job work item")]
    [Authenticated]
    public class GetJobItem : IReturn<Object>
    {
        public string id { get; set; }
        public int item_index { get; set; }
    }

    [Route("/chapter_api/reload_intro_data", "GET", Summary = "Reloads the intro DB form the data path")]
    [Authenticated]
    public class ReloadIntroData : IReturn<Object>
    {
    }

    [Route("/chapter_api/intro_data_stats", "GET", Summary = "Intro DB stats")]
    [Authenticated]
    public class IntroDataStats : IReturn<Object>
    {
    }

    [Route("/chapter_api/download_intro_data", "GET", Summary = "Intro DB download")]
    [Authenticated]
    public class DownloadIntroData : IReturn<Object>
    {
    }

    public class DetectApiEndpoint : IService, IRequiresRequest
    {
        private readonly ISessionManager _sessionManager;
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly IFileSystem _fileSystem;
        private readonly IServerConfigurationManager _config;
        private readonly IUserManager _userManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IAuthorizationContext _ac;
        private readonly IItemRepository _ir;
        private readonly IFfmpegManager _ffmpeg;
        private readonly IHttpResultFactory _hrf;
        private readonly IServerApplicationHost _appHost;

        private readonly JobManager _jm;

        public DetectApiEndpoint(ILogManager logger,
            IFileSystem fileSystem,
            IServerConfigurationManager config,
            IJsonSerializer jsonSerializer,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            IAuthorizationContext authContext,
            IUserDataManager userDataManager,
            IItemRepository itemRepository,
            IFfmpegManager ffmpegManager,
            IHttpResultFactory httpResultFactory,
            IServerApplicationHost appHost)
        {
            _logger = logger.GetLogger("ChapterApi - DetectApiEndpoint");
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            _config = config;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _ac = authContext;
            _userDataManager = userDataManager;
            _ir = itemRepository;
            _ffmpeg = ffmpegManager;
            _hrf = httpResultFactory;
            _appHost = appHost;

            _jm = JobManager.GetInstance(_logger, _ir);

            _logger.Info("ChapterApi - DetectApiEndpoint Loaded");
        }

        public IRequest Request { get; set; }

        public object Get(GetJobItem request)
        {
            Dictionary<string, object> job_item_info = new Dictionary<string, object>();

            Dictionary<string, DetectionJob> jobs = _jm.GetJobList();

            if (jobs.ContainsKey(request.id))
            {
                DetectionJob job = jobs[request.id];

                if(request.item_index < 0 || request.item_index >= job.items.Count)
                {
                    job_item_info.Add("Result", "Job Item Index not valid");
                    return job_item_info;
                }

                DetectionJobItem job_item = job.items[request.item_index];

                job_item_info.Add("Name", job_item.name);
                job_item_info.Add("ExtractTime", job_item.job_extract_time);
                job_item_info.Add("DetectTime", job_item.job_detect_time);
                job_item_info.Add("TotalTime", job_item.job_total_time);
                job_item_info.Add("Status", job_item.status);

                if (job_item.detection_result != null)
                {
                    job_item_info.Add("FoundIntro", job_item.detection_result.found_intro);
                    job_item_info.Add("IntroMD5", job_item.detection_result.intro_info.cp_data_md5);
                    job_item_info.Add("DistanceSum", job_item.detection_result.sum_distance);
                    job_item_info.Add("DistanceMax", job_item.detection_result.max_distance);
                    job_item_info.Add("DistanceAvg", job_item.detection_result.avg_distance);
                    job_item_info.Add("DistanceMin", job_item.detection_result.min_distance);
                    job_item_info.Add("DistanceThreshold", job_item.detection_result.dist_threshold);
                    job_item_info.Add("MinOffset", job_item.detection_result.min_offset);
                    job_item_info.Add("SearchDistances", job_item.detection_result.distances);
                }
                else
                {
                    job_item_info.Add("FoundIntro", false);
                    job_item_info.Add("IntroMD5", "");
                    job_item_info.Add("DistanceSum", "");
                    job_item_info.Add("DistanceMax", "");
                    job_item_info.Add("DistanceAvg", "");
                    job_item_info.Add("DistanceMin", "");
                    job_item_info.Add("DistanceThreshold", "");
                    job_item_info.Add("MinOffset", "");
                    job_item_info.Add("SearchDistances", "");
                }

            }
            else
            {
                job_item_info.Add("Result", "Job ID not found");
                return job_item_info;
            }

            return job_item_info;
        }

        public object Get(InsertChapters request)
        {
            Dictionary<string, object> add_result = new Dictionary<string, object>();
            add_result.Add("JobId", request.id);
            Dictionary<string, DetectionJob> jobs = _jm.GetJobList();

            if (!jobs.ContainsKey(request.id))
            {
                add_result.Add("Result", "Job Id not found : " + request.id);
                return add_result;
            }

            DetectionJob job = jobs[request.id];

            if (job.status != JobStatus.Complete)
            {
                add_result.Add("Result", "Job not complete : " + request.id);
                return add_result;
            }

            ChapterManager chaper_manager = new ChapterManager(_ir);

            int count = 0;
            foreach(DetectionJobItem job_item in job.items)
            {
                if(job_item.detection_result != null && job_item.detection_result.found_intro)
                {
                    chaper_manager.InsertChapters(job_item);
                    count++;
                }
            }

            add_result.Add("Result", "Inserted chapters : " + count);

            return add_result;
        }

        public object Get(GetEpisodeList request)
        {
            List<Dictionary<string, object>> episode_list = new List<Dictionary<string, object>>();

            InternalItemsQuery query = new InternalItemsQuery();
            query.IsVirtualItem = false;
            query.IncludeItemTypes = new string[] { "Episode" };
            query.ParentIds = new long[] { request.id };
            BaseItem[] results = _libraryManager.GetItemList(query);

            foreach (BaseItem item in results)
            {
                Dictionary<string, object> episode = new Dictionary<string, object>();

                episode.Add("Name", item.Name);
                episode.Add("Id", item.InternalId);

                episode_list.Add(episode);
            }

            return episode_list;
        }

        public object Get(GetSeasonList request)
        {
            List<Dictionary<string, object>> season_list = new List<Dictionary<string, object>>();

            InternalItemsQuery query = new InternalItemsQuery();
            query.IncludeItemTypes = new string[] { "Season" };

            (string, SortOrder)[] ord = new (string, SortOrder)[1];
            ord[0] = ("SortName", SortOrder.Ascending);
            query.OrderBy = ord;

            query.ParentIds = new long[] { request.id };

            BaseItem[] results = _libraryManager.GetItemList(query);

            foreach (BaseItem item in results)
            {
                Dictionary<string, object> season = new Dictionary<string, object>();

                season.Add("Name", item.Name);
                season.Add("Id", item.InternalId);

                season_list.Add(season);
            }

            return season_list;
        }

        public object Get(GetSeriesList request)
        {
            InternalItemsQuery query = new InternalItemsQuery();
            query.IncludeItemTypes = new string[] { "Series" };
            query.Recursive = true;

            (string, SortOrder)[] ord = new (string, SortOrder)[1];
            ord[0] = ("SortName", SortOrder.Ascending);
            query.OrderBy = ord;

            BaseItem[] results = _libraryManager.GetItemList(query);
            List<Dictionary<string, object>> series_list = new List<Dictionary<string, object>>();

            foreach (BaseItem item in results)
            {
                Dictionary<string, object> series = new Dictionary<string, object>();

                series.Add("Name", item.Name);
                series.Add("Id", item.InternalId);

                series_list.Add(series);
            }

            return series_list;
        }

        public object Get(GetJobList request)
        {
            List<Dictionary<string, object>> job_list = new List<Dictionary<string, object>>();

            Dictionary<string, DetectionJob> jobs = _jm.GetJobList();

            List<string> keys = jobs.Keys.ToList();
            keys.Sort();

            foreach (string job_id in keys)
            {
                DetectionJob job = jobs[job_id];
                Dictionary<string, object> job_info = new Dictionary<string, object>();

                job_info.Add("Id", job_id);
                job_info.Add("Name", job.name);
                job_info.Add("Count", job.items.Count);
                job_info.Add("Status", job.status);

                job_list.Add(job_info);
            }

            return job_list;
        }

        public object Get(GetJobInfo request)
        {
            Dictionary<string, object> job_info = new Dictionary<string, object>();
            Dictionary<string, DetectionJob> jobs = _jm.GetJobList();

            if(jobs.ContainsKey(request.id))
            {
                DetectionJob job = jobs[request.id];

                job_info.Add("Id", request.id);
                job_info.Add("Name", job.name);
                job_info.Add("AutoInsert", job.auto_insert);
                job_info.Add("Status", job.status);
                job_info.Add("Added", job.added.ToString("yyyy-MM-dd HH:mm:ss"));
                job_info.Add("ItemCount", job.items.Count);
                job_info.Add("IntroCount", job.intro_info_list.Count);

                job_info.Add("KeepFor", job.keep_finished_for);
                if(job.finished != null)
                {
                    job_info.Add("Finished", job.finished.Value.ToString("yyyy-MM-dd HH:mm:ss"));

                    DateTime delete_at = job.finished.Value + TimeSpan.FromHours(job.keep_finished_for);
                    TimeSpan time_to_delete = delete_at - DateTime.Now;
                    job_info.Add("RemoveIn", time_to_delete.ToString(@"d\.hh\:mm\:ss"));
                }
                else
                {
                    job_info.Add("Finished", "");
                    job_info.Add("RemoveIn", "");
                }
                
                List<Dictionary<string, object>> job_items = new List<Dictionary<string, object>>();
                
                int item_index = 0;
                foreach (DetectionJobItem job_item in job.items)
                {
                    Dictionary<string, object> job_item_info = new Dictionary<string, object>();

                    job_item_info.Add("Name", job_item.name);
                    job_item_info.Add("Index", item_index);
                    job_item_info.Add("Status", job_item.status);

                    if (job_item.detection_result != null)
                    {
                        job_item_info.Add("Found", job_item.detection_result.found_intro);
                        job_item_info.Add("StartTime", job_item.detection_result.start_time);
                        job_item_info.Add("EndTime", job_item.detection_result.end_time);
                        job_item_info.Add("Duration", job_item.detection_result.duration_time);
                    }
                    else
                    {
                        job_item_info.Add("Found", false);
                        job_item_info.Add("StartTime", (string)null);
                        job_item_info.Add("EndTime", (string)null);
                        job_item_info.Add("Duration", (string)null);
                    }
                    
                    job_item_info.Add("Time", job_item.job_duration);

                    job_items.Add(job_item_info);
                    item_index++;
                }

                job_items.Sort(delegate (Dictionary<string, object> c1, Dictionary<string, object> c2)
                {
                    string c1_str = c1["Name"] as string;
                    string c2_str = c2["Name"] as string;
                    int cmp_restlt = string.Compare(c1_str, c2_str, comparisonType: StringComparison.OrdinalIgnoreCase);
                    return cmp_restlt;
                });

                job_info.Add("Items", job_items);
            }

            return job_info;
        }

        public object Get(CancelJob request)
        {
            Dictionary<string, object> cancel_result = new Dictionary<string, object>();

            bool result = _jm.CancelJob(request.id);

            if (result)
            {
                cancel_result.Add("Result", "Canceled");
            }
            else
            {
                cancel_result.Add("Result", "Already Canceled");
            }

            return cancel_result;
        }

        public object Get(RemoveJob request)
        {
            Dictionary<string, object> remove_result = new Dictionary<string, object>();

            bool result = _jm.RemoveJob(request.id);
            if (result)
            {
                remove_result.Add("Result", "Removed");
            }
            else
            {
                remove_result.Add("Result", "Not Found");
            }

            return remove_result;
        }

        private void ExtractZippedIntroData(string zip_data, List<IntroInfo> intro_items)
        {
            byte[] zip_bytes = _jsonSerializer.DeserializeFromString(zip_data, typeof(byte[])) as byte[];
            _logger.Info("ZipBytesLen : " + zip_bytes.Length);

            using (MemoryStream zip_ms = new MemoryStream(zip_bytes))
            {
                using (ZipArchive archive = new ZipArchive(zip_ms, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        _logger.Info("ArchiveEntry: " + entry.Name);

                        if (!string.IsNullOrEmpty(entry.Name) && entry.Name.ToLower().EndsWith(".json"))
                        {
                            using (StreamReader st = new StreamReader(entry.Open()))
                            {
                                string entry_data = st.ReadToEnd();
                                //_logger.Info("Entry Data : " + entry_data);
                                IntroInfo info = _jsonSerializer.DeserializeFromString(entry_data, typeof(IntroInfo)) as IntroInfo;
                                if (info != null)
                                {
                                    _logger.Info("Adding info from zip : " + entry.Name);
                                    intro_items.Add(info);
                                }
                            }
                        }
                    }
                }
            }
        }

        public object Post(AddDetectionJob request)
        {
            Dictionary<string, object> add_result = new Dictionary<string, object>();

            _logger.Info("ItemId      : " + request.ItemId);
            _logger.Info("JobType     : " + request.JobType);

            List<IntroInfo> intro_cp_info_items = new List<IntroInfo>();

            if (!string.IsNullOrEmpty(request.ZipData)) // load data from submitted zip
            {
                _logger.Info("ZipDataLen  : " + request.ZipData.Length);
                //_logger.Info("ZipData     : " + request.ZipData);
                ExtractZippedIntroData(request.ZipData, intro_cp_info_items);
            }
            else if (!string.IsNullOrEmpty(request.IntroInfo)) // load data from submitted json
            {
                IntroInfo info = _jsonSerializer.DeserializeFromString(request.IntroInfo, typeof(IntroInfo)) as IntroInfo;
                if (info != null)
                {
                    intro_cp_info_items.Add(info);
                }
            }
            else // load data from internal intro DB data table
            {
                BaseItem base_item = _libraryManager.GetItemById(request.ItemId);
                IntroDataManager idm = new IntroDataManager(_logger, _jsonSerializer);
                idm.LookupInternalIntroDB(base_item, intro_cp_info_items, _jm);
            }

            if(intro_cp_info_items.Count == 0)
            {
                add_result.Add("Status", "Failed");
                add_result.Add("Message", "No intro info data available from submitted files or Intro DB");
                return add_result;
            }

            // extract and verfy cp data
            foreach(IntroInfo intro_data in intro_cp_info_items)
            {
                _logger.Info("series      : " + intro_data.series);
                _logger.Info("season      : " + intro_data.season);
                _logger.Info("episode     : " + intro_data.episode);
                _logger.Info("tvdb        : " + intro_data.tvdb);
                _logger.Info("imdb        : " + intro_data.imdb);
                _logger.Info("tmdb        : " + intro_data.tmdb);
                _logger.Info("extract     : " + intro_data.extract);
                _logger.Info("cp_data_md5 : " + intro_data.cp_data_md5);
                _logger.Info("cp_data     : " + intro_data.cp_data);

                intro_data.cp_data_bytes = Convert.FromBase64String(intro_data.cp_data);

                string cp_md5_string = "";
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(intro_data.cp_data_bytes);
                    cp_md5_string = BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
                }

                if (intro_data.cp_data_md5 != cp_md5_string)
                {
                    add_result.Add("Status", "Failed");
                    add_result.Add("Message", "Mismatch on Intro MD5 : " + intro_data.cp_data_md5);
                    return add_result;
                }
            }

            // build the job item
            DetectionJob job = new DetectionJob();
            job.ffmpeg_path = _ffmpeg.FfmpegConfiguration.EncoderPath;
            job.intro_info_list = intro_cp_info_items;
            job.auto_insert = request.AutoInsert;

            // set the keep for based on options
            ChapterApiOptions config = _config.GetReportPlaybackOptions();
            job.keep_finished_for = config.KeepFinishdJobFor;

            string series_name = "";

            if (request.JobType == "series" || request.JobType == "season")
            {
                // get item list
                InternalItemsQuery query = new InternalItemsQuery();
                query.IsVirtualItem = false;
                query.Recursive = true;
                query.IncludeItemTypes = new string[] { "Episode" };
                query.ParentIds = new long[] { request.ItemId };
                BaseItem[] episode_list = _libraryManager.GetItemList(query);
                foreach (BaseItem episode in episode_list)
                {
                    DetectionJobItem job_item = new DetectionJobItem();
                    job_item.item = episode;

                    string s_no = (episode.ParentIndexNumber ?? 0).ToString("D2");
                    string e_no = (episode.IndexNumber ?? 0).ToString("D2");
                    string item_name = "s" + s_no + "e" + e_no + " - " + episode.Name;
                    job_item.name = item_name;

                    job.items.Add(job_item);

                    if(string.IsNullOrEmpty(series_name))
                    {
                        Episode ep = episode as Episode;
                        series_name = ep.SeriesName;
                    }
                }

            }
            else if(request.JobType == "episode")
            {
                //Guid item_guid = _libraryManager.GetGuid(request.ItemId);
                //BaseItem episode = _libraryManager.GetItemById(item_guid);
                BaseItem episode = _libraryManager.GetItemById(request.ItemId);

                DetectionJobItem job_item = new DetectionJobItem();
                job_item.item = episode;

                string s_no = (episode.ParentIndexNumber ?? 0).ToString("D2");
                string e_no = (episode.IndexNumber ?? 0).ToString("D2");
                string item_name = "s" + s_no + "e" + e_no + " - " + episode.Name;
                job_item.name = item_name;

                job.items.Add(job_item);

                Episode ep = episode as Episode;
                series_name = ep.SeriesName;
            }

            job.name = series_name + " (" + request.JobType + ")";
            add_result.Add("ItemCount", job.items.Count);

            _jm.AddJob(job);

            add_result.Add("Status", "Added");

            return add_result;
        }

        public object Get(IntroDataStats request)
        {
            Dictionary<string, object> intro_data_stats = new Dictionary<string, object>();

            Dictionary<string, List<IntroInfo>> intro_data = _jm.GetIntroData();

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

            intro_data_stats.Add("SeriesCount", series_count);
            intro_data_stats.Add("ItemCount", item_count);

            return intro_data_stats;
        }

        public object Get(ReloadIntroData request)
        {
            Dictionary<string, object> reload_result = new Dictionary<string, object>();
            ChapterApiOptions config = _config.GetReportPlaybackOptions();

            DirectoryInfo di = new DirectoryInfo(config.IntroDataPath);
            if(di.Exists == false)
            {
                reload_result.Add("Result", "Failed");
                reload_result.Add("Message", "Data directory does not exist");
                return reload_result;
            }

            IntroDataManager idm = new IntroDataManager(_logger, _jsonSerializer);
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
                    _logger.Info(key + " : " + info.cp_data_md5 + " : " + info.series + " : " + info.season + " : " + info.episode);
                }
            }

            reload_result.Add("Result", "OK");
            reload_result.Add("Message", series_count + " series " + item_count + " items");
            return reload_result;
        }

        public object Get(DownloadIntroData request)
        {
            Dictionary<string, object> download_result = new Dictionary<string, object>();
            ChapterApiOptions config = _config.GetReportPlaybackOptions();

            DirectoryInfo di = new DirectoryInfo(config.IntroDataPath);
            if(!di.Exists)
            {
                download_result.Add("Result", "Failed");
                download_result.Add("Message", "Local data path does not exist");
                return download_result;
            }

            if(string.IsNullOrEmpty(config.IntroDataExternalUrl))
            {
                download_result.Add("Result", "Failed");
                download_result.Add("Message", "Data Url not set");
                return download_result;
            }

            string temp_local_path = Path.Combine(config.IntroDataPath, "theme_service_data.zip_temp");
            FileInfo fi = new FileInfo(temp_local_path);
            if (fi.Exists)
            {
                fi.Delete();
            }

            try
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile(config.IntroDataExternalUrl, temp_local_path);
                }
            }
            catch(Exception e)
            {
                download_result.Add("Result", "Failed");
                download_result.Add("Message", e.Message);
                return download_result;
            }

            fi = new FileInfo(temp_local_path);
            if (!fi.Exists)
            {
                download_result.Add("Result", "Failed");
                download_result.Add("Message", "File not downloaded");
                return download_result;
            }

            string local_path = Path.Combine(config.IntroDataPath, "theme_service_data.zip");
            FileInfo nfi = new FileInfo(local_path);
            if (nfi.Exists)
            {
                nfi.Delete();
            }

            fi.MoveTo(nfi.FullName);

            nfi = new FileInfo(local_path);
            string message = "Downloaded : theme_service_data.zip (" + nfi.Length + ")";

            download_result.Add("Result", "OK");
            download_result.Add("Message", message);
            return download_result;
        }
    }
}
