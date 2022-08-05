﻿/*
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
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    //[Authenticated]
    public class AddDetectionJob : IReturn<Object>
    {
        public string IntroInfo { get; set; }
        public int ItemId { get; set; }
        public string JobType { get; set; }
    }

    [Route("/chapter_api/get_job_list", "GET", Summary = "Get list of jobs")]
    //[Authenticated]
    public class GetJobList : IReturn<Object>
    {
    }

    [Route("/chapter_api/get_job_info", "GET", Summary = "Get job info")]
    //[Authenticated]
    public class GetJobInfo : IReturn<Object>
    {
        public string id { get; set; }
    }

    [Route("/chapter_api/cancel_job", "GET", Summary = "Cancel a job")]
    //[Authenticated]
    public class CancelJob : IReturn<Object>
    {
        public string id { get; set; }
    }

    [Route("/chapter_api/remove_job", "GET", Summary = "Remove a job")]
    //[Authenticated]
    public class RemoveJob : IReturn<Object>
    {
        public string id { get; set; }
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

        private JobManager _jm;

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
            IHttpResultFactory httpResultFactory)
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

            _jm = JobManager.GetInstance(_logger);

            _logger.Info("ChapterApi - DetectApiEndpoint Loaded");
        }

        public IRequest Request { get; set; }

        public object Get(GetEpisodeList request)
        {
            List<Dictionary<string, object>> episode_list = new List<Dictionary<string, object>>();

            InternalItemsQuery query = new InternalItemsQuery();
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
                job_info.Add("Status", job.status);
                job_info.Add("Added", job.added.ToString("yyyy-MM-dd HH:mm:ss"));
                job_info.Add("ItemCount", job.items.Count);

                List<Dictionary<string, object>> job_items = new List<Dictionary<string, object>>();

                foreach(DetectionJobItem job_item in job.items)
                {
                    Dictionary<string, object> job_item_info = new Dictionary<string, object>();

                    job_item_info.Add("Name", job_item.item.Name);
                    job_item_info.Add("Status", job_item.status);
                    job_item_info.Add("Found", job_item.found_intro);
                    job_item_info.Add("StartTime", job_item.start_time);
                    job_item_info.Add("EndTime", job_item.end_time);
                    job_item_info.Add("Duration", job_item.duration_time);
                    job_item_info.Add("Time", job_item.detection_duration);

                    job_items.Add(job_item_info);
                }

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

        public object Post(AddDetectionJob request)
        {
            Dictionary<string, object> add_result = new Dictionary<string, object>();

            _logger.Info("ItemId      : " + request.ItemId);
            _logger.Info("JobType     : " + request.JobType);

            IntroInfo intro_cp_info = _jsonSerializer.DeserializeFromString(request.IntroInfo, typeof(IntroInfo)) as IntroInfo;

            if (intro_cp_info == null)
            {
                add_result.Add("Status", "Failed");
                add_result.Add("Message", "Failed to extract chromaprint data from submitted file");
                return add_result;
            }

            _logger.Info("series      : " + intro_cp_info.series);
            _logger.Info("season      : " + intro_cp_info.season);
            _logger.Info("tvdb        : " + intro_cp_info.tvdb);
            _logger.Info("imdb        : " + intro_cp_info.imdb);
            _logger.Info("tmdb        : " + intro_cp_info.tmdb);
            _logger.Info("duration    : " + intro_cp_info.duration);
            _logger.Info("extract     : " + intro_cp_info.extract);
            _logger.Info("cp_data_md5 : " + intro_cp_info.cp_data_md5);
            _logger.Info("cp_data     : " + intro_cp_info.cp_data);

            byte[] cp_byte_data = Convert.FromBase64String(intro_cp_info.cp_data);

            string cp_md5 = "";
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(cp_byte_data);
                cp_md5 = BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
            }

            if (intro_cp_info.cp_data_md5 != cp_md5)
            {
                add_result.Add("Status", "Failed");
                add_result.Add("Message", "MD5 mismatch");
                return add_result;
            }

            DetectionJob job = new DetectionJob();
            job.ffmpeg_path = _ffmpeg.FfmpegConfiguration.EncoderPath;
            job.intro_info = intro_cp_info;
            job.intro_cp_data = cp_byte_data;

            job.name = intro_cp_info.series + " (" + request.JobType + ")";

            if (request.JobType == "series" || request.JobType == "season")
            {
                // get item list
                InternalItemsQuery query = new InternalItemsQuery();
                query.Recursive = true;
                query.IncludeItemTypes = new string[] { "Episode" };
                query.ParentIds = new long[] { request.ItemId };
                BaseItem[] episode_list = _libraryManager.GetItemList(query);
                foreach (BaseItem episode in episode_list)
                {
                    DetectionJobItem job_item = new DetectionJobItem();
                    job_item.item = episode;
                    job.items.Add(job_item);
                }

            }
            else if(request.JobType == "episode")
            {
                Guid item_guid = _libraryManager.GetGuid(request.ItemId);
                BaseItem episode = _libraryManager.GetItemById(item_guid);

                DetectionJobItem job_item = new DetectionJobItem();
                job_item.item = episode;
                job.items.Add(job_item);
            }

            add_result.Add("ItemCount", job.items.Count);

            _jm.AddJob(job);

            add_result.Add("Status", "Added");

            return add_result;
        }

    }
}