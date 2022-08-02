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

    function CreateResultNode(result) {

        var tr = document.createElement("tr");

        var td = document.createElement("td");
        td.appendChild(document.createTextNode(result.Name));
        tr.appendChild(td);

        var td = document.createElement("td");
        td.appendChild(document.createTextNode(result.StartTime));
        tr.appendChild(td);

        var td = document.createElement("td");
        td.appendChild(document.createTextNode(result.EndTime));
        tr.appendChild(td);

        var td = document.createElement("td");
        td.appendChild(document.createTextNode(result.Duration));
        tr.appendChild(td);

        var td = document.createElement("td");
        td.appendChild(document.createTextNode(result.Time));
        tr.appendChild(td);
        
        td = document.createElement("td");
        td.appendChild(document.createTextNode(result.Result));
        tr.appendChild(td);

        return tr;
    }

    async function SendRunData(view, file_content, season_id) {

        //console.log(file_content);

        var detection_status = view.querySelector("#detection_status");
        var detection_results_table = view.querySelector("#detection_results_table");

        // empty table
        while (detection_results_table.firstChild) {
            detection_results_table.removeChild(detection_results_table.firstChild);
        }

        var episode_url = "chapter_api/get_episode_list";
        episode_url += "?id=" + season_id;
        episode_url += "&stamp = " + new Date().getTime();
        episode_url = ApiClient.getUrl(episode_url);

        await ApiClient.getApiData(episode_url).then(async function (episode_list_data) {
            console.log("Episode List Data: " + JSON.stringify(episode_list_data));
            var count = 1;
            for (const episode of episode_list_data) {

                detection_status.innerHTML = "Processing : " + count + " of " + episode_list_data.length;

                var query_data = {
                    IntroInfo: file_content,
                    EpisodeId: episode.Id
                };
                var url = "chapter_api/detect_episode_intro?stamp=" + new Date().getTime();
                url = ApiClient.getUrl(url);

                await ApiClient.sendPostQuery(url, query_data).then(function (result) {
                    console.log("Episode Intro Detection Results : " + JSON.stringify(result));

                    var result_row = CreateResultNode(result);
                    detection_results_table.appendChild(result_row);
                });

                count++;
            }
            detection_status.innerHTML = "Processing : Done";
        });

        /*
        var url = "chapter_api/detect_season_intros?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        var query_data = {
            IntroInfo: file_content,
            SeasonId: season_id
        };

        await ApiClient.sendPostQuery(url, query_data).then(function (result) {
            console.log("Query Results : " + JSON.stringify(result));

        });
        */

        console.log("SendRunData Done!");
    }

    function RunDetection(view) {

        var season_list = view.querySelector("#season_list");
        if (season_list.selectedIndex === -1) {
            console.log("No season selected");
            return;
        }
        var season_id = season_list.options[season_list.selectedIndex].value;
        console.log("Season Id : " + season_id);

        const theme_info_file = view.querySelector("#theme_info_file");
        if (theme_info_file.files.length === 0) {
            console.log("No file selected");
            return;
        }

        const selected_file = theme_info_file.files[0];
        //console.log(selected_file);

        const reader = new FileReader();
        reader.readAsText(selected_file, "UTF-8");

        reader.onload = (evt) => {
            SendRunData(view, evt.target.result, season_id);
        };

        reader.onerror = (evt) => {
            console.log("Error loading file");
        };
    }

    function PopulateSeriesNames(view) {

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

        var season_list = view.querySelector("#season_list");

        var series_list = view.querySelector("#series_list");
        var selected_series_id = series_list.options[series_list.selectedIndex].value;
        if (selected_series_id === "-1") {
            season_list.innerHTML = "";
            return;
        }

        var url = "chapter_api/get_season_list";
        url += "?id=" + selected_series_id;
        url += "&stamp = " + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (season_list_data) {
            console.log("Season List Data: " + JSON.stringify(season_list_data));

            var options_html = "";
            for (const season of season_list_data) {
                options_html += "<option value='" + season.Id + "'>" + season.Name + "</option>";
            }
            season_list.innerHTML = options_html;
        });

    }

    return function (view, params) {

        // init code here
        view.addEventListener('viewshow', function (e) {

            mainTabsManager.setTabs(this, 2, getTabList);

            var series_list = view.querySelector("#series_list");
            series_list.addEventListener("change", function () { PopulateSeasons(view); });

            PopulateSeriesNames(view);

            const run_detection = view.querySelector("#run_detection");
            run_detection.addEventListener("click", function () { RunDetection(view); });
            
        });

        view.addEventListener('viewhide', function (e) {

        });

        view.addEventListener('viewdestroy', function (e) {

        });
    };
});
