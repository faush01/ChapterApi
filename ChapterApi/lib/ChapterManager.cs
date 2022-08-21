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

using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChapterApi.lib
{
    public class ChapterManager
    {
        private IItemRepository _ir;

        public ChapterManager(IItemRepository ir)
        {
            _ir = ir;
        }   

        public void InsertChapters(DetectionJobItem job_item)
        {
            if (job_item.detection_result == null || job_item.detection_result.found_intro == false)
            {
                return;
            }

            // get chapters
            List<ChapterInfo> chapters = _ir.GetChapters(job_item.item);

            List<ChapterInfo> new_chapters = new List<ChapterInfo>();
            // first remove the existing Intro chapters
            foreach (ChapterInfo ci in chapters)
            {
                if (ci.MarkerType != MarkerType.IntroStart && ci.MarkerType != MarkerType.IntroEnd)
                {
                    new_chapters.Add(ci);
                }
            }

            // add new chapters
            ChapterInfo intro_start = new ChapterInfo();
            intro_start.MarkerType = MarkerType.IntroStart;
            intro_start.Name = "IntroStart";
            intro_start.StartPositionTicks = job_item.detection_result.start_time_ticks;
            new_chapters.Add(intro_start);

            ChapterInfo intro_end = new ChapterInfo();
            intro_end.MarkerType = MarkerType.IntroEnd;
            intro_end.Name = "IntroEnd";
            intro_end.StartPositionTicks = job_item.detection_result.end_time_ticks;
            new_chapters.Add(intro_end);

            // sort chapters
            new_chapters.Sort(delegate (ChapterInfo c1, ChapterInfo c2)
            {
                return c1.StartPositionTicks.CompareTo(c2.StartPositionTicks);
            });

            _ir.SaveChapters(job_item.item.InternalId, new_chapters);
        }
    }
}
