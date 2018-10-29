using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Helpers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository
{
    using StrDict = IDictionary<string, string>;

    /// <summary>
    /// A repository for Device telemetry data.
    /// </summary>
    public class DeviceTelemetryRepository : IDeviceTelemetryRepository
    {
        private readonly string _telemetryContainerName;
        private readonly string _telemetryDataPrefix;
        private readonly string _telemetryStoreConnectionString;
        private readonly string _telemetrySummaryPrefix;

        /// <summary>
        /// Initializes a new instance of the DeviceTelemetryRepository class.
        /// </summary>
        /// <param name="configProvider">
        /// The IConfigurationProvider implementation with which to initialize 
        /// the new instance.
        /// </param>
        public DeviceTelemetryRepository(IConfigurationProvider configProvider)
        {
            if (configProvider == null)
            {
                throw new ArgumentNullException("configProvider");
            }

            this._telemetryContainerName = configProvider.GetConfigurationSettingValue("TelemetryStoreContainerName");
            this._telemetryDataPrefix = configProvider.GetConfigurationSettingValue("TelemetryDataPrefix");
            this._telemetryStoreConnectionString = configProvider.GetConfigurationSettingValue("device.StorageConnectionString");
            this._telemetrySummaryPrefix = configProvider.GetConfigurationSettingValue("TelemetrySummaryPrefix");
        }

        /// <summary>
        /// Loads the most recent Device telemetry.
        /// </summary>
        /// <param name="deviceId">
        /// The ID of the Device for which telemetry should be returned.
        /// </param>
        /// <param name="minTime">
        /// The minimum time of record of the telemetry that should be returned.
        /// </param>
        /// <returns>
        /// Telemetry for the Device specified by deviceId, inclusively since 
        /// minTime.
        /// </returns>
        public async Task<IEnumerable<DeviceTelemetryModel>> LoadLatestDeviceTelemetryAsync(
            string deviceId,
            IList<DeviceTelemetryFieldModel> telemetryFields,
            DateTime minTime)
        {
            IEnumerable<DeviceTelemetryModel> result = new DeviceTelemetryModel[0];

            CloudBlobContainer container =
                await BlobStorageHelper.BuildBlobContainerAsync(this._telemetryStoreConnectionString, this._telemetryContainerName);

            IEnumerable<IListBlobItem> blobs =
                await BlobStorageHelper.LoadBlobItemsAsync(
                    async (token) =>
                    {
                        return await container.ListBlobsSegmentedAsync(
                            this._telemetryDataPrefix,
                            true,
                            BlobListingDetails.None,
                            null,
                            token,
                            null,
                            null);
                    });

            blobs = blobs
                .OrderByDescending(t => BlobStorageHelper.ExtractBlobItemDate(t));

            CloudBlockBlob blockBlob;
            IEnumerable<DeviceTelemetryModel> blobModels;
            foreach (IListBlobItem blob in blobs)
            {
                if ((blockBlob = blob as CloudBlockBlob) == null)
                {
                    continue;
                }

                // Translate LastModified to local time zone.  DateTimeOffsets 
                // don't do this automatically.  This is for equivalent behavior 
                // with parsed DateTimes.
                if ((blockBlob.Properties != null) &&
                    blockBlob.Properties.LastModified.HasValue &&
                    (blockBlob.Properties.LastModified.Value.LocalDateTime < minTime))
                {
                    break;
                }

                try
                {
                    blobModels = await LoadBlobTelemetryModelsAsync(blockBlob, telemetryFields);
                }
                catch
                {
                    continue;
                }

                if (blobModels == null)
                {
                    break;
                }

                int preFilterCount = blobModels.Count();

                blobModels =
                    blobModels.Where(
                        t =>
                            (t != null) &&
                            t.Timestamp.HasValue &&
                            t.Timestamp.Value >= minTime);

                if (preFilterCount == 0)
                {
                    break;
                }

                result = result.Concat(blobModels);
            }

            if (!string.IsNullOrEmpty(deviceId))
            {
                result = result.Where(t => t.DeviceId == deviceId);
            }

            return result;
        }

        /// <summary>
        /// Loads the most recent DeviceTelemetrySummaryModel for a specified Device.
        /// </summary>
        /// <param name="deviceId">
        /// The ID of the Device for which a telemetry summary model should be 
        /// returned.
        /// </param>
        /// <param name="minTime">
        /// If provided the the minimum time stamp of the summary data that should 
        /// be loaded.
        /// </param>
        /// <returns>
        /// The most recent DeviceTelemetrySummaryModel for the Device, 
        /// specified by deviceId.
        /// </returns>
        public async Task<DeviceTelemetrySummaryModel> LoadLatestDeviceTelemetrySummaryAsync(
            string deviceId,
            DateTime? minTime)
        {
            DeviceTelemetrySummaryModel summaryModel = null;

            CloudBlobContainer container =
                await BlobStorageHelper.BuildBlobContainerAsync(
                    this._telemetryStoreConnectionString,
                    this._telemetryContainerName);

            IEnumerable<IListBlobItem> blobs =
                await BlobStorageHelper.LoadBlobItemsAsync(
                    async (token) =>
                    {
                        return await container.ListBlobsSegmentedAsync(
                            this._telemetrySummaryPrefix,
                            true,
                            BlobListingDetails.None,
                            null,
                            token,
                            null,
                            null);
                    });

            blobs = blobs.OrderByDescending(t => BlobStorageHelper.ExtractBlobItemDate(t));

            IEnumerable<DeviceTelemetrySummaryModel> blobModels;
            CloudBlockBlob blockBlob;

            foreach (IListBlobItem blob in blobs)
            {
                if ((blockBlob = blob as CloudBlockBlob) == null)
                {
                    continue;
                }

                // Translate LastModified to local time zone.  DateTimeOffsets 
                // don't do this automatically.  This is for equivalent behavior 
                // with parsed DateTimes.
                if (minTime.HasValue &&
                    (blockBlob.Properties != null) &&
                    blockBlob.Properties.LastModified.HasValue &&
                    (blockBlob.Properties.LastModified.Value.LocalDateTime < minTime.Value))
                {
                    break;
                }

                try
                {
                    blobModels = await LoadBlobTelemetrySummaryModelsAsync(blockBlob);
                }
                catch
                {
                    continue;
                }

                if (blobModels == null)
                {
                    break;
                }

                blobModels = blobModels.Where(t => t != null);

                if (!string.IsNullOrEmpty(deviceId))
                {
                    blobModels = blobModels.Where(t => t.DeviceId == deviceId);
                }

                summaryModel = blobModels.LastOrDefault();
                if (summaryModel != null)
                {
                    break;
                }
            }

            return summaryModel;
        }

        public async Task<RouteWavepointModel> LoadWavepointForRoute(string routeId, string pointId)
        {
            RouteWavepointModel routeWavepoint = new RouteWavepointModel();
            routeWavepoint.RouteId = 1;
            routeWavepoint.lat = 17.431883;
            routeWavepoint.lon = 78.343552;
            routeWavepoint.status = "new";

            return routeWavepoint;
        }
        public async Task<RouteWavepointListModel> LoadWavepointsListForRoute(string routeId)
        {
            RouteWavepointListModel route = new RouteWavepointListModel();
            route.RouteId = 1;
            route.Wavepoints = new List<dynamic>();

            WavepointModel wp = new WavepointModel();
            wp.WavepointId = 1;
            wp.lat = 17.431883;
            wp.lon = 78.343552;
            wp.Status = "new";
            route.Wavepoints.Add(wp);

            wp = new WavepointModel();
            wp.WavepointId = 2;
            wp.lat = 17.431831;
            wp.lon = 78.343826;
            wp.Status = "new";
            route.Wavepoints.Add(wp);

            wp = new WavepointModel();
            wp.WavepointId = 3;
            wp.lat = 17.431946;
            wp.lon = 78.343849;
            wp.Status = "new";
            route.Wavepoints.Add(wp);

            wp = new WavepointModel();
            wp.WavepointId = 4;
            wp.lat = 17.432128;
            wp.lon = 78.343807;
            wp.Status = "new";
            route.Wavepoints.Add(wp);

            wp = new WavepointModel();
            wp.WavepointId = 5;
            wp.lat = 17.432128;
            wp.lon = 78.343807;
            wp.Status = "new";
            route.Wavepoints.Add(wp);

            return route;
        }

        public async Task<RouteModel> AddRouteAsync(dynamic route)
        {
            throw new NotImplementedException();
        }

        public async Task<RouteWavepointUpdateModel> UpdateWavepointOnRouteAsync(dynamic routewavepoint)
        {
            throw new NotImplementedException();
        }

        private async static Task<List<DeviceTelemetryModel>> LoadBlobTelemetryModelsAsync(CloudBlockBlob blob, IList<DeviceTelemetryFieldModel> telemetryFields)
        {
            Debug.Assert(blob != null, "blob is a null reference.");

            List<DeviceTelemetryModel> models = new List<DeviceTelemetryModel>();

            TextReader reader = null;
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream();
                await blob.DownloadToStreamAsync(stream);
                stream.Position = 0;
                reader = new StreamReader(stream);

                IEnumerable<StrDict> strdicts = ParsingHelper.ParseCsv(reader).ToDictionaries();
                DeviceTelemetryModel model;
                string str;
                foreach (StrDict strdict in strdicts)
                {
                    model = new DeviceTelemetryModel();

                    if (strdict.TryGetValue("deviceid", out str))
                    {
                        model.DeviceId = str;
                    }

                    model.Timestamp = DateTime.Parse(
                        strdict["eventenqueuedutctime"],
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces);

                    IEnumerable<DeviceTelemetryFieldModel> fields;

                    if (telemetryFields != null && telemetryFields.Count > 0)
                    {
                        fields = telemetryFields;
                    }
                    else
                    {
                        List<string> reservedColumns = new List<string>
                        {
                            "DeviceId",
                            "EventEnqueuedUtcTime",
                            "EventProcessedUtcTime",
                            "IoTHub",
                            "PartitionId"
                        };

                        fields = strdict.Keys
                            .Where((key) => !reservedColumns.Contains(key))
                            .Select((name) => new DeviceTelemetryFieldModel
                            {
                                Name = name,
                                Type = "double"
                            });
                    }

                    foreach (var field in fields)
                    {
                        if (strdict.TryGetValue(field.Name, out str))
                        {
                            switch (field.Type.ToLowerInvariant())
                            {
                                case "int":
                                case "int16":
                                case "int32":
                                case "int64":
                                case "sbyte":
                                case "byte":
                                    int intValue;
                                    if (
                                        int.TryParse(
                                            str,
                                            NumberStyles.Integer,
                                            CultureInfo.InvariantCulture,
                                            out intValue) &&
                                        !model.Values.ContainsKey(field.Name))
                                    {
                                        model.Values.Add(field.Name, intValue);
                                    }
                                    break;

                                case "double":
                                case "decimal":
                                case "single":
                                    double dblValue;
                                    if (
                                        double.TryParse(
                                            str,
                                            NumberStyles.Float,
                                            CultureInfo.InvariantCulture,
                                            out dblValue) &&
                                        !model.Values.ContainsKey(field.Name))
                                    {
                                        model.Values.Add(field.Name, dblValue);
                                    }
                                    break;
                                case "bool":
                                    var boolValue = str.Equals("1", StringComparison.OrdinalIgnoreCase) ? true : false;
                                    if (!model.BoolValues.ContainsKey(field.Name))
                                    {
                                        model.BoolValues.Add(field.Name, boolValue);
                                    }
                                    break;
                            }
                        }
                    }

                    models.Add(model);
                }
            }
            finally
            {
                IDisposable disp;

                if ((disp = stream) != null)
                {
                    disp.Dispose();
                }

                if ((disp = reader) != null)
                {
                    disp.Dispose();
                }
            }

            return models;
        }

        private async static Task<List<DeviceTelemetrySummaryModel>> LoadBlobTelemetrySummaryModelsAsync(
            CloudBlockBlob blob)
        {
            Debug.Assert(blob != null, "blob is a null reference.");

            var models = new List<DeviceTelemetrySummaryModel>();

            TextReader reader = null;
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream();
                await blob.DownloadToStreamAsync(stream);
                stream.Position = 0;
                reader = new StreamReader(stream);

                IEnumerable<StrDict> strdicts = ParsingHelper.ParseCsv(reader).ToDictionaries();
                DeviceTelemetrySummaryModel model;
                double number;
                string str;
                foreach (StrDict strdict in strdicts)
                {
                    model = new DeviceTelemetrySummaryModel();

                    if (strdict.TryGetValue("deviceid", out str))
                    {
                        model.DeviceId = str;
                    }

                    if (strdict.TryGetValue("averagespeed", out str) &&
                       double.TryParse(
                            str,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out number))
                    {
                        model.AverageSpeed = number;
                    }

                    if (strdict.TryGetValue("maxspeed", out str) &&
                       double.TryParse(
                            str,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out number))
                    {
                        model.MaximumSpeed = number;
                    }

                    if (strdict.TryGetValue("minimumSpeed", out str) &&
                       double.TryParse(
                            str,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out number))
                    {
                        model.MinimumSpeed = number;
                    }

                    if (strdict.TryGetValue("timeframeminutes", out str) &&
                       double.TryParse(
                            str,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out number))
                    {
                        model.TimeFrameMinutes = number;
                    }

                    // Translate LastModified to local time zone.  DateTimeOffsets 
                    // don't do this automatically.  This is for equivalent behavior 
                    // with parsed DateTimes.
                    if ((blob.Properties != null) &&
                        blob.Properties.LastModified.HasValue)
                    {
                        model.Timestamp = blob.Properties.LastModified.Value.LocalDateTime;
                    }

                    models.Add(model);
                }
            }
            finally
            {
                IDisposable disp;
                if ((disp = stream) != null)
                {
                    disp.Dispose();
                }

                if ((disp = reader) != null)
                {
                    disp.Dispose();
                }
            }

            return models;
        }
    }
}
