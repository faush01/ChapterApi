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

using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ChapterApi.lib
{
    public class IntroDataManager
    {
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILibraryManager _libraryManager;

        public IntroDataManager(
            ILogger logger, 
            IJsonSerializer jsonSerializer,
            ILibraryManager libraryManager)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
            _libraryManager = libraryManager;
        }

        public void LookupInternalIntroDB(BaseItem base_item, List<IntroInfo> intro_cp_info_items, JobManager jm)
        {
            if (base_item != null)
            {
                string imdb_name = MetadataProviders.Imdb.ToString();
                string imdb_id = "";
                if (base_item.GetType() == typeof(Episode))
                {
                    Episode episode = base_item as Episode;
                    if (episode.Series.ProviderIds.ContainsKey(imdb_name))
                    {
                        imdb_id = episode.Series.ProviderIds[imdb_name];
                    }
                }
                else if (base_item.GetType() == typeof(Season))
                {
                    Season season = base_item as Season;
                    if (season.Series.ProviderIds.ContainsKey(imdb_name))
                    {
                        imdb_id = season.Series.ProviderIds[imdb_name];
                    }
                }
                else if (base_item.GetType() == typeof(Series))
                {
                    Series series = base_item as Series;
                    if (series.ProviderIds.ContainsKey(imdb_name))
                    {
                        imdb_id = series.ProviderIds[imdb_name];
                    }
                }

                if (!string.IsNullOrEmpty(imdb_id))
                {
                    imdb_id = imdb_id.ToLower().Trim();
                    Dictionary<string, List<IntroInfo>> intro_data = jm.GetIntroData();
                    if (intro_data.ContainsKey(imdb_id))
                    {
                        List<IntroInfo> intros = intro_data[imdb_id];
                        if (intros.Count > 0)
                        {
                            intro_cp_info_items.AddRange(intros);
                        }
                    }
                }
            }
        }

        private HashSet<string> GetSeriesProviderIDs()
        {
            HashSet<string> ids = new HashSet<string>();

            InternalItemsQuery query = new InternalItemsQuery();
            query.IncludeItemTypes = new string[] { "Series" };
            query.Recursive = true;

            string imdb_name = MetadataProviders.Imdb.ToString();

            BaseItem[] results = _libraryManager.GetItemList(query);
            foreach (BaseItem base_item in results)
            {
                Series series = base_item as Series;
                if (series.ProviderIds.ContainsKey(imdb_name))
                {
                    string imdb_id = series.ProviderIds[imdb_name];
                    if(!string.IsNullOrEmpty(imdb_id))
                    {
                        imdb_id = imdb_id.ToLower();
                        ids.Add(imdb_id);
                    }
                }
            }

            return ids;
        }

        public Dictionary<string, List<IntroInfo>> LoadIntroDataFromPath(DirectoryInfo data_path)
        {
            List<FileInfo> file_list = new List<FileInfo>();
            try
            {
                WalkDir(data_path, file_list);
            }
            catch(Exception e)
            {
                _logger.Error("Failed to load IntroInfo files from : " + data_path.FullName);
            }

            // process the list of files
            List<IntroInfo> intro_data = new List<IntroInfo>();
            foreach (FileInfo fi in file_list)
            {
                _logger.Info("Loading data from IntroFile : " + fi.FullName);
                List<IntroInfo> loaded_intro_list = LoadIntroFileData(fi);
                if (loaded_intro_list.Count > 0)
                {
                    intro_data.AddRange(loaded_intro_list);
                }
            }
            
            Dictionary<string, List<IntroInfo>> intro_db = BuildIntroDB(intro_data);
            return intro_db;
        }

        private void WalkDir(DirectoryInfo di, List<FileInfo> fil)
        {
            foreach (FileInfo fi in di.GetFiles())
            {
                string name = fi.Name.ToLower();
                if (name.EndsWith(".zip") || name.EndsWith(".json"))
                {
                    fil.Add(fi);
                    _logger.Debug("Found Intro File : " + fi.FullName);
                }
            }

            foreach (DirectoryInfo idi in di.GetDirectories())
            {
                WalkDir(idi, fil);
            }
        }

        private void LoadFromJsonFile(FileInfo fi, List<IntroInfo> intro_items)
        {
            string file_data = File.ReadAllText(fi.FullName, Encoding.UTF8);
            IntroInfo info = _jsonSerializer.DeserializeFromString(file_data, typeof(IntroInfo)) as IntroInfo;
            if(info != null)
            {
                _logger.Debug("Adding info from json : " + info.series + " - " + info.imdb + " - " + info.cp_data_md5);
                intro_items.Add(info);
            }
        }

        private void LoadFromZipFile(FileInfo fi, List<IntroInfo> intro_items)
        {
            List<IntroInfo> loaded_info_items = new List<IntroInfo>();
            using (ZipArchive archive = ZipFile.Open(fi.FullName, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    //_logger.Info("ArchiveEntry: " + entry.Name);
                    if (!string.IsNullOrEmpty(entry.Name) && entry.Name.ToLower().EndsWith(".json"))
                    {
                        using (StreamReader st = new StreamReader(entry.Open()))
                        {
                            string entry_data = st.ReadToEnd();
                            //_logger.Info("Entry Data : " + entry_data);
                            IntroInfo info = _jsonSerializer.DeserializeFromString(entry_data, typeof(IntroInfo)) as IntroInfo;
                            if (info != null)
                            {
                                _logger.Debug("Adding info from zip : " + info.series + " - " + info.imdb + " - " + info.cp_data_md5);
                                loaded_info_items.Add(info);
                            }
                        }
                    }
                }
            }
            if(loaded_info_items.Count > 0)
            {
                intro_items.AddRange(loaded_info_items);
            }
        }

        private List<IntroInfo> LoadIntroFileData(FileInfo intro_file)
        {
            List<IntroInfo> intro_list = new List<IntroInfo>();

            string file_name = intro_file.Name.ToLower();
            if (file_name.EndsWith(".json"))
            {
                LoadFromJsonFile(intro_file, intro_list);
            }
            else if (file_name.EndsWith(".zip"))
            {
                try
                {
                    LoadFromZipFile(intro_file, intro_list);
                }
                catch (Exception e)
                {
                    _logger.Error("Error loading IntoData from ZIP (" + intro_file.FullName + ") - " + e.Message);
                }
            }

            return intro_list;
        }

        Dictionary<string, List<IntroInfo>> BuildIntroDB(List<IntroInfo> items)
        {
            HashSet<string> provider_ids = GetSeriesProviderIDs();
            foreach(string provider_id in provider_ids)
            {
                _logger.Info("Provide ID Loaded : " + provider_id);
            }

            // build the intro DB to use
            Dictionary<string, List<IntroInfo>> intro_db = new Dictionary<string, List<IntroInfo>>();
            foreach (IntroInfo intro in items)
            {
                string imdb = intro.imdb ?? "";
                imdb = imdb.Trim();
                imdb = imdb.ToLower();
                if (!string.IsNullOrEmpty(imdb) && provider_ids.Contains(imdb))
                {
                    if (intro_db.ContainsKey(imdb))
                    {
                        // check if exists based on md5
                        bool found = false;
                        foreach (IntroInfo info in intro_db[imdb])
                        {
                            if (!string.IsNullOrEmpty(intro.cp_data_md5) && intro.cp_data_md5.Equals(info.cp_data_md5, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            intro_db[imdb].Add(intro);
                        }
                        else
                        {
                            _logger.Info("Dropping duplicate item : " + intro.cp_data_md5);
                        }
                    }
                    else
                    {
                        intro_db[imdb] = new List<IntroInfo>() { intro };
                    }
                }
            }
            return intro_db;
        }

    }
}
