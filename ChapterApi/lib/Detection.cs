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
using System.Text;

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

        public void ProcessJobItem(DetectionJobItem job_item, int extract, byte[] theme_cp_byte_data)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            TimeSpan duration = TimeSpan.FromSeconds(extract);

            _logger.Info("Extracted CP from : " + job_item.item.Name);
            byte[] episode_cp_data = ExtractChromaprint(duration, job_item.item.Path);
            _logger.Info("Extracted CP data length : " + episode_cp_data.Length);

            job_item.cp_data_len = episode_cp_data.Length;

            FindBestOffset(episode_cp_data, duration, theme_cp_byte_data, job_item);

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            job_item.detection_duration = ts.ToString(@"hh\:mm\:ss\.fff");
        }

        private byte[] ExtractChromaprint(
            TimeSpan ts_duration,
            string media_path)
        {
            List<string> command_params = new List<string>();

            command_params.Add("-accurate_seek");
            //command_params.Add(string.Format("-ss {0}", ts_start));
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

            return chroma_bytes;
        }

        private bool FindBestOffset(
            byte[] episode_cp_bytes,
            TimeSpan duration,
            byte[] theme_cp_bytes,
            DetectionJobItem job_item)
        {
            List<uint> episode_cp_uints = BytesToInts(episode_cp_bytes);
            List<uint> theme_cp_uints = BytesToInts(theme_cp_bytes);

            if (episode_cp_uints.Count == 0 || theme_cp_uints.Count == 0 || theme_cp_uints.Count > episode_cp_uints.Count)
            {
                _logger.Info("Error with cp data : episode[" + episode_cp_uints.Count + "] theme[" + theme_cp_uints.Count + "]");
                return false;
            }

            List<uint> distances = GetDistances(episode_cp_uints, theme_cp_uints);

            int? best_start_offset = GetBestOffset(distances, job_item);

            if (best_start_offset == null)
            {
                job_item.found_intro = false;
                _logger.Info("Theme not found!");
                return false;
            }
            job_item.found_intro = true;

            // based on testing it looks like it is about 8.06 ints per second
            // based on the options used in the ffmpeg audio mixing and cp muxing
            // TODO: this need further investigation
            // https://github.com/acoustid/chromaprint/issues/45
            // double ints_per_sec = 8.06; // this is calculated by extracting a bunch of test data and comparing them
            // for now lets use the duration and extracted byte array length to calculate this 
            double ints_per_sec = (episode_cp_bytes.Length / duration.TotalSeconds) / 4;

            // also remember we are using int offsets, this is 4 bytes, we could get better
            // granularity by comparing bytes for byte and use actual byte offsets in our best match

            double theme_start = best_start_offset.Value / ints_per_sec;
            TimeSpan ts_start = TimeSpan.FromSeconds(theme_start);

            double theme_end = theme_start + (theme_cp_uints.Count / ints_per_sec);
            TimeSpan ts_end = TimeSpan.FromSeconds(theme_end);

            job_item.start_time = ts_start.ToString(@"hh\:mm\:ss\.fff");
            job_item.start_time_ticks = ts_start.Ticks;

            job_item.end_time = ts_end.ToString(@"hh\:mm\:ss\.fff");
            job_item.end_time_ticks = ts_end.Ticks;

            TimeSpan into_duration = ts_end - ts_start;
            job_item.duration_time = into_duration.ToString(@"hh\:mm\:ss\.fff");
            job_item.duration_time_ticks = into_duration.Ticks;

            _logger.Info("Theme At : " + ts_start + " - " + ts_end);

            return true;
        }

        private int? GetBestOffset(List<uint> distances, DetectionJobItem job_item)
        {
            uint sum_distances = 0;
            uint min_dist = uint.MaxValue;
            int? min_offset = null;
            for (int x = 0; x < distances.Count; x++)
            {
                sum_distances += distances[x];
                if (distances[x] < min_dist)
                {
                    min_dist = distances[x];
                    min_offset = x;
                }
            }

            double average_distance = sum_distances / distances.Count;
            uint distance_threshold = (uint)(average_distance * 0.5);  // TODO: find a good threshold

            job_item.min_distance = min_dist;
            job_item.avg_distance = average_distance;
            job_item.dist_threshold = distance_threshold;
            job_item.min_offset = min_offset;

            _logger.Info("Min Distance        : " + min_dist);
            _logger.Info("Average Distance    : " + average_distance);
            _logger.Info("Distance Threshold  : " + distance_threshold);
            _logger.Info("Min Distance Offset : " + min_offset);

            if (min_dist > distance_threshold)
            {
                job_item.min_dist_found = false;
                _logger.Info("Min distance was not below average distance threshold!");
                return null;
            }

            job_item.min_dist_found = true;

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
