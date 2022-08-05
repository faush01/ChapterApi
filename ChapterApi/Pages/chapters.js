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

    function GetVideoUrl(item_id, startTime) {
        var url = "Videos/" + item_id + "/stream.mp4";
        url += "?StartTimeTicks=" + startTime;
        url += "&VideoCodec=h264";
        url += "&AudioCodec=mp3";
        url += "&VideoBitrate=1000000";
        url += "&AudioBitrate=128000";
        url += "&allowVideoStreamCopy=false";
        url += "&MaxWidth=640";
        //url += "&AudioStreamIndex=1";
        //url += "&SubtitleStreamIndex=12";
        //url += "&SubtitleMethod=Hls";
        //url += "&TranscodingMaxAudioChannels=2";
        //url += "&SegmentContainer=m4s,ts";
        //url += "&MinSegments=1";
        //url += "&BreakOnNonKeyFrames=True";
        //url += "&ManifestSubtitles=vtt";
        //url += "&h264-profile=high,main,baseline,constrainedbaseline,high10";
        //url += "&h264-level=52";
        //url += "&TranscodeReasons=AudioCodecNotSupported,DirectPlayError";
        url += "&PlaySessionId=" + new Date().getTime(); 
        url += "&api_key=" + ApiClient._serverInfo.AccessToken;
        url += "&n=" + new Date().getTime();
        var url = ApiClient.getUrl(url);
        return url;
    }

    function pad2(pad, num) {
        return num.toString().padStart(pad, '0');
    }

    function GetTimeString(time) {

        let milliseconds = Math.floor(time * 1000);
        let seconds = Math.floor(milliseconds / 1000);
        let minutes = Math.floor(seconds / 60);
        let hours = Math.floor(minutes / 60);

        milliseconds = milliseconds % 1000;
        seconds = seconds % 60;
        minutes = minutes % 60;
        //hours = hours % 24;

        return pad2(2, hours) + ":" + pad2(2, minutes) + ":" + pad2(2, seconds) + "." + pad2(3, milliseconds);

        //const result = new Date(time * 1000).toISOString().slice(11, 19) + " - " + time;
        //return result;
    }

    function PlayChapter(view, item_info, chapter_info) {
        //alert("play");
        console.log("Play Chapter : item_info=" + JSON.stringify(item_info) + " chapter_info=" + JSON.stringify(chapter_info));

        var start_time_offset = 0;
        if (chapter_info.StartPositionTicks > (5 * ticks_per_sec)) {
            start_time_offset = chapter_info.StartPositionTicks - (5 * ticks_per_sec);
        }
        var chapter_start_sec = chapter_info.StartPositionTicks / ticks_per_sec;

        var dlg = dialogHelper.createDialog({ removeOnClose: true, size: 'small' });
        dlg.classList.add('ui-body-a');
        dlg.classList.add('background-theme-a');
        dlg.classList.add('formDialog');
        dlg.style.maxWidth = '50%';
        dlg.style.maxHeight = '65%';

        var html = '';
        html += '<div class="formDialogHeader">';
        html += '<button is="paper-icon-button-light" class="btnCancel autoSize" tabindex="-1">';
        html += '<i class="md-icon">&#xE5C4;</i>';
        html += '</button>';
        html += '<h3 class="formDialogHeaderTitle">Chapter : ' + chapter_info.Name + ' (' + chapter_info.StartTime + ')</h3>';
        html += '</div>';

        html += '<div class="formDialogContent" style="margin:2em;">';
        html += '<div class="dialogContentInner" style="max-width: 100%; justify-content: center;">';

        var video_url = GetVideoUrl(item_info.Id, start_time_offset);
        console.log("Chapter Play URL : " + video_url);

        html += '<table style="width:100%;"><tr><td style="text-align: center;">';

        html += '<video style="width:80%; height:80%" autoplay controls ';
        html += 'webkit-playsinline="" playsinline="" crossorigin="anonymous" ';
        html += 'src = "' + video_url + '">';
        html += '</video>';

        html += '</td></tr></table>';

        html += '<br /><div id="progression" style="font-weight: bold;"></div>';

        html += '</div>';
        html += '</div>';

        dlg.innerHTML = html;

        var video = dlg.querySelector('video');
        var progress = dlg.querySelector('#progression');
        var time_offset_sec = start_time_offset / ticks_per_sec;

        video.addEventListener("timeupdate", function () {
            var prog_time = "Time: " + GetTimeString(this.currentTime + time_offset_sec);
            progress.innerHTML = prog_time;
            if (chapter_start_sec < (this.currentTime + time_offset_sec)) {
                progress.style.backgroundColor = "#00FF00";
            }
            else {
                progress.style.backgroundColor = "#FF0000";
            }
        });

        dlg.querySelectorAll('.btnCancel').forEach(btn => {
            btn.addEventListener('click', (e) => {
                dialogHelper.close(dlg);
            });
        });

        dialogHelper.open(dlg);

    }

    function CopyTime(view, start_time) {
        var tokens = start_time.split(".");
        var msec = tokens[1];
        tokens = tokens[0].split(":");
        view.querySelector('#add_hour').value = tokens[0];
        view.querySelector('#add_minute').value = tokens[1];
        view.querySelector('#add_second').value = tokens[2];
        view.querySelector('#add_msecond').value = msec;
    }

    function AutoCreateChapters(view) {

        var item_id = view.querySelector("#add_item_id").value;
        var auto_add_interval_select = view.querySelector("#auto_chapter_interval");
        var auto_add_interval = auto_add_interval_select.options[auto_add_interval_select.selectedIndex].value;
        var auto_chapter_name = view.querySelector("#auto_chapter_name").value;

        console.log("Chapter auto add : " + item_id + " " + auto_add_interval);

        var url = "chapter_api/update_chapters?id=" + item_id;
        url += "&action=auto";
        url += "&name=" + auto_chapter_name;
        url += "&auto_interval=" + auto_add_interval;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (add_result) {
            console.log("Loaded Auto Create Data: " + JSON.stringify(add_result));

            var item_info = { Id: item_id };
            PopulateChapterInfo(view, item_info);
        });

    }

    function AddChapter(view) {

        var item_id = view.querySelector('#add_item_id').value;
        var add_name = view.querySelector('#add_name').value;

        var add_type_select = view.querySelector('#add_type');
        var add_type = add_type_select.options[add_type_select.selectedIndex].value;

        var add_hour = view.querySelector('#add_hour').value;
        var add_minute = view.querySelector('#add_minute').value;
        var add_second = view.querySelector('#add_second').value;
        var add_msecond = view.querySelector('#add_msecond').value;
        var time_string = add_hour + ":" + add_minute + ":" + add_second + "." + add_msecond;

        var url = "chapter_api/update_chapters?id=" + item_id;
        url += "&action=add";
        url += "&name=" + add_name;
        url += "&type=" + add_type;
        url += "&time=" + time_string;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (add_result) {
            console.log("Loaded Add Data: " + JSON.stringify(add_result));

            var item_info = { Id: item_id };
            PopulateChapterInfo(view, item_info);
        });
    }

    function SelectAll(view) {
        var select_all_check = view.querySelector("#chapter_select_all");
        var selectors = view.querySelectorAll("input#chapter_select");
        for (const selector of selectors) {
            selector.checked = select_all_check.checked;
        }
    }

    function RemoveChapter(view, item_info, chapter_info) {

        var message = "";
        var remove_indexes = "";
        if (chapter_info == null) {
            var selectors = view.querySelectorAll("input#chapter_select");

            var selected = [];
            for (const selector of selectors) {
                var checked = selector.checked;
                var chap_id = selector.chap_index;
                if (checked) {
                    selected.push(chap_id);
                }
            }
            remove_indexes = selected.join(',');

            if (selected.length > 0) {
                message = "Are you sure you want to remove (" + selected.length + ") chapters?";
            }
        }
        else {
            message = "Are you sure you want to remove this chapter?";
            remove_indexes = chapter_info.Index;
        }

        if (message !== "") {
            var result = confirm(message);
            if (!result) {
                return;
            }
        }
        else {
            return;
        }

        console.log("Removing Chapter: ItemInfo:" + JSON.stringify(item_info) + " Remove Indexes: " + remove_indexes);

        var url = "chapter_api/update_chapters?id=" + item_info.Id;
        url += "&action=remove";
        url += "&index_list=" + remove_indexes;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (chapter_data) {
            console.log("Loaded Remove Data: " + JSON.stringify(chapter_data));
            PopulateChapterInfo(view, item_info);
        });
        
    }

    function PopulateChapterInfo(view, item_info) {
        console.log(item_info);

        var cell_padding = "2px 5px 2px 5px";

        view.querySelector("#search_text").value = "";

        if (item_info !== null) {
            var url = "chapter_api/get_chapters?id=" + item_info.Id;
            url += "&stamp=" + new Date().getTime();
            url = ApiClient.getUrl(url);
            ApiClient.getApiData(url).then(function (item_chapter_data) {
                console.log("Loaded Chapter Data: " + JSON.stringify(item_chapter_data));

                var chapter_data = item_chapter_data.chapters;
                var episode_data = item_chapter_data.episodes;
                var item_data = item_chapter_data.item_info;

                // populate the item info
                var display_item_info = view.querySelector('#display_item_info');
                var item_display_info = "<strong>" + item_data.Name + "</strong><br>";
                item_display_info += item_data.ItemType + "<br>";
                //item_display_info += "Item Id : " + item_data.Id + "<br>";
                //item_display_info += "Series : " + item_data.Series + "<br>";
                display_item_info.innerHTML = item_display_info;

                // set form data
                view.querySelector('#add_item_id').value = item_data.Id;
                view.querySelector('#add_name').value = "";
                view.querySelector('#add_type').selectedIndex = 0;
                view.querySelector('#add_hour').value = "00";
                view.querySelector('#add_minute').value = "00";
                view.querySelector('#add_second').value = "00";
                view.querySelector('#add_msecond').value = "000";

                // clean chapters table
                var display_chapter_list = view.querySelector('#display_chapter_list');
                while (display_chapter_list.firstChild) {
                    display_chapter_list.removeChild(display_chapter_list.firstChild);
                }

                // clean episode table
                var display_episode_list = view.querySelector('#display_episode_list');
                while (display_episode_list.firstChild) {
                    display_episode_list.removeChild(display_episode_list.firstChild);
                }

                // show chapter list
                var chapter_table = view.querySelector('#chapter_table');
                var create_chapters = view.querySelector('#create_chapters');
                if (item_data.ItemType.toUpperCase() === "MOVIE" || item_data.ItemType.toUpperCase() === "EPISODE") {
                    chapter_table.style.display = "block";
                    create_chapters.style.display = "block";
                }
                else {
                    chapter_table.style.display = "none";
                    create_chapters.style.display = "none";
                }

                // show episode list
                var episodes_table = view.querySelector('#episodes_table');
                if (item_data.ItemType.toUpperCase() === "SEASON") {
                    episodes_table.style.display = "block";
                }
                else {
                    episodes_table.style.display = "none";
                }

                if (chapter_data == null) {
                    chapter_data = [];
                }
                var row_count = 0;
                for (const chapter of chapter_data) {
                    var tr = document.createElement("tr");
                    var td = null;

                    td = document.createElement("td");
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";

                    var box = document.createElement("input");
                    box.type = "checkbox";
                    box.id = "chapter_select";
                    box.chap_index = chapter.Index;
                    td.appendChild(box);

                    td.appendChild(document.createTextNode(chapter.Name));
                    td.style.padding = cell_padding;
                    tr.appendChild(td);

                    td = document.createElement("td");
                    td.appendChild(document.createTextNode(chapter.MarkerType));
                    td.style.padding = cell_padding;
                    tr.appendChild(td);

                    td = document.createElement("td");
                    td.appendChild(document.createTextNode(chapter.StartTime));
                    td.style.padding = cell_padding;
                    tr.appendChild(td);

                    td = document.createElement("td");
                    td.style.padding = cell_padding;
                    td.style.textAlign = "center";

                    var i = document.createElement("i");
                    i.title = "Delete";
                    i.className = "md-icon";
                    i.style.fontSize = "25px";
                    i.style.cursor = "pointer";
                    i.appendChild(document.createTextNode("highlight_off"));
                    i.addEventListener("click", function () { RemoveChapter(view, item_info, chapter); });
                    td.appendChild(i);

                    td.appendChild(document.createTextNode("\u00A0"));

                    i = document.createElement("i");
                    i.title = "Copy Time";
                    i.className = "md-icon";
                    i.style.fontSize = "25px";
                    i.style.cursor = "pointer";
                    i.appendChild(document.createTextNode("edit_note"));
                    i.addEventListener("click", function () { CopyTime(view, chapter.StartTime); });
                    td.appendChild(i);

                    td.appendChild(document.createTextNode("\u00A0"));

                    i = document.createElement("i");
                    i.title = "Play";
                    i.className = "md-icon";
                    i.style.fontSize = "25px";
                    i.style.cursor = "pointer";
                    i.appendChild(document.createTextNode("play_circle_outline"));
                    i.addEventListener("click", function () { PlayChapter(view, item_info, chapter); });
                    td.appendChild(i);

                    tr.appendChild(td);

                    if (chapter.MarkerType !== "Chapter") {
                        tr.style.backgroundColor = "#FF777730";
                    }
                    else if (row_count % 2 === 0) {
                        tr.style.backgroundColor = "#77FF7730";
                    }
                    else {
                        tr.style.backgroundColor = "#7777FF30";
                    }
                    
                    display_chapter_list.appendChild(tr);

                    row_count++;
                }

                // add delete all button
                if (chapter_data.length > 0) {
                    var tr = document.createElement("tr");
                    var td = document.createElement("td");

                    var check = document.createElement("input");
                    check.type = "checkbox";
                    check.id = "chapter_select_all";
                    check.addEventListener("click", function () { SelectAll(view); });
                    td.appendChild(check);

                    td.appendChild(document.createTextNode("\u00A0"));
                    td.appendChild(document.createTextNode("\u00A0"));

                    var delete_all = document.createElement("button");
                    delete_all.appendChild(document.createTextNode("Delete"));
                    delete_all.addEventListener("click", function () { RemoveChapter(view, item_info, null);  } );

                    td.appendChild(delete_all);
                    td.style.padding = cell_padding;
                    tr.appendChild(td);

                    display_chapter_list.appendChild(tr);
                }

                var show_images = view.querySelector("#show_images").checked;
                if (episode_data == null) {
                    episode_data = [];
                }
                row_count = 0;
                for (const episode of episode_data) {
                    var tr = document.createElement("tr");
                    var td = null;

                    td = document.createElement("td");
                    td.appendChild(document.createTextNode(episode.Name));
                    td.style.padding = cell_padding;
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    tr.appendChild(td);

                    var intro_start = episode.IntroStart;
                    td = document.createElement("td");
                    if (intro_start) {

                        if (show_images && intro_start.ImageTag && intro_start.ImageTag !== "") {
                            var url = "Items/" + episode.Id + "/Images/Chapter/" + intro_start.Index;
                            url += "?maxWidth=480&quality=90&tag=" + intro_start.ImageTag;
                            var img_src = ApiClient.getUrl(url);
                            console.log(img_src);
                            var img = document.createElement("img");
                            img.src = img_src;
                            img.style.width = "90px";
                            td.appendChild(img);
                            var line_break = document.createElement("br");
                            td.appendChild(line_break);
                        }

                        td.appendChild(document.createTextNode(intro_start.Time));
                    }
                    else {
                        td.appendChild(document.createTextNode("--:--:--.---"));
                    }
                    td.style.padding = cell_padding;
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    tr.appendChild(td);

                    var intro_end = episode.IntroEnd;
                    td = document.createElement("td");
                    if (intro_end) {

                        if (show_images && intro_end.ImageTag && intro_end.ImageTag !== "") {
                            var url = "Items/" + episode.Id + "/Images/Chapter/" + intro_end.Index;
                            url += "?maxWidth=480&quality=90&tag=" + intro_end.ImageTag;
                            var img_src = ApiClient.getUrl(url);
                            console.log(img_src);
                            var img = document.createElement("img");
                            img.src = img_src;
                            img.style.width = "90px";
                            td.appendChild(img);
                            var line_break = document.createElement("br");
                            td.appendChild(line_break);
                        }

                        td.appendChild(document.createTextNode(intro_end.Time));
                    }
                    else {
                        td.appendChild(document.createTextNode("--:--:--.---"));
                    }
                    td.style.padding = cell_padding;
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    tr.appendChild(td);

                    var intro_span = episode.IntroDuration;
                    td = document.createElement("td");
                    if (intro_span) {
                        td.appendChild(document.createTextNode(intro_span));
                    }
                    else {
                        td.appendChild(document.createTextNode("--:--:--.---"));
                    }
                    td.style.padding = cell_padding;
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    tr.appendChild(td);

                    var credits_info = episode.CreditsStart;
                    td = document.createElement("td");
                    if (credits_info) {

                        if (show_images && credits_info.ImageTag && credits_info.ImageTag !== "") {
                            var url = "Items/" + episode.Id + "/Images/Chapter/" + credits_info.Index;
                            url += "?maxWidth=480&quality=90&tag=" + credits_info.ImageTag;
                            var img_src = ApiClient.getUrl(url);
                            console.log(img_src);
                            var img = document.createElement("img");
                            img.src = img_src;
                            img.style.width = "90px";
                            td.appendChild(img);
                            var line_break = document.createElement("br");
                            td.appendChild(line_break);
                        }

                        td.appendChild(document.createTextNode(credits_info.Time));
                    }
                    else {
                        td.appendChild(document.createTextNode("--:--:--.---"));
                    }
                    td.style.padding = cell_padding;
                    td.style.overflow = "hidden";
                    td.style.whiteSpace = "nowrap";
                    tr.appendChild(td);

                    td = document.createElement("td");

                    if (intro_start && intro_end) {
                        var intro_duration = (intro_end.TimeTicks - intro_start.TimeTicks) / ticks_per_sec;
                        if (intro_duration > 10 && intro_duration < 300) {
                            var link = document.createElement("a");

                            link.style.color = "inherit";

                            var export_link = "chapter_api/extract_theme?id=" + episode.Id;
                            export_link += "&stamp=" + new Date().getTime();
                            export_link = ApiClient.getUrl(export_link);
                            link.href = export_link;

                            var i = document.createElement("i");
                            i.title = "Extract Intro Chromaprint";
                            i.className = "md-icon";
                            i.style.fontSize = "25px";
                            //i.style.cursor = "pointer";
                            i.appendChild(document.createTextNode("file_download"));
                            link.appendChild(i);
                            td.appendChild(link);
                        }
                    }

                    tr.appendChild(td);

                    if (row_count % 2 === 0) {
                        tr.style.backgroundColor = "#77FF7730";
                    }
                    else {
                        tr.style.backgroundColor = "#7777FF30";
                    }
                    row_count++;

                    display_episode_list.appendChild(tr);
                }

            });
        }

    }

    function PopulateSelectedPath(view, item_info) {

        var path_string = view.querySelector("#path_string");
        while (path_string.firstChild) {
            path_string.removeChild(path_string.firstChild);
        }

        if (item_info === null) {
            path_string.appendChild(document.createTextNode("\\"));
            return;
        }

        var url = "chapter_api/get_item_path";
        url += "?id=" + item_info.Id;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (item_path_data) {
            console.log("Loaded Path Data: " + JSON.stringify(item_path_data));

            //var path_link_data = "";
            for (const path_info of item_path_data) {

                var span = document.createElement("span");
                span.appendChild(document.createTextNode(path_info.Name));
                span.style.cursor = "pointer";

                span.addEventListener("click", function () {
                    var item_data = { Name: path_info.Name, Id: path_info.Id };
                    PopulateSelector(view, item_data, "");
                    PopulateChapterInfo(view, item_data); 
                    PopulateSelectedPath(view, item_data);
                });

                path_string.appendChild(document.createTextNode("\\"));
                path_string.appendChild(span);

            }
        });

    }

    function PopulateSelector(view, item_info, search_filter) {

        var parent_id = 0;
        var url = "chapter_api/get_items?";

        var is_seaerch = false;

        if (search_filter !== null && search_filter.length > 0) {
            url += "parent=0";
            url += "&filter=" + search_filter;
            is_seaerch = true;
        }
        else if (item_info !== null) {
            parent_id = item_info.Id;
            url += "parent=" + parent_id;
        }
        else {
            url += "parent=0";
        }

        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (item_data) {
            //alert("Loaded Data: " + JSON.stringify(item_data));

            var item_list = view.querySelector('#item_list');

            // clear current list
            if (is_seaerch || (item_info !== null && item_data.length > 0)) {
                while (item_list.firstChild) {
                    item_list.removeChild(item_list.firstChild);
                }
            }

            // add items
            for (const item_details of item_data) {
                var tr = document.createElement("tr");
                var td = document.createElement("td");
                td.appendChild(document.createTextNode(item_details.Name));
                td.style.overflow = "hidden";
                td.style.whiteSpace = "nowrap";
                td.addEventListener("click", function () {
                    PopulateSelectedPath(view, item_details);
                    PopulateChapterInfo(view, item_details);
                    PopulateSelector(view, item_details, "");
                });
                td.style.cursor = "pointer";
                tr.appendChild(td);
                item_list.appendChild(tr);
            }

        });
    }

    var qr_timeout = null;
    function SearchChanged(view, search_box) {

        if (qr_timeout != null) {
            clearTimeout(qr_timeout);
        }

        qr_timeout = setTimeout(function () {

            var search_text = search_box.value;
            search_text = search_text.trim();
            console.log("search: " + search_text);
            var item_info = { Id: "0" };
            PopulateSelector(view, item_info, search_text);

        }, 500);

    }

    return function (view, params) {

        // init code here
        view.addEventListener('viewshow', function (e) {

            mainTabsManager.setTabs(this, 0, getTabList);

            var add_chapter_button = view.querySelector('#add_chapter_button');
            add_chapter_button.addEventListener("click", function () { AddChapter(view); });

            var auto_create_button = view.querySelector('#auto_create_button');
            auto_create_button.addEventListener("click", function () { AutoCreateChapters(view); });

            var search_box = view.querySelector("#search_text");
            search_box.addEventListener("input", function () { SearchChanged(view, search_box); });
            PopulateSelector(view, null, "");

        });

        view.addEventListener('viewhide', function (e) {

        });

        view.addEventListener('viewdestroy', function (e) {

        });
    };
});
