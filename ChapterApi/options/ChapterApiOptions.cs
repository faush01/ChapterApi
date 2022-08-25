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

using System;
using System.Collections.Generic;
using System.Text;

namespace ChapterApi.options
{
    public class ChapterApiOptions
    {
        public bool ProcessAddedItems { get; set; } = true;
        public int KeepFinishdJobFor { get; set; } = 24;
        public string IntroDataPath { set; get; }
        public string IntroDataExternalUrl { set; get; } = "https://themeservice.azurewebsites.net/Home/Search?download=true";
    }
}
