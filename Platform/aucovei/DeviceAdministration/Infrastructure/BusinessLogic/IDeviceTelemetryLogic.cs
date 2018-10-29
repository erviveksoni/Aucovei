using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.BusinessLogic
{
    public interface IDeviceTelemetryLogic
    {
        Task<IEnumerable<DeviceTelemetryModel>> LoadLatestDeviceTelemetryAsync(
            string deviceId,
            IList<DeviceTelemetryFieldModel> telemetryFields,
            DateTime minTime);

        Task<DeviceTelemetrySummaryModel> LoadLatestDeviceTelemetrySummaryAsync(
            string deviceId,
            DateTime? minTime);

        Func<string, DateTime?> ProduceGetLatestDeviceAlertTime(
            IEnumerable<AlertHistoryItemModel> alertHistoryModels);

        Task<RouteWavepointModel> LoadWavepointForRoute(string routeId, string pointId);

        Task<RouteWavepointListModel> LoadWavepointsListForRoute(string routeId);

        Task<RouteModel> AddRouteAsync(dynamic route);

        Task<RouteWavepointUpdateModel> UpdateWavepointOnRouteAsync(dynamic routewavepoint);

    }
}