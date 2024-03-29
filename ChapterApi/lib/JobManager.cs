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

using ChapterApi.lib;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ChapterApi
{
    public enum JobStatus : int
    {
        Waiting = -1,
        Canceled = 0,
        Running = 1,
        Complete = 2,
        Error = 3
    }

    public enum JobItemStatus : int
    {
        Waiting = -1,
        Running = 0,
        Complete = 1
    }

    public class DetectionJob
    {
        public DateTime added { get; } = DateTime.Now;
        public DateTime? finished { get; set; }
        public int keep_finished_for { set; get; } = 24;
        public JobStatus status { get; set; } = JobStatus.Waiting;
        public string name { get; set; }
        public List<IntroInfo> intro_info_list { get; set; } = new List<IntroInfo>();
        public List<DetectionJobItem> items { get; } = new List<DetectionJobItem>();
        public string ffmpeg_path { get; set; }
        public string message { get; set; }
        public bool auto_insert { get; set; } = false;
        public double threshold { set; get; } = 0.5;
    }

    public class DetectionJobItem
    {
        public BaseItem item { get; set; }
        public string name { set; get; }
        public JobItemStatus status { get; set; } = JobItemStatus.Waiting;
        public List<DetectionResult> detection_result_list { get; set; } = new List<DetectionResult>();
        public DetectionResult detection_result { get; set; }
        public string job_duration { set; get; }
        public double job_total_time { set; get; }
        public double job_extract_time {  set; get; }
        public double job_detect_time { set; get; }
    }

    public class DetectionResult
    {
        public IntroInfo intro_info { get; set; }
        public bool found_intro { get; set; } = false;
        public string start_time { set; get; }
        public long start_time_ticks { set; get; }
        public string end_time { set; get; }
        public long end_time_ticks { set; get; }
        public string duration_time { set; get; }
        public long duration_time_ticks { set; get; }
        public uint sum_distance { set; get; }
        public uint min_distance { set; get; }
        public uint max_distance { set; get; }
        public double avg_distance { set; get; }
        public uint dist_threshold { set; get; }
        public int? min_offset { set; get; }
        public bool min_dist_found { set; get; } = false;
        public List<uint> distances { set; get; }
    }

    public class JobManager
    {
        private static readonly object padlock = new object();
        private static JobManager instance = null;

        private readonly IItemRepository _ir;
        private readonly ILogger _logger;
        private Dictionary<string, DetectionJob> jobs;
        private Dictionary<string, List<IntroInfo>> intro_data = new Dictionary<string, List<IntroInfo>>();

        private bool StopWorker = false;

        private JobManager(ILogger logger, IItemRepository ir)
        {
            _ir = ir;
            _logger = logger;
            _logger.Info("JobManager Created");

            jobs = new Dictionary<string, DetectionJob>();

            Thread t = new Thread(new ThreadStart(WorkerThread));
            t.Start();
        }

        public static JobManager GetInstance(ILogger logger, IItemRepository ir)
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new JobManager(logger, ir);
                }
                return instance;
            }
        }

        public void StopWorkerThread()
        {
            StopWorker = true;
            _logger.Info("Stopping WorkerThread Thread");
        }

        public void AddJob(DetectionJob job)
        {
            lock (padlock)
            {
                _logger.Info("Adding Jobs");
                jobs.Add(job.added.Ticks.ToString(), job);
            }
        }

        public Dictionary<string, List<IntroInfo>> GetIntroData()
        {
            return intro_data;
        }

        public void SetIntroData(Dictionary<string, List<IntroInfo>> data)
        {
            intro_data = data;
        }

        public bool CancelJob(string job_id)
        {
            bool action = false;
            lock (padlock)
            {
                _logger.Info("Canceling Job : " + job_id);
                if (jobs.ContainsKey(job_id) && jobs[job_id].status == JobStatus.Running)
                {
                    jobs[job_id].status = JobStatus.Canceled;
                    action = true;
                }
            }
            return action;
        }

        public bool RemoveJob(string job_id)
        {
            bool action = false;
            lock (padlock)
            {
                _logger.Info("Removing Job : " + job_id);
                if (jobs.ContainsKey(job_id))
                {
                    if (jobs[job_id].status == JobStatus.Running)
                    {
                        jobs[job_id].status = JobStatus.Canceled;
                    }

                    jobs.Remove(job_id);
                    action = true;
                }
            }
            return action;
        }

        public Dictionary<string, DetectionJob> GetJobList()
        {
            return jobs;
        }

        public void WorkerThread()
        {
            _logger.Info("Detection WorkerThread Started");
            while (StopWorker == false)
            {
                // find next job to work on
                DetectionJob job = null;
                lock (padlock)
                {
                    List<string> keys = jobs.Keys.ToList();

                    // remove finished jobs after set time
                    foreach (string key in keys)
                    {
                        if (jobs[key].finished != null)
                        {
                            DateTime remote_at = jobs[key].finished.Value + TimeSpan.FromHours(jobs[key].keep_finished_for);
                            if (remote_at < DateTime.Now)
                            {
                                jobs.Remove(key);
                            }
                        }
                    }

                    // get next waiting job
                    keys = jobs.Keys.ToList();
                    keys.Sort();
                    foreach (string key in keys)
                    {
                        if (jobs[key].status == JobStatus.Waiting)
                        {
                            job = jobs[key];
                            job.status = JobStatus.Running;
                            break;
                        }
                    }
                }

                // if we have a job then work on it
                if (job != null)
                {
                    _logger.Info("Detection WorkerThread Processing Job : " + job.name);
                    try
                    {
                        ProcessJob(job);
                    }
                    catch(Exception e)
                    {
                        job.finished = DateTime.Now;
                        job.status = JobStatus.Error;
                        job.message = e.Message;
                        _logger.Error("Error Processing Job : " + job.name + " - " + e.Message);
                    }
                }

                if(StopWorker)
                {
                    break;
                }

                Thread.Sleep(2000);
            }

            _logger.Info("Detection WorkerThread Exited");
        }

        private void ProcessJob(DetectionJob job)
        {
            Detection detector = new Detection(_logger, job.ffmpeg_path);

            job.items.Sort(delegate (DetectionJobItem c1, DetectionJobItem c2)
            {
                string c1_str = c1.name;
                string c2_str = c2.name;
                int cmp_restlt = string.Compare(c1_str, c2_str, comparisonType: StringComparison.OrdinalIgnoreCase);
                return cmp_restlt;
            });

            ChapterManager chapter_manager = new ChapterManager(_ir);
            foreach (DetectionJobItem item in job.items)
            {
                //Thread.Sleep(10000);
                if (StopWorker)
                {
                    job.status = JobStatus.Canceled;
                    break;
                }

                // detect introes
                detector.ProcessJobItem(item, job.intro_info_list, job.threshold);
                item.status = JobItemStatus.Complete;

                // insert detected chapters
                if (item.detection_result != null && item.detection_result.found_intro && job.auto_insert)
                {
                    chapter_manager.InsertChapters(item);
                }

                if (job.status == JobStatus.Canceled)
                {
                    break;
                }
            }

            if (job.status != JobStatus.Canceled)
            {
                job.status = JobStatus.Complete;
            }

            job.finished = DateTime.Now;
        }

    }
}
