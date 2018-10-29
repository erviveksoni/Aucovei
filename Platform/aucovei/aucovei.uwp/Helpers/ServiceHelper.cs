using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            httpClient = new HttpClient(filter);
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + App.AuthResult.AccessToken);
        }

        public async Task<bool> IsAucovieOnline(string deviceId)
        {
            if (App.AuthResult == null)
            {
                throw new ArgumentNullException("AuthResult");
            }

            if (deviceId == null)
            {
                throw new ArgumentNullException("AccessToken");
            }

            bool isOnline = false;
            HttpResponseMessage response = await httpClient.GetAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/devices/{deviceId}/status"));
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

        public async Task<dynamic> GetDevices()
        {
            if (App.AuthResult == null)
            {
                throw new ArgumentNullException("AuthResult");
            }

            dynamic results = false;
            HttpResponseMessage response = await httpClient.GetAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/devices?take=50"));
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

        public async Task<bool> SendCommand(string deviceName, string commandName, KeyValuePair<string, string> methodparams)
        {
            bool result = false;
            if (App.AuthResult == null)
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

            var response = await httpClient.PostAsync(new Uri(App.AucoveiRestBaseAddress + $"/api/v1/devices/{deviceName}/commands/{commandName}"), content);
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
    }
}
