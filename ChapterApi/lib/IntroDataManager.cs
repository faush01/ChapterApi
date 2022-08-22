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

        public IntroDataManager(ILogger logger, IJsonSerializer jsonSerializer)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
        }

        public Dictionary<string, List<IntroInfo>> LoadIntroDataFromPath(DirectoryInfo data_path)
        {
            List<FileInfo> fil = new List<FileInfo>();
            WalkDir(data_path, fil);

            // process the list of files
            List<IntroInfo> intro_data = new List<IntroInfo>();
            foreach (FileInfo fi in fil)
            {
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
                    _logger.Info("Processing Intro File : " + fi.FullName);
                }
            }

            foreach (DirectoryInfo idi in di.GetDirectories())
            {
                WalkDir(idi, fil);
            }
        }

        private IntroInfo LoadFromJsonFile(FileInfo fi)
        {
            string file_data = File.ReadAllText(fi.FullName, Encoding.UTF8);
            IntroInfo info = _jsonSerializer.DeserializeFromString(file_data, typeof(IntroInfo)) as IntroInfo;
            return info;
        }

        private List<IntroInfo> LoadFromZipFile(FileInfo fi)
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
                                _logger.Info("Adding info from zip : " + entry.Name + " - " + info.cp_data_md5);
                                loaded_info_items.Add(info);
                            }
                        }
                    }
                }
            }
            return loaded_info_items;
        }

        private List<IntroInfo> LoadIntroFileData(FileInfo intro_file)
        {
            List<IntroInfo> intro_list = new List<IntroInfo>();

            string file_name = intro_file.Name.ToLower();
            if (file_name.EndsWith(".json"))
            {
                IntroInfo intro = LoadFromJsonFile(intro_file);
                if (intro != null)
                {
                    intro_list.Add(intro);
                }
            }
            else if (file_name.EndsWith(".zip"))
            {
                List<IntroInfo> intros = LoadFromZipFile(intro_file);
                if (intros.Count > 0)
                {
                    intro_list.AddRange(intros);
                }
            }

            return intro_list;
        }

        Dictionary<string, List<IntroInfo>> BuildIntroDB(List<IntroInfo> items)
        {
            // build the intro DB to use
            Dictionary<string, List<IntroInfo>> intro_db = new Dictionary<string, List<IntroInfo>>();
            foreach (IntroInfo intro in items)
            {
                string imdb = intro.imdb ?? "";
                imdb = imdb.Trim();
                if (!string.IsNullOrEmpty(imdb))
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
                            _logger.Info("Dropping duplicate item");
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
