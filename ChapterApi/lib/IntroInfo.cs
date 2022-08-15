using System;
using System.Collections.Generic;
using System.Text;

namespace ChapterApi
{
    public class IntroInfo
    {
        public string series { get; set; }
        public int? season { get; set; }
        public int? episode { get; set; }
        public string tvdb { get; set; }
        public string imdb { get; set; }
        public string tmdb { get; set; }
        public int extract { get; set; }
        public string cp_data { get; set; }
        public string cp_data_md5 { get; set; }
    }
}
