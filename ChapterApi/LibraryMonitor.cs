using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace ChapterApi
{
    public class LibraryMonitor : IServerEntryPoint
    {

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger _logger;

        public LibraryMonitor(
                ILibraryManager libraryManager,
                ILogManager logger)
        {
            _logger = logger.GetLogger("ChapterApi - LibraryMonitor");
            _libraryManager = libraryManager;
        }

        public void Run()
        {
            _logger.Info("Adding Add Item Event Monitor");
            _libraryManager.ItemAdded += ItemAdded;
        }

        public void Dispose()
        {
            
        }

        void ItemAdded(object sender, ItemChangeEventArgs e)
        {
            _logger.Info("Item Added");

        }

    }
}
