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
    // http://localhost:8096/emby/chapter_api/extract_theme
    [Route("/chapter_api/extract_theme", "GET", Summary = "Extract the Theme chromaprint")]
    //[Authenticated]
    public class ExtractTheme : IReturn<Object>
    {
        [ApiMember(Name = "id", Description = "item id", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int id { get; set; }
    }

    public class ExtractApiEndpoint : IService, IRequiresRequest
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

        public ExtractApiEndpoint(ILogManager logger,
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
            _logger = logger.GetLogger("ChapterApi - ExtractApiEndpoint");
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

            _logger.Info("ChapterApi - ExtractApiEndpoint Loaded");
        }

        public IRequest Request { get; set; }

        private string GetJsonString(Dictionary<string, object> data)
        {
            StringBuilder sb = new StringBuilder(4096);
            int i = 0;
            sb.Append("{\r\n");
            foreach (var x in data)
            {
                string value = "";
                Type value_type = x.Value.GetType();
                if (value_type == typeof(int) || value_type == typeof(long) || value_type == typeof(double))
                {
                    value = x.Value.ToString();
                }
                else
                {
                    value = "\"" + x.Value.ToString() + "\"";
                }

                sb.Append("\t\"" + x.Key + "\":" + value);
                if (i < data.Count - 1)
                {
                    sb.Append(",");
                }
                sb.Append("\r\n");
                i++;
            }
            sb.Append("}");

            return sb.ToString();
        }

        private object GetResponceFile(string filename, string data)
        {
            byte[] json_bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream(json_bytes);

            //MemoryStream ms = new MemoryStream();
            //_jsonSerializer.SerializeToStream(theme_data, ms);
            //ms.Position = 0;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers["Content-Disposition"] = "attachment; filename=\"" + filename + "\"";

            return _hrf.GetResult(Request, ms, "text/html; charset=UTF-8", headers);
        }

        public object Get(ExtractTheme request)
        {
            Dictionary<string, object> theme_data = new Dictionary<string, object>();
            object responce = null;

            Guid item_guid = _libraryManager.GetGuid(request.id);
            BaseItem item = _libraryManager.GetItemById(item_guid);

            if(item == null)
            {
                string message = "Item id not valid (" + request.id + ")";
                responce = GetResponceFile("error.txt", message);
                return responce;
            }

            Episode episode = item as Episode;

            if(episode == null)
            {
                string message = "Item id not valid episode (" + request.id + ")";
                responce = GetResponceFile("error.txt", message);
                return responce;
            }

            theme_data.Add("series", episode.SeriesName);
            int s_index = episode.ParentIndexNumber ?? -1;
            theme_data.Add("season", s_index);
            //int e_index = episode.IndexNumber ?? -1;
            //theme_data.Add("episode", e_index);

            List<string> wanted_prividers = new List<string> { "tvdb", "imdb" , "tmdb" };
            bool provider_found = false;
            foreach (var provider in episode.Series.ProviderIds)
            {
                if(wanted_prividers.Contains(provider.Key.ToLower()))
                {
                    theme_data.Add(provider.Key.ToLower(), provider.Value);
                    provider_found = true;
                }
            }

            if(provider_found == false)
            {
                string message = "Series has no provider IDs (" + request.id + ")";
                responce = GetResponceFile("error.txt", message);
                return responce;
            }

            // get start and end times for intro extraction

            List<ChapterInfo> chapters = _ir.GetChapters(episode);
            long? intro_start = null;
            long? intro_end = null;
            foreach (ChapterInfo ci in chapters)
            {
                if (intro_start == null && ci.MarkerType == MarkerType.IntroStart)
                {
                    intro_start = ci.StartPositionTicks;
                }
                else if (intro_end == null && ci.MarkerType == MarkerType.IntroEnd)
                {
                    intro_end = ci.StartPositionTicks;
                }
            }

            if(intro_start == null || intro_end == null)
            {
                string message = "Episode has no IntroStart or IntroEnd chapter markers (" + request.id + ")";
                responce = GetResponceFile("error.txt", message);
                return responce;
            }

            long intro_duration = (intro_end.Value - intro_start.Value) / 10000000;
            if (intro_duration < 5 || intro_duration > 300)
            {
                string message = "Episode Intro duration is not valid " + intro_duration + " (" + request.id + ")";
                responce = GetResponceFile("error.txt", message);
                return responce;
            }

            TimeSpan intro_start_time = new TimeSpan(intro_start.Value);
            TimeSpan intro_end_time = new TimeSpan(intro_end.Value);

            double duration = Math.Round((intro_end_time - intro_start_time).TotalSeconds, 3);
            theme_data.Add("duration", duration);

            int intro_extract_span = (int)((intro_end_time.TotalSeconds * 1.33) + 0.5);
            theme_data.Add("extract", intro_extract_span);

            int result = ExtractChromaprint(theme_data, intro_start_time, intro_end_time, item.Path);

            if (result != 0)
            {
                string message = "Error extracting chromaprint data " + result + " (" + request.id + ")";
                responce = GetResponceFile("error.txt", message);
                return responce;
            }

            // build filename
            string filename = episode.SeriesName;
            filename = filename.ToLower();
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid)
            {
                filename = filename.Replace(c.ToString(), "_");
            }
            filename += "-";
            filename += "s" + (episode.ParentIndexNumber ?? 0).ToString("D2");
            //filename += "e" + (episode.IndexNumber ?? 0).ToString("D2");
            filename += ".json";

            string json_string = GetJsonString(theme_data);
            responce = GetResponceFile(filename, json_string);
            return responce;
        }

        private int ExtractChromaprint(
            Dictionary<string, object> theme_data,
            TimeSpan ts_start,
            TimeSpan ts_end,
            string media_path
            )
        {
            //TimeSpan ts_start = TimeSpan.FromSeconds(20);
            //TimeSpan ts_end = TimeSpan.FromSeconds(90);
            TimeSpan ts_duration = ts_end - ts_start;

            string ffmpeg_path = _ffmpeg.FfmpegConfiguration.EncoderPath;
            //string ffmpeg_path = _config.CommonApplicationPaths.ProgramSystemPath;
            //ffmpeg_path = Path.Combine(ffmpeg_path, "ffmpeg");

            List<string> command_params = new List<string>();

            command_params.Add("-accurate_seek");
            command_params.Add(string.Format("-ss {0}", ts_start));
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
                //process.WaitForExit(1000 * 60 * 3);

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

            if (return_code != 0)
            {
                return return_code;
            }

            if(chroma_bytes.Length == 0)
            {
                return -100;
            }

            string theme_cp_data_md5 = null;
            // calc md5
            using (MD5 md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(chroma_bytes);
                //theme.theme_cp_data_md5 = Convert.ToHexString(hashBytes);
                theme_cp_data_md5 = BitConverter.ToString(hashBytes).Replace("-", "").ToUpper();
            }

            string cp_data = Convert.ToBase64String(chroma_bytes);

            theme_data.Add("cp_data", cp_data);
            theme_data.Add("cp_data_md5", theme_cp_data_md5);

            return return_code;
        }

    }
}
