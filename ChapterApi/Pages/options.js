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
            },
            {
                href: Dashboard.getConfigurationPageUrl('options'),
                name: 'Options'
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

    function PopulateSettingsPage(view) {

        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            console.log("Config Options : " + JSON.stringify(config));

            view.querySelector("#keep_for").value = config.KeepFinishdJobFor;
            view.querySelector('#intro_data_path_label').innerHTML = config.IntroDataPath;
            view.querySelector('#input_data_url').value = config.IntroDataExternalUrl;
            view.querySelector('#process_added_items').checked = config.ProcessAddedItems;
            view.querySelector('#process_updated_items').checked = config.ProcessUpdatedItems;
            view.querySelector('#detection_threshold').value = config.DetectionThreshold;
        });

        var url = "chapter_api/intro_data_stats?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);
        ApiClient.getApiData(url).then(function (loaded_stats) {
            console.log("Loaded Stats Result : " + JSON.stringify(loaded_stats));

            let message_string = loaded_stats.SeriesCount + " series " + loaded_stats.ItemCount + " items";
            const loaded_intro_data_label = view.querySelector("#loaded_intro_data_label");
            loaded_intro_data_label.innerHTML = message_string;

            const loaded_intro_data_list = view.querySelector("#loaded_intro_data_list");
            while (loaded_intro_data_list.firstChild) {
                loaded_intro_data_list.removeChild(loaded_intro_data_list.firstChild);
            }

            let row_count = 0;
            for (const intro_item of loaded_stats.IntroItems) {
                var tr = document.createElement("tr");
                var td = null;

                td = document.createElement("td");
                td.appendChild(document.createTextNode(intro_item.imdb));
                tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(intro_item.series));
                tr.appendChild(td);

                td = document.createElement("td");
                td.appendChild(document.createTextNode(intro_item.count));
                tr.appendChild(td);

                if (row_count % 2 === 0) {
                    tr.style.backgroundColor = "#77FF7730";
                }
                else {
                    tr.style.backgroundColor = "#7777FF30";
                }
                row_count++;

                loaded_intro_data_list.appendChild(tr);
            }
        });
    }

    function UpdateDataUrl(text_box) {
        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            console.log("Config Options : " + JSON.stringify(config));
            let new_url = text_box.value;
            console.log("New Data URL : " + new_url);
            config.IntroDataExternalUrl = new_url;
            ApiClient.updateNamedConfiguration("chapter_api", config);
        });
    }

    function KeepForSelectedChanged(selector) {
        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            console.log("Config Options : " + JSON.stringify(config));
            let keep_for = selector.value;
            console.log("Keep For New : " + keep_for);
            config.KeepFinishdJobFor = keep_for;
            ApiClient.updateNamedConfiguration("chapter_api", config);
        });
    }

    function ShowDataPathPathPicker(view) {
        require(['directorybrowser'], function (directoryBrowser) {
            var picker = new directoryBrowser();
            picker.show({
                includeFiles: false,
                callback: function (selected) {
                    picker.close();
                    DataPathSelectedCallBack(selected, view);
                },
                header: "Select Intro Data Path"
            });
        });
    }

    function DataPathSelectedCallBack(selectedDir, view) {
        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            config.IntroDataPath = selectedDir;
            console.log("New Config Settings : " + JSON.stringify(config));
            ApiClient.updateNamedConfiguration("chapter_api", config);

            var intro_data_path_label = view.querySelector('#intro_data_path_label');
            intro_data_path_label.innerHTML = selectedDir;
        });
    }

    function ReloadIntroData(view) {
        var url = "chapter_api/reload_intro_data?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (reload_result) {
            console.log("ReloadIntroData Result : " + JSON.stringify(reload_result));
            PopulateSettingsPage(view);
            alert("Intro data reloaded : " + reload_result.Result + "\n" + reload_result.Message);
        });
    }

    function DownloadIntroData(view) {
        var url = "chapter_api/download_intro_data?stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (download_result) {
            console.log("DownloadIntroData Result : " + JSON.stringify(download_result));
            
            alert("Intro data downloaded : " + download_result.Result + "\n" + download_result.Message);
        });
    }

    function ProcessAddedChanged(checkbox) {
        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            console.log("Config Options : " + JSON.stringify(config));
            config.ProcessAddedItems = checkbox.checked;
            ApiClient.updateNamedConfiguration("chapter_api", config);
        });
    }

    function ProcessUpdatedChanged(checkbox) {
        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            console.log("Config Options : " + JSON.stringify(config));
            config.ProcessUpdatedItems = checkbox.checked;
            ApiClient.updateNamedConfiguration("chapter_api", config);
        });
    }

    function UpdateDetectionThreshold(text_box) {
        ApiClient.getNamedConfiguration("chapter_api").then(function (config) {
            console.log("Config Options : " + JSON.stringify(config));

            let threshold = parseFloat(text_box.value);

            if (threshold && threshold > 0.0 && threshold < 1.0) {
                config.DetectionThreshold = threshold;
                text_box.value = threshold;
                ApiClient.updateNamedConfiguration("chapter_api", config);
            }
            else {
                text_box.value = config.DetectionThreshold;
            }
        });
    }

    return function (view, params) {

        // init code here
        view.addEventListener('viewshow', function (e) {

            mainTabsManager.setTabs(this, 3, getTabList);

            PopulateSettingsPage(view);

            view.querySelector("#keep_for").addEventListener("change", function () {
                KeepForSelectedChanged(this);
            });

            view.querySelector('#set_intro_data_path').addEventListener("click", function () {
                ShowDataPathPathPicker(view);
            });

            view.querySelector('#reload_intro_data').addEventListener("click", function () {
                ReloadIntroData(view);
            });
            
            view.querySelector('#download_intro_data').addEventListener("click", function () {
                DownloadIntroData(view);
            });

            view.querySelector('#input_data_url').addEventListener("change", function () {
                UpdateDataUrl(this);
            });

            view.querySelector('#process_added_items').addEventListener("change", function () {
                ProcessAddedChanged(this);
            });

            view.querySelector('#process_updated_items').addEventListener("change", function () {
                ProcessUpdatedChanged(this);
            });

            view.querySelector('#detection_threshold').addEventListener("change", function () {
                UpdateDetectionThreshold(this);
            });

        });

        view.addEventListener('viewhide', function (e) {

        });

        view.addEventListener('viewdestroy', function (e) {

        });
    };
});
