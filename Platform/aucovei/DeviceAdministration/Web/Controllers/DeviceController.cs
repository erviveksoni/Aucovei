using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using DeviceManagement.Infrustructure.Connectivity.Exceptions;
using DeviceManagement.Infrustructure.Connectivity.Models.TerminalDevice;
using DeviceManagement.Infrustructure.Connectivity.Services;
using GlobalResources;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.DeviceSchema;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.BusinessLogic;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Exceptions;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Infrastructure.Repository;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Helpers;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Models;
using Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Security;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Controllers
{
    [Authorize]
    [OutputCache(CacheProfile = "NoCacheProfile")]
    public class DeviceController : Controller
    {
        private readonly IApiRegistrationRepository _apiRegistrationRepository;
        private readonly IExternalCellularService _cellularService;
        private readonly IDeviceLogic _deviceLogic;
        private readonly IDeviceTypeLogic _deviceTypeLogic;

        private readonly string _iotHubName = string.Empty;
        public const double Device_Version_1_0 = 1.0;

        public DeviceController(IDeviceLogic deviceLogic, IDeviceTypeLogic deviceTypeLogic,
            IConfigurationProvider configProvider,
            IExternalCellularService cellularService,
            IApiRegistrationRepository apiRegistrationRepository)
        {
            this._deviceLogic = deviceLogic;
            this._deviceTypeLogic = deviceTypeLogic;
            this._cellularService = cellularService;
            this._apiRegistrationRepository = apiRegistrationRepository;

            this._iotHubName = configProvider.GetConfigurationSettingValue("iotHub.HostName");
        }

        [RequirePermission(Permission.ViewDevices)]
        public ActionResult Index()
        {
            return this.View();
        }

        [RequirePermission(Permission.AddDevices)]
        public async Task<ActionResult> AddDevice()
        {
            var deviceTypes = await this._deviceTypeLogic.GetAllDeviceTypesAsync();
            return this.View(deviceTypes);
        }

        [RequirePermission(Permission.AddDevices)]
        public async Task<ActionResult> SelectType(DeviceType deviceType)
        {
            if (this._apiRegistrationRepository.IsApiRegisteredInAzure())
            {
                try
                {
                    var devices = await this.GetDevices();
                    this.ViewBag.AvailableIccids = this._cellularService.GetListOfAvailableIccids(devices);
                    this.ViewBag.CanHaveIccid = true;
                }
                catch (CellularConnectivityException)
                {
                    this.ViewBag.CanHaveIccid = false;
                }
            }
            else
            {
                this.ViewBag.CanHaveIccid = false;
            }

            // device type logic getdevicetypeasync
            var device = new UnregisteredDeviceModel
            {
                DeviceType = deviceType,
                IsDeviceIdSystemGenerated = true
            };
            return this.PartialView("_AddDeviceCreate", device);
        }

        [HttpPost]
        [RequirePermission(Permission.AddDevices)]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddDeviceCreate(string button, UnregisteredDeviceModel model)
        {
            bool isModelValid = this.ModelState.IsValid;
            bool onlyValidating = (button != null && button.ToLower().Trim() == "check");

            if (ReferenceEquals(null, model) ||
                (model.GetType() == typeof(object)))
            {
                model = new UnregisteredDeviceModel();
            }

            if (this._apiRegistrationRepository.IsApiRegisteredInAzure())
            {
                try
                {
                    var devices = await this.GetDevices();
                    this.ViewBag.AvailableIccids = this._cellularService.GetListOfAvailableIccids(devices);
                    this.ViewBag.CanHaveIccid = true;
                }
                catch (CellularConnectivityException)
                {
                    this.ViewBag.CanHaveIccid = false;
                }
            }
            else
            {
                this.ViewBag.CanHaveIccid = false;
            }

            //reset flag
            model.IsDeviceIdUnique = false;

            if (model.IsDeviceIdSystemGenerated)
            {
                //clear the model state of errors prior to modifying the model
                this.ModelState.Clear();

                //assign a system generated device Id
                model.DeviceId = Guid.NewGuid().ToString();

                //validate the model
                isModelValid = this.TryValidateModel(model);
            }

            if (isModelValid)
            {
                bool deviceExists = await this.GetDeviceExistsAsync(model.DeviceId);

                model.IsDeviceIdUnique = !deviceExists;

                if (model.IsDeviceIdUnique)
                {
                    if (!onlyValidating)
                    {
                        return await this.Add(model);
                    }
                }
                else
                {
                    this.ModelState.AddModelError("DeviceId", Strings.DeviceIdInUse);
                }
            }

            return this.PartialView("_AddDeviceCreate", model);
        }

        [HttpPost]
        [RequirePermission(Permission.AddDevices)]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditDeviceProperties(EditDevicePropertiesModel model)
        {
            if (this.ModelState.IsValid)
            {
                try
                {
                    return await this.Edit(model);
                }
                catch (ValidationException exception)
                {
                    if (exception.Errors != null && exception.Errors.Any())
                    {
                        exception.Errors.ToList<string>().ForEach(error => this.ModelState.AddModelError(string.Empty, error));
                    }
                }
                catch (Exception)
                {
                    this.ModelState.AddModelError(string.Empty, Strings.DeviceUpdateError);
                }
            }

            return this.View("EditDeviceProperties", model);
        }

        private async Task<ActionResult> Add(UnregisteredDeviceModel model)
        {
            Debug.Assert(model != null, "model is a null reference.");
            Debug.Assert(
                model.DeviceType != null,
                "model.DeviceType is a null reference.");

            dynamic deviceWithKeys = await this.AddDeviceAsync(model);

            var newDevice = new RegisteredDeviceModel
            {
                HostName = _iotHubName,
                DeviceType = model.DeviceType,
                DeviceId = DeviceSchemaHelper.GetDeviceID(deviceWithKeys.Device),
                PrimaryKey = deviceWithKeys.SecurityKeys.PrimaryKey,
                SecondaryKey = deviceWithKeys.SecurityKeys.SecondaryKey,
                InstructionsUrl = model.DeviceType.InstructionsUrl
            };

            return this.PartialView("_AddDeviceCopy", newDevice);
        }

        [RequirePermission(Permission.EditDeviceMetadata)]
        public async Task<ActionResult> EditDeviceProperties(string deviceId)
        {
            EditDevicePropertiesModel model;
            IEnumerable<DevicePropertyValueModel> propValModels;

            model = new EditDevicePropertiesModel
            {
                DevicePropertyValueModels = new List<DevicePropertyValueModel>()
            };

            var device = await this._deviceLogic.GetDeviceAsync(deviceId);
            if (!object.ReferenceEquals(device, null))
            {
                model.DeviceId = DeviceSchemaHelper.GetDeviceID(device);

                propValModels = this._deviceLogic.ExtractDevicePropertyValuesModels(device);
                propValModels = ApplyDevicePropertyOrdering(propValModels);

                model.DevicePropertyValueModels.AddRange(propValModels);
            }

            return this.View("EditDeviceProperties", model);
        }

        private async Task<ActionResult> Edit(EditDevicePropertiesModel model)
        {
            if (model != null)
            {
                dynamic device = await this._deviceLogic.GetDeviceAsync(model.DeviceId);
                if (!object.ReferenceEquals(device, null))
                {
                    this._deviceLogic.ApplyDevicePropertyValueModels(
                        device,
                        model.DevicePropertyValueModels);

                    await this._deviceLogic.UpdateDeviceAsync(device);
                }
            }

            return this.RedirectToAction("Index");
        }

        [RequirePermission(Permission.ViewDevices)]
        public async Task<ActionResult> GetDeviceDetails(string deviceId)
        {
            IEnumerable<DevicePropertyValueModel> propModels;

            dynamic device = await this._deviceLogic.GetDeviceAsync(deviceId);
            if (object.ReferenceEquals(device, null))
            {
                throw new InvalidOperationException("Unable to load device with deviceId " + deviceId);
            }

            double version = 1.0;
            string strversion = DeviceSchemaHelper.GetDeviceVersion(device);
            if (!string.IsNullOrEmpty(strversion))
            {
                double.TryParse(strversion.Trim(), out version);
            }

            DeviceDetailModel deviceModel = new DeviceDetailModel
            {
                DeviceID = deviceId,
                HubEnabledState = DeviceSchemaHelper.GetHubEnabledState(device),
                DevicePropertyValueModels = new List<DevicePropertyValueModel>(),
                DeviceIsNewGeneration = version > Device_Version_1_0 ? true : false
            };

            propModels = this._deviceLogic.ExtractDevicePropertyValuesModels(device);
            propModels = ApplyDevicePropertyOrdering(propModels);

            deviceModel.DevicePropertyValueModels.AddRange(propModels);

            // check if value is cellular by checking iccid property
            deviceModel.IsCellular = device.SystemProperties.ICCID != null;
            deviceModel.Iccid = device.SystemProperties.ICCID; // todo: try get rid of null checks

            return this.PartialView("_DeviceDetails", deviceModel);
        }

        [RequirePermission(Permission.ViewDevices)]
        public ActionResult GetDeviceCellularDetails(string iccid)
        {
            var viewModel = new SimInformationViewModel();
            viewModel.TerminalDevice = this._cellularService.GetSingleTerminalDetails(new Iccid(iccid));
            viewModel.SessionInfo = this._cellularService.GetSingleSessionInfo(new Iccid(iccid)).LastOrDefault() ??
                                    new SessionInfo();

            return this.PartialView("_CellularInformation", viewModel);
        }

        [RequirePermission(Permission.ViewDeviceSecurityKeys)]
        public async Task<ActionResult> GetDeviceKeys(string deviceId)
        {
            var keys = await this._deviceLogic.GetIoTHubKeysAsync(deviceId);

            var keysModel = new SecurityKeysModel
            {
                PrimaryKey = keys != null ? keys.PrimaryKey : Strings.DeviceNotRegisteredInIoTHub,
                SecondaryKey = keys != null ? keys.SecondaryKey : Strings.DeviceNotRegisteredInIoTHub
            };

            return this.PartialView("_DeviceDetailsKeys", keysModel);
        }


        [RequirePermission(Permission.RemoveDevices)]
        public ActionResult RemoveDevice(string deviceId)
        {
            var device = new RegisteredDeviceModel
            {
                HostName = _iotHubName,
                DeviceId = deviceId
            };

            return this.View("RemoveDevice", device);
        }

        [HttpPost]
        [RequirePermission(Permission.RemoveDevices)]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteDevice(string deviceId)
        {
            await this._deviceLogic.RemoveDeviceAsync(deviceId);
            return this.View("Index");
        }

        private static IEnumerable<DevicePropertyValueModel> ApplyDevicePropertyOrdering(
            IEnumerable<DevicePropertyValueModel> devicePropertyModels)
        {
            Debug.Assert(
                devicePropertyModels != null,
                "devicePropertyModels is a null reference.");

            return devicePropertyModels.OrderByDescending(
                t => DeviceDisplayHelper.GetIsCopyControlPropertyName(
                    t.Name)).ThenBy(u => u.DisplayOrder).ThenBy(
                        v => v.Name);
        }

        private async Task<dynamic> AddDeviceAsync(UnregisteredDeviceModel unregisteredDeviceModel)
        {
            dynamic device;

            Debug.Assert(
                unregisteredDeviceModel != null,
                "unregisteredDeviceModel is a null reference.");

            Debug.Assert(
                unregisteredDeviceModel.DeviceType != null,
                "unregisteredDeviceModel.DeviceType is a null reference.");

            device = DeviceSchemaHelper.BuildDeviceStructure(unregisteredDeviceModel.DeviceId,
                unregisteredDeviceModel.DeviceType.IsSimulatedDevice, unregisteredDeviceModel.Iccid);

            return await this._deviceLogic.AddDeviceAsync(device);
        }

        private async Task<bool> GetDeviceExistsAsync(string deviceId)
        {
            dynamic existingDevice;

            existingDevice = await this._deviceLogic.GetDeviceAsync(deviceId);

            return !object.ReferenceEquals(existingDevice, null);
        }

        private async Task<List<dynamic>> GetDevices()
        {
            var query = new DeviceListQuery
            {
                Take = 1000
            };

            var devices = await this._deviceLogic.GetDevices(query);
            return devices.Results;
        }

    }
}
