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

using MediaBrowser.Controller.Entities;
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
        public JobStatus status { get; set; } = JobStatus.Waiting;
        public string name { get; set; }
        public IntroInfo intro_info { get; set; }
        public List<DetectionJobItem> items { get; } = new List<DetectionJobItem>();
        public byte[] intro_cp_data { get; set; }
        public string ffmpeg_path { get; set; }
        public string message { get; set; }
    }

    public class DetectionJobItem
    {
        public BaseItem item { get; set; }
        public JobItemStatus status { get; set; } = JobItemStatus.Waiting;
        public int cp_data_len { get; set; }
        public string detection_duration { set; get; }
        public bool found_intro { get; set; } = false;
        public string start_time { set; get; }
        public long start_time_ticks { set; get; }
        public string end_time { set; get; }
        public long end_time_ticks { set; get; }
        public string duration_time { set; get; }
        public long duration_time_ticks { set; get; }
        public uint min_distance { set; get; }
        public double avg_distance { set; get; }
        public uint dist_threshold { set; get; }
        public int? min_offset { set; get; }
        public bool min_dist_found { set; get; } = false;
    }


    public class JobManager
    {
        private static readonly object padlock = new object();
        private static JobManager instance = null;
        
        private readonly ILogger _logger;
        private Dictionary<string, DetectionJob> jobs;

        private JobManager(ILogger logger)
        {
            _logger = logger;
            _logger.Info("JobManager Created");

            jobs = new Dictionary<string, DetectionJob>();

            Thread t = new Thread(new ThreadStart(WorkerThread));
            t.Start();
        }

        public static JobManager GetInstance(ILogger logger)
        {
            lock (padlock)
            {
                if (instance == null)
                {
                    instance = new JobManager(logger);
                }
                return instance;
            }
        }

        public void AddJob(DetectionJob job)
        {
            lock (padlock)
            {
                _logger.Info("Adding Jobs");
                jobs.Add(job.added.Ticks.ToString(), job);
            }
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
            while (true)
            {
                // find next job to work on
                DetectionJob job = null;
                lock (padlock)
                {
                    List<string> keys = jobs.Keys.ToList();
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
                        job.status = JobStatus.Error;
                        job.message = e.Message;
                        _logger.Error("Error Processing Job : " + job.name + " - " + e.Message);
                    }
                }

                Thread.Sleep(5000);
            }
        }

        private void ProcessJob(DetectionJob job)
        {
            Detection detector = new Detection(_logger, job.ffmpeg_path);

            foreach (DetectionJobItem item in job.items)
            {
                //Thread.Sleep(10000);
                detector.ProcessJobItem(item, job.intro_info.extract, job.intro_cp_data);
                item.status = JobItemStatus.Complete;

                if (job.status == JobStatus.Canceled)
                {
                    break;
                }
            }

            if (job.status != JobStatus.Canceled)
            {
                job.status = JobStatus.Complete;
            }
        }

    }
}