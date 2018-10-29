using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.ActiveDirectory;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using System;
using System.Globalization;
using System.IdentityModel.Tokens;
using System.Threading.Tasks;
using System.Web;
using AuthenticationContext = Microsoft.IdentityModel.Clients.ActiveDirectory.AuthenticationContext;


namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web
{
    public partial class Startup
    {
        public void ConfigureAuth(IAppBuilder app, IConfigurationProvider configProvider)
        {
            string aadClientId = configProvider.GetConfigurationSettingValue("ida.AADClientId");
            string aadInstance = configProvider.GetConfigurationSettingValue("ida.AADInstance");
            string aadTenant = configProvider.GetConfigurationSettingValue("ida.AADTenant");
            string authority = string.Format(CultureInfo.InvariantCulture, aadInstance, aadTenant);

            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = aadClientId,
                    Authority = authority,
                    PostLogoutRedirectUri = configProvider.GetConfigurationSettingValue("RedirectUrl"),
                    RedirectUri = configProvider.GetConfigurationSettingValue("RedirectUrl"),

                    TokenValidationParameters = new TokenValidationParameters
                    {
                        // SaveSigninToken = true,
                        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                    },
                    Notifications = new OpenIdConnectAuthenticationNotifications
                    {
                        AuthenticationFailed = async context =>
                        {
                            string appBaseUrl = context.Request.Scheme + "://" + context.Request.Host + context.Request.PathBase;

                            context.ProtocolMessage.RedirectUri = appBaseUrl + "/";
                            context.HandleResponse();
                            context.Response.Redirect(context.ProtocolMessage.RedirectUri);
                            await Task.FromResult(0).ConfigureAwait(false);
                        },
                        RedirectToIdentityProvider = async context =>
                        {
                            context.ProtocolMessage.Prompt = "login";
                            await Task.FromResult(0).ConfigureAwait(false);
                        },
                        AuthorizationCodeReceived = async context =>
                        {
                            /*
                            var code = context.Code;
                            ClientCredential credential = new ClientCredential(aadClientId, "DA7uLMjQK9UTDyVQD9nR8k47ahhHJhfRykBcJgTxMSY=");
                            AuthenticationContext authContext = new AuthenticationContext(authority, TokenCache.DefaultShared);
                            Uri uri = new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Path));
                            var result = await authContext.AcquireTokenByAuthorizationCodeAsync(code, uri, credential, "https://graph.windows.net").ConfigureAwait(false);
                            string accessToken = result.AccessToken;
                            var header = result.CreateAuthorizationHeader();
                            */
                        }
                    }
                });

            this.WSFedAuth(app, configProvider);
        }

        private void WSFedAuth(IAppBuilder app, IConfigurationProvider configProvider)
        {
            /*
            string federationMetadataAddress = configProvider.GetConfigurationSettingValue("ida.FederationMetadataAddress");
            string federationRealm = configProvider.GetConfigurationSettingValue("ida.FederationRealm");

            if (string.IsNullOrEmpty(federationMetadataAddress) || string.IsNullOrEmpty(federationRealm))
            {
                throw new ApplicationException("Config issue: Unable to load required federation values from web.config or other configuration source.");
            }

            // check for default values that will cause app to fail to startup with an unhelpful 404 exception
            if (federationMetadataAddress.StartsWith("-- ", StringComparison.Ordinal) ||
                federationRealm.StartsWith("-- ", StringComparison.Ordinal))
            {
                throw new ApplicationException("Config issue: Default federation values from web.config need to be overridden or replaced.");
            }

            app.UseWsFederationAuthentication(
                new WsFederationAuthenticationOptions
                {
                    MetadataAddress = federationMetadataAddress,
                    Wtrealm = federationRealm
                });

            */

            string aadTenant = configProvider.GetConfigurationSettingValue("ida.AADTenant");
            string aadAudience = configProvider.GetConfigurationSettingValue("ida.AADAudience");

            if (string.IsNullOrEmpty(aadTenant) || string.IsNullOrEmpty(aadAudience))
            {
                throw new ApplicationException("Config issue: Unable to load required AAD values from web.config or other configuration source.");
            }

            // check for default values that will cause failure
            if (aadTenant.StartsWith("-- ", StringComparison.Ordinal) ||
                aadAudience.StartsWith("-- ", StringComparison.Ordinal))
            {
                throw new ApplicationException("Config issue: Default AAD values from web.config need to be overridden or replaced.");
            }

            // Fallback authentication method to allow "Authorization: Bearer <token>" in the header for WebAPI calls
            app.UseWindowsAzureActiveDirectoryBearerAuthentication(
                new WindowsAzureActiveDirectoryBearerAuthenticationOptions
                {
                    Tenant = aadTenant,
                    TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidAudience = aadAudience,

                        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role" // Used to unwrap token roles and provide them to [Authorize(Roles="")] attributes
                    },
                });
        }
    }
}