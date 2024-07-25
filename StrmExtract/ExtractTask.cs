using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Controller.Providers;
using System.Collections;
using System.Linq;

namespace StrmExtract
{
    public class ExtractTask : IScheduledTask
    {
        private readonly ILogger _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILibraryMonitor _libraryMonitor;
        private readonly IMediaProbeManager _mediaProbeManager;

        public ExtractTask(ILibraryManager libraryManager, 
            ILogger logger, 
            IFileSystem fileSystem,
            ILibraryMonitor libraryMonitor,
            IMediaProbeManager prob)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _fileSystem = fileSystem;
            _libraryMonitor = libraryMonitor;
            _mediaProbeManager = prob;
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            _logger.Info("MediaExtract - Task Execute");

            InternalItemsQuery query = new InternalItemsQuery();

            query.HasPath = true;
            query.HasContainer = false;
            query.ExcludeItemTypes = new string[] { "Folder", "CollectionFolder", "UserView", "Series", "Season", "Trailer", "Playlist" };

            BaseItem[] results = _libraryManager.GetItemList(query);
            _logger.Info("MediaExtract - Scan items : " + results.Length);
            List<BaseItem> items = new List<BaseItem>();
            foreach(BaseItem item in  results)
            {
                if(!string.IsNullOrEmpty(item.Path) &&
                    (item.Path.Contains("softlink") || item.Path.Contains("strm")) &&
                    item.GetMediaStreams().Count == 0)
                {
                    items.Add(item);
                    _logger.Info("MediaExtract - Item added : " + " - " + item.Path);
                }
                else
                {
                    _logger.Info("MediaExtract - Item dropped : " + " - " + item.Path +  " - MediaStreams: " + item.GetMediaStreams().Count);
                }
            }

            _logger.Info("MediaExtract - Nums of targets : " + items.Count);

            double total = items.Count;
            int current = 0;
            foreach(BaseItem item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.Info("MediaExtract - Task Cancelled");
                    break;
                }
                double percent_done = (current / total) * 100;
                progress.Report(percent_done);

                MetadataRefreshOptions options = new MetadataRefreshOptions(_fileSystem);
                options.EnableRemoteContentProbe = true;
                options.ReplaceAllMetadata = true;
                options.EnableThumbnailImageExtraction = false;
                options.ImageRefreshMode = MetadataRefreshMode.ValidationOnly;
                options.MetadataRefreshMode = MetadataRefreshMode.ValidationOnly;
                options.ReplaceAllImages = false;

                ItemUpdateType resp = await item.RefreshMetadata(options, cancellationToken);

                _logger.Info("MediaExtract - " + current + "/" + total + " - " + item.Path);

                //Thread.Sleep(5000);
                current++;
            }

            progress.Report(100.0);
            _logger.Info("MediaExtract - Task Complete");
        }

        public string Category
        {
            get { return "Media Extract"; }
        }

        public string Key
        {
            get { return "MediaExtract Task"; }
        }

        public string Description
        {
            get { return "Run Media Info Extraction"; }
        }

        public string Name
        {
            get { return "Process Media targets"; }
        }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
                {
                    new TaskTriggerInfo
                    {
                        Type = TaskTriggerInfo.TriggerDaily,
                        TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
                        MaxRuntimeTicks = TimeSpan.FromHours(24).Ticks
                    }
                };
        }
    }
}
