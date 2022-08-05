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

define(['mainTabsManager', 'dialogHelper'], function (
    mainTabsManager, dialogHelper) {
    'use strict';

    var ticks_per_sec = 10000000;
    var selected_job_id = "";

    function getTabList() {
        var tab_list = [
            {
                href: Dashboard.getConfigurationPageUrl('chapters'),
                name: 'Chapters'
            },
            {
                href: Dashboard.getConfigurationPageUrl('summary'),
                name: 'Intro Summary'
            },
            {
                href: Dashboard.getConfigurationPageUrl('detect'),
                name: 'Intro Detect'
            }
        ];
        return tab_list;
    }

    ApiClient.getApiData = function (url_to_get) {
        console.log("getUserActivity Url = " + url_to_get);
        return this.ajax({
            type: "GET",
            url: url_to_get,
            dataType: "json"
        });
    };

    ApiClient.sendPostQuery = function (url_to_get, query_data) {
        var post_data = JSON.stringify(query_data);
        console.log("sendPostQuery url  = " + url_to_get);
        //console.log("sendPostQuery data = " + post_data);
        return this.ajax({
            type: "POST",
            url: url_to_get,
            dataType: "json",
            data: post_data,
            contentType: 'application/json'
        });
    };

    function SendJobData(view, file_content, job_type, item_id) {

        var query_data = {
            IntroInfo: file_content,
            ItemId: item_id,
            JobType: job_type
        };

        var url = "chapter_api/add_detection_job?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.sendPostQuery(url, query_data).then(function (result) {
            console.log("Job creation results : " + JSON.stringify(result));
            RefreshJobs(view);
        });
        
    }

    function RunDetection(view) {

        var series_list = view.querySelector("#series_list");
        var season_list = view.querySelector("#season_list");
        var episode_list = view.querySelector("#episode_list");

        var series_id = series_list.options[series_list.selectedIndex].value;
        var season_id = season_list.options[season_list.selectedIndex].value;
        var episode_id = episode_list.options[episode_list.selectedIndex].value;

        console.log("series_id  : " + series_id);
        console.log("season_id  : " + season_id);
        console.log("episode_id : " + episode_id);

        var job_type = "";
        var item_id = "-1";

        if (series_id === "-1" && season_id === "-1" && episode_id === "-1") {
            alert("No items selected");
            return;
        }
        else if (series_id !== "-1" && season_id === "-1" && episode_id === "-1") {
            job_type = "series";
            item_id = series_id
        }
        else if (series_id !== "-1" && season_id !== "-1" && episode_id === "-1") {
            job_type = "season";
            item_id = season_id;
        }
        else if (series_id !== "-1" && season_id !== "-1" && episode_id !== "-1") {
            job_type = "episode";
            item_id = episode_id;
        }

        console.log("Job Type : " + job_type);
        console.log("Item Id  : " + item_id);

        const theme_info_file = view.querySelector("#theme_info_file");
        if (theme_info_file.files.length === 0) {
            alert("No file selected");
            return;
        }

        const selected_file = theme_info_file.files[0];
        //console.log(selected_file);

        const reader = new FileReader();
        reader.readAsText(selected_file, "UTF-8");

        reader.onload = (evt) => {
            console.log("SendJobData");
            SendJobData(view, evt.target.result, job_type, item_id);
        };

        reader.onerror = (evt) => {
            console.log("Error loading file");
        };
    }

    function PopulateSeriesNames(view) {
        console.log("PopulateSeriesNames");

        var url = "chapter_api/get_series_list?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (series_list_data) {
            console.log("Series List Data: " + JSON.stringify(series_list_data));

            var series_list = view.querySelector("#series_list");
            var options_html = "<option value='-1'>Select Series</option>";
            for (const series of series_list_data) {
                options_html += "<option value='" + series.Id + "'>" + series.Name + "</option>";
            }
            series_list.innerHTML = options_html;
        });
    }

    function PopulateSeasons(view) {
        console.log("PopulateSeasons");

        var series_list = view.querySelector("#series_list");
        var season_list = view.querySelector("#season_list");

        var selected_series_id = series_list.options[series_list.selectedIndex].value;
        if (selected_series_id === "-1") {
            season_list.innerHTML = "<option value='-1'>All Season</option>";
            return;
        }

        var url = "chapter_api/get_season_list";
        url += "?id=" + selected_series_id;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (season_list_data) {
            console.log("Season List Data: " + JSON.stringify(season_list_data));

            var options_html = "<option value='-1'>All Season</option>";
            for (const season of season_list_data) {
                options_html += "<option value='" + season.Id + "'>" + season.Name + "</option>";
            }
            season_list.innerHTML = options_html;
        });

    }

    function PopulateEpisodes(view) {
        console.log("PopulateEpisodes");

        //var series_list = view.querySelector("#series_list");
        var season_list = view.querySelector("#season_list");
        var episode_list = view.querySelector("#episode_list");

        var selected_season_id = season_list.options[season_list.selectedIndex].value;

        if (selected_season_id === "-1") {
            episode_list.innerHTML = "<option value='-1'>All Episodes</option>";
            return;
        }

        var episode_url = "chapter_api/get_episode_list";
        episode_url += "?id=" + selected_season_id;
        episode_url += "&stamp=" + new Date().getTime();
        episode_url = ApiClient.getUrl(episode_url);

        ApiClient.getApiData(episode_url).then(async function (episode_list_data) {
            console.log("Episode List Data: " + JSON.stringify(episode_list_data));

            var options_html = "<option value='-1'>All Episodes</option>";
            for (const episode of episode_list_data) {
                options_html += "<option value='" + episode.Id + "'>" + episode.Name + "</option>";
            }
            episode_list.innerHTML = options_html;
        });

    }

    function SetSelectedJob(view, job_id) {

        const job_item_list = view.querySelector("#job_list");

        for (const tr_item of job_item_list.childNodes) {
            var td_job_id = tr_item.getAttribute("job_id");

            console.log("job_list : " + td_job_id + " - " + job_id);

            if (td_job_id === job_id) {
                tr_item.style.backgroundColor = "#77FF7730";
            }
            else {
                tr_item.style.backgroundColor = "";
            }
        }

    }

    function PopulateJobInfo(view, job_id) {
        console.log("Populate Job Info : " + job_id);

        selected_job_id = job_id;

        // set selected job item
        SetSelectedJob(view, job_id);

        var url = "chapter_api/get_job_info";
        url += "?id=" + job_id;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (job_info_data) {
            console.log("Job Info Data : " + JSON.stringify(job_info_data));

            // populate the job info
            const job_info_summary = view.querySelector("#job_info");
            var job_info_html = "<table>";
            job_info_html += "<tr><td>Id</td><td>" + job_info_data.Id + "</td></tr>";
            job_info_html += "<tr><td>Name</td><td>" + job_info_data.Name + "</td></tr>";
            job_info_html += "<tr><td>Added</td><td>" + job_info_data.Added + "</td></tr>";
            job_info_html += "<tr><td>Items</td><td>" + job_info_data.ItemCount + "</td></tr>";
            job_info_html += "<tr><td>Status</td><td>" + job_info_data.Status + "</td></tr>";
            job_info_html += "</table>";

            job_info_summary.innerHTML = job_info_html;

            // populate the job item list
            const job_item_list = view.querySelector("#job_item_list");

            while (job_item_list.firstChild) {
                job_item_list.removeChild(job_item_list.firstChild);
            }

            for (const job_item of job_info_data.Items) {
                var tr = document.createElement("tr");

                var td = document.createElement("td");
                td.appendChild(document.createTextNode(job_item.Name));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_item.StartTime));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_item.EndTime));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                tr.appendChild(td);

                //td = document.createElement("td");
                //td.appendChild(document.createTextNode(job_item.Duration));
                //td.style.overflow = "hidden";
                //td.style.whiteSpace = "nowrap";
                //tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_item.Time));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_item.Found));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                tr.appendChild(td);
                
                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_item.Status));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                tr.appendChild(td);

                job_item_list.appendChild(tr);
            }
        });
    }

    function CancelJob(view, job_id) {
        console.log("CancelJob : " + job_id);

        var url = "chapter_api/cancel_job";
        url += "?id=" + job_id;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (cancel_action_result) {
            console.log("Cancel Job Result : " + JSON.stringify(cancel_action_result));
            RefreshJobs(view);
        });
    }

    function RemoveJob(view, job_id) {
        console.log("RemoveJob : " + job_id);

        var url = "chapter_api/remove_job";
        url += "?id=" + job_id;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (remove_action_result) {
            console.log("Remove Job Result : " + JSON.stringify(remove_action_result));
            RefreshJobs(view);
        });
    }

    function RefreshJobs(view) {

        const job_info_summary = view.querySelector("#job_info");
        job_info_summary.innerHTML = "";
        const job_item_list = view.querySelector("#job_item_list");
        while (job_item_list.firstChild) {
            job_item_list.removeChild(job_item_list.firstChild);
        }

        const job_list = view.querySelector("#job_list");

        // clear table
        while (job_list.firstChild) {
            job_list.removeChild(job_list.firstChild);
        }

        var url = "chapter_api/get_job_list?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (job_list_data) {
            console.log("Job List Data: " + JSON.stringify(job_list_data));

            var selected_exists = false;

            for (const job_info of job_list_data) {

                var tr = document.createElement("tr");
                var td = null;

                if (job_info.Status === "Running") {
                    td = document.createElement("td");
                    td.appendChild(document.createTextNode("!"));
                    td.style.width = "35px";
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    td.style.cursor = "pointer";
                    td.addEventListener("click", function () {
                        CancelJob(view, job_info.Id);
                    });
                    tr.appendChild(td);
                }
                else {
                    td = document.createElement("td");
                    td.appendChild(document.createTextNode("X"));
                    td.style.width = "30px";
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    td.style.cursor = "pointer";
                    td.addEventListener("click", function () {
                        RemoveJob(view, job_info.Id);
                    });
                    tr.appendChild(td);
                }

                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_info.Name + " (" + job_info.Count + ")"));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                td.style.cursor = "pointer";
                td.addEventListener("click", function () {
                    PopulateJobInfo(view, job_info.Id);
                });
                tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(job_info.Status));
                td.style.width = "75px";
                tr.appendChild(td);

                if (job_info.Id == selected_job_id) {
                    selected_exists = true;
                    //tr.style.backgroundColor = "#77FF7730";
                }

                tr.setAttribute("job_id", job_info.Id);

                job_list.appendChild(tr);
            }

            if (selected_exists) {
                PopulateJobInfo(view, selected_job_id);
            }
        });

    }

    return function (view, params) {

        // init code here
        view.addEventListener('viewshow', function (e) {

            mainTabsManager.setTabs(this, 2, getTabList);

            var series_list = view.querySelector("#series_list");
            series_list.addEventListener("change", function () {
                PopulateSeasons(view);
                PopulateEpisodes(view);
            });

            var season_list = view.querySelector("#season_list");
            season_list.addEventListener("change", function () { PopulateEpisodes(view); });
            
            PopulateSeriesNames(view);

            const run_detection = view.querySelector("#run_detection");
            run_detection.addEventListener("click", function () { RunDetection(view); });

            const refresh_jobs = view.querySelector("#refresh_jobs");
            refresh_jobs.addEventListener("click", function () { RefreshJobs(view); });

            RefreshJobs(view);
        });

        view.addEventListener('viewhide', function (e) {

        });

        view.addEventListener('viewdestroy', function (e) {

        });
    };
});
