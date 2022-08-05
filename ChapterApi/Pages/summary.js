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
        console.log("getApiData Url = " + url_to_get);
        return this.ajax({
            type: "GET",
            url: url_to_get,
            dataType: "json"
        });
    };

    return function (view, params) {

        // init code here
        view.addEventListener('viewshow', function (e) {

            mainTabsManager.setTabs(this, 1, getTabList);

            var url = "chapter_api/get_summary?type=series";
            url += "&stamp=" + new Date().getTime();
            url = ApiClient.getUrl(url);

            var series_summary_table = view.querySelector('#series_intro_summary');

            while (series_summary_table.firstChild) {
                series_summary_table.removeChild(series_summary_table.firstChild);
            }

            var loading_tr = document.createElement("tr");
            var loading_td = document.createElement("td");
            loading_td.appendChild(document.createTextNode("Loading Data..."));
            loading_td.colSpan = "5";
            loading_tr.appendChild(loading_td);
            series_summary_table.appendChild(loading_tr);

            ApiClient.getApiData(url).then(function (summary_data) {
                console.log("Loaded summary data : " + JSON.stringify(summary_data));

                while (series_summary_table.firstChild) {
                    series_summary_table.removeChild(series_summary_table.firstChild);
                }

                for (const series_data of summary_data) {

                    var tr = document.createElement("tr");
                    var td = null;

                    td = document.createElement("td");
                    var series_percentage = (series_data.IntroCount / series_data.EpisodeCount) * 100;
                    var series_text = series_data.Name + " (" + Math.floor(series_percentage) + "%)";
                    td.appendChild(document.createTextNode(series_text));
                    td.colSpan = "5";
                    td.style.borderBottom = "1px solid";

                    tr.appendChild(td);

                    series_summary_table.appendChild(tr);

                    var season_data = series_data.Seasons;

                    for (const season of season_data) {
                        tr = document.createElement("tr");
                        td = document.createElement("td");
                        tr.appendChild(td);

                        td = document.createElement("td");
                        td.appendChild(document.createTextNode(season.Name));
                        tr.appendChild(td);

                        td = document.createElement("td");
                        td.appendChild(document.createTextNode(season.EpisodeCount));
                        tr.appendChild(td);

                        td = document.createElement("td");
                        var intro_percent = (season.IntroCount / season.EpisodeCount) * 100;
                        var intro_text = season.IntroCount + " (" + Math.floor(intro_percent) + "%)";
                        td.appendChild(document.createTextNode(intro_text));
                        tr.appendChild(td);

                        td = document.createElement("td");
                        var credits_percent = (season.CreditsCount / season.EpisodeCount) * 100;
                        var credits_text = season.CreditsCount + " (" + Math.floor(credits_percent) + "%)";
                        td.appendChild(document.createTextNode(credits_text));
                        tr.appendChild(td);

                        series_summary_table.appendChild(tr);
                    }
                }

            });

        });

        view.addEventListener('viewhide', function (e) {

        });

        view.addEventListener('viewdestroy', function (e) {

        });
    };
});
