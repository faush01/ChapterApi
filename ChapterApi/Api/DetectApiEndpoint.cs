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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ChapterApi.Api
{

    // http://localhost:8096/emby/chapter_api/get_series_list
    [Route("/chapter_api/get_series_list", "GET", Summary = "Get list of series")]
    [Authenticated]
    public class GetSeriesList : IReturn<Object>
    {
    }

    // http://localhost:8096/emby/chapter_api/get_season_list
    [Route("/chapter_api/get_season_list", "GET", Summary = "Get list of seasons")]
    [Authenticated]
    public class GetSeasonList : IReturn<Object>
    {
        public int id { get; set; }
    }

    // http://localhost:8096/emby/chapter_api/get_episode_list
    [Route("/chapter_api/get_episode_list", "GET", Summary = "Get list of episodes")]
    [Authenticated]
    public class GetEpisodeList : IReturn<Object>
    {
        public int id { get; set; }
    }

    // http://localhost:8096/emby/chapter_api/detect_season_intros
    [Route("/chapter_api/detect_season_intros", "POST", Summary = "Detect the intros")]
    //[Authenticated]
    public class DetectSeasonIntros : IReturn<Object>
    {
        public string IntroInfo { get; set; }
        public int SeasonId { get; set; }
    }

    // http://localhost:8096/emby/chapter_api/detect_episode_intro
    [Route("/chapter_api/detect_episode_intro", "POST", Summary = "Detect the intros")]
    //[Authenticated]
    public class DetectEpisodeIntro : IReturn<Object>
    {
        public string IntroInfo { get; set; }
        public int EpisodeId { get; set; }
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

        public object Post(DetectEpisodeIntro request)
        {
            Dictionary<string, object> detection_results = new Dictionary<string, object>();

            _logger.Info("EpisodeId    : " + request.EpisodeId);

            IntroInfo intro_cp_info = _jsonSerializer.DeserializeFromString(request.IntroInfo, typeof(IntroInfo)) as IntroInfo;

            if (intro_cp_info == null)
            {
                detection_results.Add("Status", "Failed");
                detection_results.Add("Message", "Failed to extract chromaprint data from submitted file");
                return detection_results;
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
                detection_results.Add("Status", "Failed");
                detection_results.Add("Message", "MD5 mismatch");
                return detection_results;
            }

            ProcessEpisode(
                detection_results, 
                request.EpisodeId, 
                intro_cp_info, 
                cp_byte_data);

            detection_results.Add("Status", "Ok");
            return detection_results;
        }

        private void ProcessEpisode(
            Dictionary<string, object> detection_results,
            long episode_id,
            IntroInfo intro_cp_info,
            byte[] theme_cp_byte_data)
        {

            Guid item_guid = _libraryManager.GetGuid(episode_id);
            BaseItem episode = _libraryManager.GetItemById(item_guid);

            TimeSpan duration = TimeSpan.FromSeconds(intro_cp_info.extract);
            _logger.Info("Extracting cp data for first " + duration.TotalSeconds + " seconds of episode");

            detection_results.Add("Name", episode.Name);
            detection_results.Add("Id", episode.InternalId);

            _logger.Info("Extracted CP from : " + episode.Name);
            byte[] episode_cp_data = ExtractChromaprint(duration, episode.Path);
            _logger.Info("Extracted CP data length : " + episode_cp_data.Length);

            detection_results.Add("CpDataLen", episode_cp_data.Length);

            FindBestOffset(episode_cp_data, duration, theme_cp_byte_data, detection_results);

        }

        public object Post(DetectSeasonIntros request)
        {
            Dictionary<string, object> detection_results = new Dictionary<string, object>();

            //_logger.Info("IntroInfo   : " + request.IntroInfo);
            _logger.Info("SeasonId    : " + request.SeasonId);

            IntroInfo intro_cp_info = _jsonSerializer.DeserializeFromString(request.IntroInfo, typeof(IntroInfo)) as IntroInfo;

            if(intro_cp_info == null)
            {
                detection_results.Add("Status", "Failed");
                detection_results.Add("Message", "Failed to extract chromaprint data from submitted file");
                return detection_results;
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

            if(intro_cp_info.cp_data_md5 != cp_md5)
            {
                detection_results.Add("Status", "Failed");
                detection_results.Add("Message", "MD5 mismatch");
                return detection_results;
            }

            ProcessSeason(
                detection_results,
                request.SeasonId,
                intro_cp_info,
                cp_byte_data);

            detection_results.Add("Status", "Ok");
            return detection_results;
        }

        private void ProcessSeason(
            Dictionary<string, object> detection_results, 
            int season_id,
            IntroInfo intro_cp_info,
            byte[] theme_cp_byte_data)
        {
            InternalItemsQuery query = new InternalItemsQuery();
            query.IncludeItemTypes = new string[] { "Episode" };
            query.ParentIds = new long[] { season_id };
            BaseItem[] episode_list = _libraryManager.GetItemList(query);

            TimeSpan duration = TimeSpan.FromSeconds(intro_cp_info.extract);
            _logger.Info("Extracting cp data for first " + duration.TotalSeconds + " seconds of episode");

            List<Dictionary<string, object>> episode_results = new List<Dictionary<string, object>>();

            foreach (BaseItem episode in episode_list)
            {
                Dictionary<string, object> episode_info = new Dictionary<string, object>();

                episode_info.Add("Name", episode.Name);
                episode_info.Add("Id", episode.InternalId);

                _logger.Info("Extracted CP from : " + episode.Name);
                byte[] episode_cp_data = ExtractChromaprint(duration, episode.Path);
                _logger.Info("Extracted CP data length : " + episode_cp_data.Length);

                episode_info.Add("CpDataLen", episode_cp_data.Length);

                FindBestOffset(episode_cp_data, duration, theme_cp_byte_data, episode_info);

                episode_results.Add(episode_info);
            }

            detection_results.Add("Episodes", episode_results);
        }

        private byte[] ExtractChromaprint(
            TimeSpan ts_duration,
            string media_path
            )
        {
            //TimeSpan ts_start = TimeSpan.FromSeconds(20);
            //TimeSpan ts_end = TimeSpan.FromSeconds(90);
            //TimeSpan ts_duration = ts_end - ts_start;

            string ffmpeg_path = _ffmpeg.FfmpegConfiguration.EncoderPath;
            //string ffmpeg_path = _config.CommonApplicationPaths.ProgramSystemPath;
            //ffmpeg_path = Path.Combine(ffmpeg_path, "ffmpeg");

            List<string> command_params = new List<string>();

            command_params.Add("-accurate_seek");
            //command_params.Add(string.Format("-ss {0}", ts_start));
            command_params.Add(string.Format("-t {0}", ts_duration));
            command_params.Add("-i \"" + media_path + "\"");
            command_params.Add("-ac 1");
            command_params.Add("-acodec pcm_s16le");
            command_params.Add("-ar 16000");
            command_params.Add("-c:v nul");
            command_params.Add("-f chromaprint");
            command_params.Add("-fp_format raw");
            command_params.Add("-");

            string param_string = string.Join(" ", command_params);

            _logger.Info("Extracting chromaprint : " + ffmpeg_path + " " + param_string);

            ProcessStartInfo start_info = new ProcessStartInfo(ffmpeg_path, param_string);
            start_info.RedirectStandardOutput = true;
            start_info.RedirectStandardError = false;
            start_info.UseShellExecute = false;
            start_info.CreateNoWindow = true;

            byte[] chroma_bytes = new byte[0];
            int return_code = -1;
            using (Process process = new Process() { StartInfo = start_info })
            {
                process.Start();

                FileStream baseStream = process.StandardOutput.BaseStream as FileStream;
                using (MemoryStream ms = new MemoryStream())
                {
                    int last_read = 0;
                    byte[] buffer = new byte[4096];
                    do
                    {
                        last_read = baseStream.Read(buffer, 0, buffer.Length);
                        ms.Write(buffer, 0, last_read);
                    } while (last_read > 0);

                    chroma_bytes = ms.ToArray();
                }

                return_code = process.ExitCode;
            }

            return chroma_bytes;
        }



        private bool FindBestOffset(
            byte[] episode_cp_bytes, 
            TimeSpan duration, 
            byte[] theme_cp_bytes,
            Dictionary<string, object> episode_info)
        {
            List<uint> episode_cp_uints = BytesToInts(episode_cp_bytes);
            List<uint> theme_cp_uints = BytesToInts(theme_cp_bytes);

            if (episode_cp_uints.Count == 0 || theme_cp_uints.Count == 0 || theme_cp_uints.Count > episode_cp_uints.Count)
            {
                _logger.Info("Error with cp data : episode[" + episode_cp_uints.Count + "] theme[" + theme_cp_uints.Count + "]");
                return false;
            }

            List<uint> distances = GetDistances(episode_cp_uints, theme_cp_uints);

            int? best_start_offset = GetBestOffset(distances, episode_info);

            if (best_start_offset == null)
            {
                episode_info.Add("Result", false);
                _logger.Info("Theme not found!");
                return false;
            }

            episode_info.Add("Result", true);

            // based on testing it looks like it is about 8.06 ints per second
            // based on the options used in the ffmpeg audio mixing and cp muxing
            // TODO: this need further investigation
            // https://github.com/acoustid/chromaprint/issues/45
            // double ints_per_sec = 8.06; // this is calculated by extracting a bunch of test data and comparing them
            // for now lets use the duration and extracted byte array length to calculate this 
            double ints_per_sec = (episode_cp_bytes.Length / duration.TotalSeconds) / 4;

            // also remember we are using int offsets, this is 4 bytes, we could get better
            // granularity by comparing bytes for byte and use actual byte offsets in our best match

            int theme_start = (int)(best_start_offset / ints_per_sec);
            TimeSpan ts_start = new TimeSpan(0, 0, theme_start);

            int theme_end = theme_start + (int)(theme_cp_uints.Count / ints_per_sec);
            TimeSpan ts_end = new TimeSpan(0, 0, theme_end);

            episode_info.Add("StartTime", ts_start.ToString(@"hh\:mm\:ss\.fff"));
            episode_info.Add("StartTimeTicks", ts_start.Ticks);

            episode_info.Add("EndTime", ts_end.ToString(@"hh\:mm\:ss\.fff"));
            episode_info.Add("EndTimeTicks", ts_end.Ticks);

            TimeSpan into_duration = ts_end - ts_start;

            episode_info.Add("Duration", into_duration.ToString(@"hh\:mm\:ss\.fff"));
            episode_info.Add("DurationTicks", into_duration.Ticks);

            _logger.Info("Theme At : " + ts_start + " - " + ts_end);

            return true;
        }

        private int? GetBestOffset(List<uint> distances,Dictionary<string, object> episode_info)
        {
            uint sum_distances = 0;
            uint min_dist = uint.MaxValue;
            int? min_offset = null;
            for (int x = 0; x < distances.Count; x++)
            {
                sum_distances += distances[x];
                if (distances[x] < min_dist)
                {
                    min_dist = distances[x];
                    min_offset = x;
                }
            }

            double average_distance = sum_distances / distances.Count;
            uint distance_threshold = (uint)(average_distance * 0.5);  // TODO: find a good threshold

            episode_info.Add("MinDistance", min_dist);
            episode_info.Add("AverageDistance", average_distance);
            episode_info.Add("DistanceThreshold", distance_threshold);
            episode_info.Add("MinDistanceOffset", min_offset);

            _logger.Info("Min Distance        : " + min_dist);
            _logger.Info("Average Distance    : " + average_distance);
            _logger.Info("Distance Threshold  : " + distance_threshold);
            _logger.Info("Min Distance Offset : " + min_offset);

            if (min_dist > distance_threshold)
            {
                episode_info.Add("MinDistanceFound", false);
                _logger.Info("Min distance was not below average distance threshold!");
                return null;
            }

            episode_info.Add("MinDistanceFound", true);

            return min_offset;
        }

        private List<uint> BytesToInts(byte[] cp_byte_data)
        {
            List<uint> cp_data = new List<uint>();
            if (cp_byte_data.Length == 0 || cp_byte_data.Length % 4 != 0)
            {
                return cp_data;
            }
            using (MemoryStream ms = new MemoryStream(cp_byte_data))
            {
                using (BinaryReader binaryReader = new BinaryReader(ms))
                {
                    int num = (int)binaryReader.BaseStream.Length / 4;
                    for (int i = 0; i < num; i++)
                    {
                        cp_data.Add(binaryReader.ReadUInt32());
                    }
                }
            }
            return cp_data;
        }

        private List<uint> GetDistances(List<uint> episode_cp_data, List<uint> theme_cp_data)
        {
            List<uint> distances = new List<uint>();

            int last_offset = (episode_cp_data.Count - theme_cp_data.Count) + 1;
            for (int offset = 0; offset < last_offset; offset++)
            {
                uint total_distance = 0;
                for (int x = 0; x < theme_cp_data.Count; x++)
                {
                    uint left = episode_cp_data[x + offset];
                    uint right = theme_cp_data[x];
                    uint this_score = GetHammingDist(left, right);
                    total_distance += this_score;
                }
                distances.Add(total_distance);
            }

            return distances;
        }

        private uint GetHammingDist(uint left, uint right)
        {
            // https://stackoverflow.com/questions/1024904/calculating-hamming-weight-efficiently-in-matlab
            // http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetNaive
            //w = bitand( bitshift(w, -1), uint32(1431655765)) + bitand(w, uint32(1431655765));
            //w = bitand(bitshift(w, -2), uint32(858993459)) + bitand(w, uint32(858993459));
            //w = bitand(bitshift(w, -4), uint32(252645135)) + bitand(w, uint32(252645135));
            //w = bitand(bitshift(w, -8), uint32(16711935)) + bitand(w, uint32(16711935));
            //w = bitand(bitshift(w, -16), uint32(65535)) + bitand(w, uint32(65535));

            uint distance = left ^ right;
            distance = ((distance >> 1) & 1431655765U) + (distance & 1431655765U);
            distance = ((distance >> 2) & 858993459U) + (distance & 858993459U);
            distance = ((distance >> 4) & 252645135U) + (distance & 252645135U);
            distance = ((distance >> 8) & 16711935U) + (distance & 16711935U);
            distance = ((distance >> 16) & 65535U) + (distance & 65535U);
            return distance;
        }

    }
}
