using System;
using System.Collections.Generic;
using System.Text;

namespace ChapterApi.options
{
    public class ChapterApiOptions
    {
        public int KeepFinishdJobFor { get; set; } = 24;
        public string IntroDataPath { set; get; }
    }
}
