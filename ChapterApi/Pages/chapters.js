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

define(['mainTabsManager'], function (mainTabsManager) {
    'use strict';

    ApiClient.getApiData = function (url_to_get) {
        console.log("getUserActivity Url = " + url_to_get);
        return this.ajax({
            type: "GET",
            url: url_to_get,
            dataType: "json"
        });
    };

    function AddChapter(view) {

        var item_id = view.querySelector('#add_item_id').value;
        var add_name = view.querySelector('#add_name').value;

        var add_type_select = view.querySelector('#add_type');
        var add_type = add_type_select.options[add_type_select.selectedIndex].value;

        var add_hour = view.querySelector('#add_hour').value;
        var add_minute = view.querySelector('#add_minute').value;
        var add_second = view.querySelector('#add_second').value;
        var time_string = add_hour + ":" + add_minute + ":" + add_second;

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

    function RemoveChapter(view, item_info, chapter_info) {

        console.log("Removing Chapter: ItemInfo:" + JSON.stringify(item_info) + " ChapterInfo:" + JSON.stringify(chapter_info));

        var url = "chapter_api/update_chapters?id=" + item_info.Id;
        url += "&action=remove";
        url += "&index=" + chapter_info.Index;
        url += "&stamp=" + new Date().getTime();
        url = ApiClient.getUrl(url);

        ApiClient.getApiData(url).then(function (chapter_data) {
            console.log("Loaded Remove Data: " + JSON.stringify(chapter_data));
            PopulateChapterInfo(view, item_info);
        });
        
    }

    function PopulateChapterInfo(view, item_info) {
        console.log(item_info);

        view.querySelector("#search_text").value = "";

        if (item_info !== null) {
            var url = "chapter_api/get_chapters?id=" + item_info.Id;
            url += "&stamp=" + new Date().getTime();
            url = ApiClient.getUrl(url);
            ApiClient.getApiData(url).then(function (item_chapter_data) {
                console.log("Loaded Chapter Data: " + JSON.stringify(item_chapter_data));

                var chapter_data = item_chapter_data.chapters;
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

                // show add form
                var add_form_div = view.querySelector('#add_form_div');
                if (item_data.ItemType.toUpperCase() === "MOVIE" || item_data.ItemType.toUpperCase() === "EPISODE") {
                    add_form_div.style.display = "block";
                }
                else {
                    add_form_div.style.display = "none";
                }

                // clean and populate the chapters
                var display_chapter_list = view.querySelector('#display_chapter_list');
                while (display_chapter_list.firstChild) {
                    display_chapter_list.removeChild(display_chapter_list.firstChild);
                }

                var row_count = 0;
                for (const chapter of chapter_data) {
                    var tr = document.createElement("tr");
                    var td = null;

                    td = document.createElement("td");
                    td.appendChild(document.createTextNode(chapter.Name));
                    tr.appendChild(td);

                    td = document.createElement("td");
                    td.appendChild(document.createTextNode(chapter.MarkerType));
                    tr.appendChild(td);

                    td = document.createElement("td");
                    td.appendChild(document.createTextNode(chapter.StartTime));
                    tr.appendChild(td);

                    td = document.createElement("td");
                    var i = document.createElement("i");
                    i.className = "md-icon";
                    i.style.fontSize = "25px";
                    i.style.cursor = "pointer";
                    i.appendChild(document.createTextNode("highlight_off"));

                    i.addEventListener("click", function () {
                        var result = confirm("Are you sure you want to remove this chapter?\n" + item_info.Name + " (" + chapter.Name + ":" + chapter.MarkerType + ":" + chapter.StartTime + ")");
                        if (result) {
                            RemoveChapter(view, item_info, chapter);
                        }
                     });

                    td.appendChild(i);
                    td.style.width = "1px";
                    tr.appendChild(td);

                    if (chapter.MarkerType !== "Chapter") {
                        tr.style.backgroundColor = "#FF777720";
                    }
                    else if (row_count % 2 === 0) {
                        tr.style.backgroundColor = "#77FF7720";
                    }
                    else {
                        tr.style.backgroundColor = "#7777FF20";
                    }
                    
                    display_chapter_list.appendChild(tr);

                    row_count++;
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

            //var tabs = [
            //    {
            //        href: Dashboard.getConfigurationPageUrl('chapters'),
            //        name: 'Chapters'
            //    }
            //];

            //mainTabsManager.setTabs(this, 0, tabs);

            var add_chapter_button = view.querySelector('#add_chapter_button');
            add_chapter_button.addEventListener("click", function () { AddChapter(view); });

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
