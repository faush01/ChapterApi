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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ChapterApi
{
    public class Detection
    {
        private ILogger _logger;
        private string _ffmpeg_path;

        public Detection(ILogger log, string ffp)
        {
            _ffmpeg_path = ffp;
            _logger = log;
        }

        public void ProcessJobItem(DetectionJobItem job_item, List<IntroInfo> intro_info_list, double threshold)
        {
            Stopwatch stop_watch_total = new Stopwatch();
            stop_watch_total.Start();

            //Thread.Sleep(1000);

            // find longest intro extract
            int extract = int.MinValue;
            foreach(IntroInfo intro in intro_info_list)
            {
                if(intro.extract > extract)
                {
                    extract = intro.extract;
                }
            }
            TimeSpan duration = TimeSpan.FromMinutes(extract);
            
            // extract cp data from episode
            _logger.Info("Extracted CP from : " + job_item.item.Name);
            _logger.Info("Extracted CP duration : " + duration);

            Stopwatch stop_watch_extract = new Stopwatch();
            stop_watch_extract.Start();

            byte[] episode_cp_data = ExtractChromaprint(duration, job_item.item.Path);
            _logger.Info("Extracted CP data length : " + episode_cp_data.Length);
            
            stop_watch_extract.Stop();
            job_item.job_extract_time = stop_watch_total.Elapsed.TotalMilliseconds;

            // run all the intro detections
            Stopwatch stop_watch_detect = new Stopwatch();
            stop_watch_detect.Start();

            job_item.detection_result_list = new List<DetectionResult>();
            foreach (IntroInfo intro in intro_info_list)
            {
                DetectionResult result = FindBestOffset(episode_cp_data, duration, intro.cp_data_bytes, threshold);
                if (result != null)
                {
                    result.intro_info = intro;
                    job_item.detection_result_list.Add(result);
                }
            }

            stop_watch_detect.Stop();
            job_item.job_detect_time = stop_watch_detect.Elapsed.TotalMilliseconds;

            // find the best intro detect result
            DetectionResult best_result = null;
            foreach (DetectionResult result in job_item.detection_result_list)
            {
                if(best_result == null && result.found_intro)
                {
                    best_result = result;
                }

                if(best_result != null && result.found_intro && result.min_distance < best_result.min_distance)
                {
                    best_result = result;
                }
            }
            job_item.detection_result = best_result;

            stop_watch_total.Stop();
            TimeSpan ts = stop_watch_total.Elapsed;
            job_item.job_total_time = ts.TotalMilliseconds;
            job_item.job_duration = ts.TotalSeconds.ToString("#.000");

            _logger.Info("ProcessJobItem Extract Time : " + job_item.job_extract_time);
            _logger.Info("ProcessJobItem Detect Time  : " + job_item.job_detect_time);
            _logger.Info("ProcessJobItem Total Time   : " + job_item.job_total_time);
        }

        private byte[] ExtractChromaprint(
            TimeSpan ts_duration,
            string media_path)
        {
            List<string> command_params = new List<string>();

            command_params.Add("-accurate_seek");
            command_params.Add("-i \"" + media_path + "\"");
            //command_params.Add(string.Format("-ss {0}", ts_start.TotalSeconds));
            command_params.Add(string.Format("-t {0}", ts_duration.TotalSeconds));
            command_params.Add("-ac 1");
            command_params.Add("-acodec pcm_s16le");
            command_params.Add("-ar 44100");
            command_params.Add("-c:v nul");
            command_params.Add("-f chromaprint");
            command_params.Add("-fp_format raw");
            command_params.Add("-");

            string param_string = string.Join(" ", command_params);

            _logger.Info("Extracting chromaprint : " + _ffmpeg_path + " " + param_string);

            ProcessStartInfo start_info = new ProcessStartInfo(_ffmpeg_path, param_string);
            start_info.RedirectStandardOutput = true;
            start_info.RedirectStandardError = false;
            start_info.UseShellExecute = false;
            start_info.CreateNoWindow = true;

            byte[] chroma_bytes = new byte[0];
            int return_code = -1;
            using (Process process = new Process() { StartInfo = start_info })
            {
                process.Start();
                Stream baseStream = process.StandardOutput.BaseStream as Stream;
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
                process.WaitForExit(1000 * 5);
                return_code = process.ExitCode;
            }

            return chroma_bytes;
        }

        private int GetByteLenthCorrection(int byte_len)
        {
            // this is a table I came up with that takes into account small CP extraction byte differences
            // the smaller the extract the more bytes are dropped in the total length
            // to be able to use the bytes number in the duration calculation you need to account for that
            if (byte_len < 884) return 83; // 15sec
            else if (byte_len < 1852) return 79; //30sec
            else if (byte_len < 2820) return 72; //60sec
            else if (byte_len < 3792) return 66; //90sec
            else if (byte_len < 4765) return 56; //120sec
            else if (byte_len < 5737) return 41; //150sec
            else if (byte_len < 6709) return 30; //180sec
            else if (byte_len < 7681) return 19; //210sec
            else if (byte_len < 8653) return 9; //240sec
            else return 0;
        }

        private DetectionResult FindBestOffset(
            byte[] episode_cp_bytes,
            TimeSpan duration,
            byte[] theme_cp_bytes,
            double threshold)
        {
            DetectionResult result = new DetectionResult();

            List<uint> episode_cp_uints = BytesToInts(episode_cp_bytes);
            List<uint> theme_cp_uints = BytesToInts(theme_cp_bytes);

            if (episode_cp_uints.Count == 0 || theme_cp_uints.Count == 0 || theme_cp_uints.Count > episode_cp_uints.Count)
            {
                _logger.Info("Error with cp data : episode[" + episode_cp_uints.Count + "] theme[" + theme_cp_uints.Count + "]");
                return null;
            }

            List<uint> distances = GetDistances(episode_cp_uints, theme_cp_uints);
            result.distances = distances;
            int? best_start_offset = GetBestOffset(distances, result, threshold);

            if (best_start_offset == null)
            {
                result.found_intro = false;
                _logger.Info("Theme not found!");
                return result;
            }
            result.found_intro = true;

            // based on testing it looks like it is about 8.06 ints per second
            // based on the options used in the ffmpeg audio mixing and cp muxing
            // TODO: this need further investigation
            // https://github.com/acoustid/chromaprint/issues/45
            //(duration_in_seconds * 11025 - 4096) / 1365 - 15 - 4 + 1
            //11025 = audio sampling rate
            //4096 = one FFT window size
            //1365 = increment of the moving FFT window
            //15 = number of classifiers -1
            //4 = number of coefficients in the chromagram smoothing filter -1
            //(((seconds * 44100) - 4096) / 1365) - 15 - 4 + 1
            // double ints_per_sec = 8.06; // this is calculated by extracting a bunch of test data and comparing them
            // for now lets use the duration and extracted byte array length to calculate this 
            double bytes_per_sec = episode_cp_bytes.Length / duration.TotalSeconds;

            // also remember we are using int offsets, this is 4 bytes, we could get better
            // granularity by comparing bytes for byte and use actual byte offsets in our best match

            double theme_start = (best_start_offset.Value * 4) / bytes_per_sec;
            TimeSpan ts_start = TimeSpan.FromSeconds(theme_start);

            int len_correction = GetByteLenthCorrection(theme_cp_bytes.Length);
            _logger.Info("Intro Byte Len : " + theme_cp_bytes.Length + " correction : " + len_correction);
            int theme_data_len = theme_cp_bytes.Length + len_correction; 
            double theme_end = theme_start + (theme_data_len / bytes_per_sec);
            TimeSpan ts_end = TimeSpan.FromSeconds(theme_end);

            result.start_time = ts_start.ToString(@"hh\:mm\:ss\.fff");
            result.start_time_ticks = ts_start.Ticks;

            result.end_time = ts_end.ToString(@"hh\:mm\:ss\.fff");
            result.end_time_ticks = ts_end.Ticks;

            TimeSpan into_duration = ts_end - ts_start;
            result.duration_time = into_duration.ToString(@"hh\:mm\:ss\.fff");
            result.duration_time_ticks = into_duration.Ticks;

            _logger.Info("Theme At : " + ts_start + " - " + ts_end);

            return result;
        }

        private int? GetBestOffset(List<uint> distances, DetectionResult result, double threshold)
        {
            uint sum_distances = 0;
            uint min_dist = uint.MaxValue;
            uint max_dist = uint.MinValue;
            int? min_offset = null;
            for (int x = 0; x < distances.Count; x++)
            {
                sum_distances += distances[x];
                if (distances[x] < min_dist)
                {
                    min_dist = distances[x];
                    min_offset = x;
                }
                if(distances[x] > max_dist)
                {
                    max_dist = distances[x];
                }
            }

            double average_distance = sum_distances / distances.Count;
            uint distance_threshold = (uint)(average_distance * threshold);

            result.sum_distance = sum_distances;
            result.max_distance = max_dist;
            result.min_distance = min_dist;
            result.avg_distance = average_distance;
            result.dist_threshold = distance_threshold;
            result.min_offset = min_offset;

            //_logger.Info("Distance List       : " + string.Join(",", distances));
            _logger.Info("Distance Sum        : " + sum_distances);
            _logger.Info("Min Distance        : " + min_dist);
            _logger.Info("Max Distance        : " + max_dist);
            _logger.Info("Average Distance    : " + average_distance);
            _logger.Info("Distance Threshold  : " + distance_threshold);
            _logger.Info("Min Distance Offset : " + min_offset);

            if (min_dist > distance_threshold)
            {
                result.min_dist_found = false;
                _logger.Info("Min distance was not below average distance threshold!");
                return null;
            }

            result.min_dist_found = true;

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
