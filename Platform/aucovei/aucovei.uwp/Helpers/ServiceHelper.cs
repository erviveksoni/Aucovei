using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace aucovei.uwp.Helpers
{
    public class ServiceHelper
    {
        private HttpClient httpClient = null;

        public ServiceHelper()
        {
            var filter = new HttpBaseProtocolFilter();
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.InvalidName);
            filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
            this.httpClient = new HttpClient(filter);
            this.httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + App.AppData.AuthResult.AccessToken);
        }

        public async Task<bool> IsAucovieOnlineAsync(string deviceId)
        {
            if (App.AppData.AuthResult == null)
            {
                throw new ArgumentNullException("AuthResult");
            }

            if (deviceId == null)
            {
                throw new ArgumentNullException("AccessToken");
            }

            bool isOnline = false;
            HttpResponseMessage response = await this.httpClient.GetAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/devices/{deviceId}/status"));
            if (response.IsSuccessStatusCode)
            {
                isOnline = bool.Parse(await response.Content.ReadAsStringAsync());
            }
            else
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Sorry, you don't have access to the Service.  Please sign-in again.");
                }
                else
                {
                    throw new UnauthorizedAccessException("Sorry, an error occurred accessing the service.  Please try again.");
                }
            }

            return isOnline;
        }

        public async Task<dynamic> GetDevicesAsync()
        {
            if (App.AppData.AuthResult == null)
            {
                throw new ArgumentNullException("AuthResult");
            }

            dynamic results = false;
            HttpResponseMessage response = await this.httpClient.GetAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/devices?take=50"));
            if (response.IsSuccessStatusCode)
            {
                results = await response.Content.ReadAsStringAsync();
            }
            else
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Sorry, you don't have access to the Service.  Please sign-in again.");
                }
                else
                {
                    throw new UnauthorizedAccessException("Sorry, an error occurred accessing the service.  Please try again.");
                }
            }

            return results;
        }

        public async Task<bool> SendCommandAsync(string deviceName, string commandName, KeyValuePair<string, string> methodparams)
        {
            bool result = false;
            if (App.AppData.AuthResult == null)
            {
                throw new ArgumentNullException("AuthResult");
            }

            dynamic results = false;

            Windows.Web.Http.HttpFormUrlEncodedContent content = null;
            if (methodparams.Key != null &&
                !string.IsNullOrEmpty(methodparams.Key))
            {
                content = new HttpFormUrlEncodedContent(new[] { methodparams });
            }

            var response = await this.httpClient.PostAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/devices/{deviceName}/commands/{commandName}"), content);
            if (response.IsSuccessStatusCode)
            {
                //results = await response.Content.ReadAsStringAsync();
                result = true;
            }
            else
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Sorry, you don't have access to the Service.  Please sign-in again.");
                }
                else
                {
                    throw new UnauthorizedAccessException("Sorry, an error occurred accessing the service.  Please try again.");
                }
            }

            return result;
        }

        public async Task<dynamic> GetDeviceTelemetryAsync(string deviceName)
        {
            if (App.AppData.AuthResult == null)
            {
                throw new ArgumentNullException("AuthResult");
            }

            dynamic results = false;

            DateTime dt = DateTime.UtcNow.AddMinutes(-1);
            string datestr = dt.ToString(CultureInfo.InvariantCulture);

            var response = await this.httpClient.GetAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/telemetry/list?deviceId={deviceName}&minTime={datestr}"));
            if (response.IsSuccessStatusCode)
            {
                results = await response.Content.ReadAsStringAsync();
            }
            else
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Sorry, you don't have access to the Service.  Please sign-in again.");
                }
                else
                {
                    throw new UnauthorizedAccessException("Sorry, an error occurred accessing the service.  Please try again.");
                }
            }

            return results;
        }

    }
}
