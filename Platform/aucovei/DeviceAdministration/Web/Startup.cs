using System.Web.Http;

using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;

using Owin;

using Swashbuckle.Application;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web
{
    public partial class Startup
    {
        public static HttpConfiguration HttpConfiguration { get; private set; }

        public void Configuration(IAppBuilder app)
        {
            Startup.HttpConfiguration = new System.Web.Http.HttpConfiguration();
            ConfigurationProvider configProvider = new ConfigurationProvider();

            this.ConfigureAuth(app, configProvider);
            this.ConfigureAutofac(app);

            // WebAPI call must come after Autofac
            // Autofac hooks into the HttpConfiguration settings
            this.ConfigureWebApi(app);

            this.ConfigureJson(app);

            Startup.HttpConfiguration.EnableSwagger(
                c =>
                {
                    c.SingleApiVersion("v1", "aucovei ControlCenter API");
                    c.UseFullTypeNameInSchemaIds();
                }).EnableSwaggerUi(c =>
            {
                c.EnableDiscoveryUrlSelector();
            });
        }
    }
}