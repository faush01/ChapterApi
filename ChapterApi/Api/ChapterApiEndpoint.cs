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
using System.Text;

namespace ChapterApi.Api
{
    // http://localhost:8096/emby/chapter_api/get_items
    [Route("/chapter_api/get_items", "GET", Summary = "Get a list of items for type and filtered")]
    [Authenticated]
    public class GetItems : IReturn<Object>
    {
        [ApiMember(Name = "filter", Description = "filter string", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string filter { get; set; }
        [ApiMember(Name = "item_type", Description = "type of items to return", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string item_type { get; set; }
        [ApiMember(Name = "parent", Description = "parent id", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int parent { get; set; }
    }

    // http://localhost:8096/emby/chapter_api/get_chapters
    [Route("/chapter_api/get_chapters", "GET", Summary = "Get a list of items for type and filtered")]
    [Authenticated]
    public class GetItemChapters : IReturn<Object>
    {
        [ApiMember(Name = "id", Description = "item id", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int id { get; set; }
    }

    // http://localhost:8096/emby/chapter_api/get_item_path
    [Route("/chapter_api/get_item_path", "GET", Summary = "Get a list of items for type and filtered")]
    [Authenticated]
    public class GetItemPath : IReturn<Object>
    {
        [ApiMember(Name = "id", Description = "item id", IsRequired = true, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int id { get; set; }
    }

    // http://localhost:8096/emby/chapter_api/update_chapters
    [Route("/chapter_api/update_chapters", "GET", Summary = "Updates chapters")]
    [Authenticated]
    public class UpdateChapters : IReturn<Object>
    {
        [ApiMember(Name = "id", Description = "item id", IsRequired = false, DataType = "long", ParameterType = "query", Verb = "GET")]
        public long id { get; set; } = -1;
        [ApiMember(Name = "index_list", Description = "list if indexes", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string index_list { set; get; }
        [ApiMember(Name = "action", Description = "action to take", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string action { get; set; }
        [ApiMember(Name = "name", Description = "chapter name", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string name { get; set; }
        [ApiMember(Name = "type", Description = "chapter type", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string type { get; set; }
        [ApiMember(Name = "time", Description = "time string of start time hh:mm:ss", IsRequired = false, DataType = "string", ParameterType = "query", Verb = "GET")]
        public string time { get; set; }
        [ApiMember(Name = "auto_interval", Description = "auto create interval", IsRequired = false, DataType = "int", ParameterType = "query", Verb = "GET")]
        public int auto_interval { set; get; }
    }

    public class ChapterApiEndpoint : IService, IRequiresRequest
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

        public ChapterApiEndpoint(ILogManager logger,
            IFileSystem fileSystem,
            IServerConfigurationManager config,
            IJsonSerializer jsonSerializer,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            IAuthorizationContext authContext,
            IUserDataManager userDataManager,
            IItemRepository itemRepository)
        {
            _logger = logger.GetLogger("ChapterApi - ChapterApiEndpoint");
            _jsonSerializer = jsonSerializer;
            _fileSystem = fileSystem;
            _config = config;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _ac = authContext;
            _userDataManager = userDataManager;
            _ir = itemRepository;

            _logger.Info("ChapterApi - ChapterApiEndpoint Loaded");
        }

        public IRequest Request { get; set; }

        public object Get(GetItemPath request)
        {
            _logger.Info("GetItemPath");

            List<Dictionary<string, object>> item_path = new List<Dictionary<string, object>>();
            //List<PathItem> item_path = new List<PathItem>();

            Guid item_guid = _libraryManager.GetGuid(request.id);
            BaseItem item = _libraryManager.GetItemById(item_guid);

            Folder[] collections = _libraryManager.GetCollectionFolders(item);
            foreach (var collection in collections)
            {
                _logger.Info("GetCollectionFolders: " + collection.Name + "(" + item.InternalId + "," + item.IsTopParent + ")");
            }
            //BaseItem base_item = item.GetTopParent();

            bool hadTopParent = false;

            while (item != null && !item.IsTopParent)
            {
                _logger.Info("AddingPathItem: " + item.Name + "(" + item.InternalId + "," + item.IsTopParent + "," + item.IsResolvedToFolder + ")");

                if (item.GetType() != typeof(Folder))
                {
                    Dictionary<string, object> pi = new Dictionary<string, object>();
                    pi.Add("Name", item.Name);
                    pi.Add("Id", item.InternalId);
                    pi.Add("ItemType", item.GetType().Name);
                    item_path.Insert(0, pi);
                }

                item = item.GetParent();

                if (item != null && item.IsTopParent)
                {
                    hadTopParent = true;
                    _logger.Info("TopParentItem: " + item.Name + "(" + item.InternalId + "," + item.IsTopParent + "," + item.IsResolvedToFolder + ")");
                }
            }

            if (hadTopParent && collections.Length > 0)
            {
                item = collections[0];

                _logger.Info("AddingCollectionItem:" + item.Name + "(" + item.InternalId + "," + item.IsTopParent + "," + item.IsResolvedToFolder + ")");

                Dictionary<string, object> pi = new Dictionary<string, object>();
                pi.Add("Name", item.Name);
                pi.Add("Id", item.InternalId);
                pi.Add("ItemType", item.GetType().Name);
                item_path.Insert(0, pi);

                item = item.GetParent();

                pi = new Dictionary<string, object>();
                pi.Add("Name", item.Name);
                pi.Add("Id", item.InternalId);
                pi.Add("ItemType", item.GetType().Name);
                item_path.Insert(0, pi);
            }

            return item_path;
        }

        public object Get(GetItems request)
        {
            List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();

            /*
            types
                AggregateFolder
                UserRootFolder
                Folder
                CollectionFolder
                Movie
                Series
                Season
                Episode
                Audio
                MusicAlbum
                MusicArtist
                MusicGenre
                Playlist
                Video
                Genre
                Person
                Studio
                UserView
            */

            InternalItemsQuery query = new InternalItemsQuery();
            query.IsVirtualItem = false;

            //(string, SortOrder)[] ord = new (string, SortOrder)[1];
            //ord[0] = ("name", SortOrder.Ascending);
            //query.OrderBy = ord;

            if (request.parent != 0)
            {
                Guid item_guid = _libraryManager.GetGuid(request.parent);
                BaseItem item = _libraryManager.GetItemById(item_guid);

                if(item != null)
                {
                    _logger.Info(item.Name + " : Item Type(" + item.GetType().Name + ")");

                    if (item.GetType() == typeof(UserRootFolder))
                    {
                        query.IncludeItemTypes = new string[] { "CollectionFolder" };
                    }
                    else if (item.GetType() == typeof(CollectionFolder))
                    {
                        query.IncludeItemTypes = new string[] { "MusicAlbum", "Movie", "Series" };
                        query.Recursive = true;
                    }
                    else if (item.GetType() == typeof(Series))
                    {
                        query.IncludeItemTypes = new string[] { "Season" };
                        query.Recursive = true;
                    }
                    else if (item.GetType() == typeof(Season))
                    {
                        query.IncludeItemTypes = new string[] { "Episode" };
                        query.Recursive = true;
                    }
                }

                query.ParentIds = new long[] { request.parent };
            }
            else if (!string.IsNullOrEmpty(request.filter))
            {
                query.IncludeItemTypes = new string[] { "MusicAlbum", "Movie", "Series" };
                query.SearchTerm = request.filter;
            }
            else
            {
                query.IncludeItemTypes = new string[] { "CollectionFolder" };
                //query.Parent = _libraryManager.RootFolder;
            }

            BaseItem[] results = _libraryManager.GetItemList(query);

            foreach (BaseItem item in results)
            {
                //_logger.Info(item.Name + "(" + item.InternalId + ")");
                Dictionary<string, object> info = new Dictionary<string, object>();
                info.Add("Id", item.InternalId);
                info.Add("Name", item.Name);
                info.Add("SortName", item.SortName);
                info.Add("ItemType", item.GetType().Name);

                if (item.GetType() == typeof(Episode))
                {
                    Episode e = (Episode)item;
                    info.Add("Series", e.SeriesName);
                    info.Add("Season", e.Season.Name);

                    string index_num = "";
                    if (e.IndexNumber != null)
                    {
                        index_num = e.IndexNumber.Value.ToString("D2");
                    }
                    else
                    {
                        index_num = "00";
                    }
                    info["Name"] = index_num + " - " + item.Name;
                    info["SortName"] = index_num + " - " + item.SortName;
                }
                else if (item.GetType() == typeof(Season))
                {
                    string index_num = "";
                    if (item.IndexNumber != null)
                    {
                        index_num = item.IndexNumber.Value.ToString("D2");
                    }
                    else
                    {
                        index_num = "00";
                    }
                    info["Name"] = index_num + " - " + item.Name;
                    info["SortName"] = index_num + " - " + item.SortName;   
                }

                items.Add(info);
            }

            items.Sort(delegate (Dictionary<string, object> c1, Dictionary<string, object> c2) 
                {
                    string c1_str = c1["SortName"] as string;
                    string c2_str = c2["SortName"] as string;
                    int cmp_restlt = string.Compare(c1_str, c2_str, comparisonType: StringComparison.OrdinalIgnoreCase);
                    return cmp_restlt; 
                });

            return items;
        }

        private List<Dictionary<string, object>> GetItemChapters(BaseItem item)
        {
            List<ChapterInfo> chapters = _ir.GetChapters(item);
            List<Dictionary<string, object>> chapter_list = new List<Dictionary<string, object>>();
            int chap_index = 0;
            foreach (ChapterInfo ci in chapters)
            {
                Dictionary<string, object> chap_info = new Dictionary<string, object>();
                chap_info.Add("Name", ci.Name);
                chap_info.Add("MarkerType", ci.MarkerType);
                chap_info.Add("StartPositionTicks", ci.StartPositionTicks);

                TimeSpan ct = new TimeSpan(ci.StartPositionTicks);
                chap_info.Add("StartTime", ct.ToString(@"hh\:mm\:ss\.fff"));

                chap_info.Add("Index", chap_index);

                chapter_list.Add(chap_info);
                chap_index++;
            }
            return chapter_list;
        }

        private List<Dictionary<string, object>> GetEpisodeIntros(BaseItem season_item)
        {
            List<Dictionary<string, object>> episode_list = new List<Dictionary<string, object>>();

            InternalItemsQuery query = new InternalItemsQuery();
            query.ParentIds = new long[] { season_item.InternalId };
            query.IncludeItemTypes = new string[] { "Episode" };
            BaseItem[] results = _libraryManager.GetItemList(query);

            foreach (BaseItem episode in results)
            {
                //_logger.Info(item.Name + "(" + item.InternalId + ")");
                Dictionary<string, object> info = new Dictionary<string, object>();
                info.Add("Id", episode.InternalId);
                info.Add("ItemType", episode.GetType().Name);

                string ep_no = (episode.IndexNumber ?? 0).ToString("D2");
                info.Add("Name", ep_no + " - " + episode.Name);

                TimeSpan? intro_start = null;
                int intro_start_index = -1;
                string intro_start_image_tag = null;

                TimeSpan? intro_end = null;
                int intro_end_index = -1;
                string intro_end_image_tag = null;

                TimeSpan? credit_start = null;
                int credit_start_index = -1;
                string credit_start_image_tag = null;

                List<ChapterInfo> chapters = _ir.GetChapters(episode);
                foreach(ChapterInfo ci in chapters)
                {
                    if(ci.MarkerType == MarkerType.IntroStart && intro_start == null)
                    {
                        intro_start_index = ci.ChapterIndex;
                        intro_start_image_tag = ci.ImageTag;
                        intro_start = new TimeSpan(ci.StartPositionTicks);
                    }
                    else if (ci.MarkerType == MarkerType.IntroEnd && intro_end == null)
                    {
                        intro_end_index = ci.ChapterIndex;
                        intro_end_image_tag = ci.ImageTag;
                        intro_end = new TimeSpan(ci.StartPositionTicks);
                    }
                    else if(ci.MarkerType == MarkerType.CreditsStart && credit_start == null)
                    {
                        credit_start_index = ci.ChapterIndex;
                        credit_start_image_tag = ci.ImageTag;
                        credit_start = new TimeSpan(ci.StartPositionTicks);
                    }
                }

                // add chapter image info
                info.Add("IntroStartIndex", intro_start_index);
                info.Add("IntroStartImageTag", intro_start_image_tag);
                info.Add("IntroEndIndex", intro_end_index);
                info.Add("IntroEndImageTag", intro_end_image_tag);
                info.Add("CreditsIndex", intro_end_index);
                info.Add("CreditsImageTag", intro_end_image_tag);

                // add intro start
                if (intro_start != null)
                {
                    info.Add("IntroStart", intro_start.Value.ToString(@"hh\:mm\:ss\.fff"));
                }
                else
                {
                    info.Add("IntroStart", "--:--:--.---");
                }

                // add intro end
                if (intro_end != null)
                {
                    info.Add("IntroEnd", intro_end.Value.ToString(@"hh\:mm\:ss\.fff"));
                }
                else
                {
                    info.Add("IntroEnd", "--:--:--.---");
                }

                // add intro duration
                if (intro_start != null && intro_end != null)
                {
                    TimeSpan duration = intro_end.Value - intro_start.Value;
                    info.Add("IntroSpan", duration.ToString(@"hh\:mm\:ss\.fff"));
                }
                else
                {
                    info.Add("IntroSpan", "--:--:--.---");
                }

                // add creits start
                if (credit_start != null)
                {
                    info.Add("CreditsStart", credit_start.Value.ToString(@"hh\:mm\:ss\.fff"));
                }
                else
                {
                    info.Add("CreditsStart", "--:--:--.---");
                }

                episode_list.Add(info);
            }

            episode_list.Sort(delegate (Dictionary<string, object> c1, Dictionary<string, object> c2)
            {
                string c1_str = c1["Name"] as string;
                string c2_str = c2["Name"] as string;
                int cmp_restlt = string.Compare(c1_str, c2_str, comparisonType: StringComparison.OrdinalIgnoreCase);
                return cmp_restlt;
            });

            return episode_list;
        }

        public object Get(GetItemChapters request)
        {
            Guid item_guid = _libraryManager.GetGuid(request.id);
            BaseItem item = _libraryManager.GetItemById(item_guid);

            Dictionary<string, object> responce_data = new Dictionary<string, object>();

            Dictionary<string, object> item_info = new Dictionary<string, object>();
            item_info.Add("Id", item.InternalId);
            item_info.Add("Name", item.Name);
            item_info.Add("ItemType", item.GetType().Name);

            if (item.GetType() == typeof(Episode))
            {
                string index_num = "";
                if (item.IndexNumber != null)
                {
                    index_num = item.IndexNumber.Value.ToString("D2");
                }
                else
                {
                    index_num = "00";
                }
                item_info["Name"] = index_num + " - " + item.Name;
            }
            else if (item.GetType() == typeof(Season))
            {
                string index_num = "";
                if (item.IndexNumber != null)
                {
                    index_num = item.IndexNumber.Value.ToString("D2");
                }
                else
                {
                    index_num = "00";
                }
                item_info["Name"] = index_num + " - " + item.Name;
            }

            responce_data.Add("item_info", item_info);

            if (item.GetType() == typeof(Movie) || item.GetType() == typeof(Episode))
            {
                List<Dictionary<string, object>> chapter_list = GetItemChapters(item);
                responce_data.Add("chapters", chapter_list);
            }
            else if (item.GetType() == typeof(Season))
            {
                List<Dictionary<string, object>> episode_list = GetEpisodeIntros(item);
                responce_data.Add("episodes", episode_list);
            }

            return responce_data;
        }

        public object Get(UpdateChapters request)
        {
            List<string> actions = new List<string>();

            Guid item_guid = _libraryManager.GetGuid(request.id);
            BaseItem item = _libraryManager.GetItemById(item_guid);

            List<ChapterInfo> chapters = _ir.GetChapters(item);

            foreach(var chapter in chapters)
            {
                _logger.Info("ChapterApiEndpoint - Chapter - " + chapter.Name + " " + chapter.MarkerType + " " + chapter.StartPositionTicks);
            }

            if (!string.IsNullOrEmpty(request.action) && request.action == "remove")
            {
                actions.Add("Removing Chapters");

                if(!string.IsNullOrEmpty(request.index_list))
                {
                    string[] indexes = request.index_list.Split(new string[] {","}, StringSplitOptions.RemoveEmptyEntries);
                    List<int> index_list = new List<int>();
                    foreach (string i in indexes)
                    {
                        index_list.Add(int.Parse(i));
                    }
                    index_list.Sort();
                    Console.WriteLine(string.Join("|", index_list));
                    for (int x = index_list.Count - 1; x >= 0; x--)
                    {
                        Console.WriteLine("Removing Item : " + x + " " + index_list[x]);
                        if (index_list[x] < chapters.Count)
                        {
                            chapters.RemoveAt(index_list[x]);
                            actions.Add("Chapter with index " + index_list[x] + " was removed");
                        }
                    }
                }
            }
            else if (!string.IsNullOrEmpty(request.action) && request.action == "auto")
            {
                actions.Add("Auto Adding Chapters at intervals: " + request.auto_interval);

                TimeSpan interval = new TimeSpan(0, request.auto_interval, 0);
                TimeSpan auto_chapter = new TimeSpan(interval.Ticks);
                int chapter_index = 1;
                string chapter_name = request.name;
                while(auto_chapter.Ticks < item.RunTimeTicks)
                {
                    ChapterInfo ci = new ChapterInfo();
                    ci.Name = chapter_name + " " + chapter_index;
                    ci.MarkerType = MarkerType.Chapter;
                    ci.StartPositionTicks = auto_chapter.Ticks;
                    chapters.Add(ci);

                    auto_chapter += interval;
                    chapter_index++;
                }
            }
            else if (!string.IsNullOrEmpty(request.action) && request.action == "add")
            {
                actions.Add("Adding Chapter");

                TimeSpan ct;
                bool sp_ok = TimeSpan.TryParse(request.time, out ct);
                if(!sp_ok)
                {
                    actions.Add("Chapter time not valid : " + request.time);
                }

                MarkerType mType = MarkerType.Chapter;
                bool m_ok = false;
                if(request.type == "chapter")
                {
                    mType = MarkerType.Chapter;
                    m_ok = true;
                }
                else if(request.type == "intro_start")
                {
                    mType = MarkerType.IntroStart;
                    m_ok = true;
                }
                else if (request.type == "intro_end")
                {
                    mType = MarkerType.IntroEnd;
                    m_ok = true;
                }
                else if (request.type == "credits_start")
                {
                    mType = MarkerType.CreditsStart;
                    m_ok = true;
                }
                if (!m_ok)
                {
                    actions.Add("Chapter Marker Type not valid : " + request.type);
                }

                if (m_ok && sp_ok)
                {
                    string chap_name = request.name;
                    if(string.IsNullOrEmpty(chap_name))
                    {
                        chap_name = mType.ToString();
                    }

                    ChapterInfo ci = new ChapterInfo();
                    ci.Name = chap_name;
                    ci.MarkerType = mType;
                    ci.StartPositionTicks = ct.Ticks;
                    chapters.Add(ci);
                    actions.Add("Chapter Added : " + ci.Name + " | " + ci.MarkerType + " | " + ci.StartPositionTicks);
                }
            }

            // sort chapters by start time
            chapters.Sort(delegate (ChapterInfo c1, ChapterInfo c2)
            {
                return c1.StartPositionTicks.CompareTo(c2.StartPositionTicks);
            });

            _ir.SaveChapters(request.id, chapters);
            _logger.Info("ChapterApiEndpoint - Chapters Saved");

            return actions;
        }
    }
}
