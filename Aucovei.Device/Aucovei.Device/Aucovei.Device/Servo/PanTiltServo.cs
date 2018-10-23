using System;
using System.Threading.Tasks;
using Microsoft.IoT.DeviceCore.Pwm;
using Microsoft.IoT.Devices.Pwm;
using Windows.Devices.Pwm;

namespace Aucovei.Device
{
    public class PanTiltServo
    {
        private PwmController pwmController;
        private PwmPin tiltServo;
        private PwmPin panServo;
        double RestingPulseLegnth = 0;
        private const int panMotorPin = 24;
        private const int TiltMotorPin = 25;
        private const double IncrementFactor = 10.0;

        double panAngle = 90.00;
        double tiltAngle = 90.00;

        public PanTiltServo()
        {
            Task.Run(async () =>
            {
                await this.init();
            }).Wait();
        }

        private async Task init()
        {
            // ENSURE ITS DONE BEFORE PWM
            // LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();

            // await PwmController.GetControllersAsyn DOES NOT WORK ON RPi3B+ in preview builds but works on other models
            // var pwmControllers = await PwmController.GetControllersAsync(LightningPwmProvider.GetPwmProvider());

            // HENCE USING PwmProviderManager
            var pwmManager = new PwmProviderManager();

            // Add providers
            pwmManager.Providers.Add(new SoftPwm());
            // Get the well-known controller collection back
            var pwmControllers = await pwmManager.GetControllersAsync();

            // Using the first PWM controller
            this.pwmController = pwmControllers[0];
            this.pwmController.SetDesiredFrequency(50);

            this.panServo = this.pwmController.OpenPin(panMotorPin);
            this.tiltServo = this.pwmController.OpenPin(TiltMotorPin);

            this.panServo.SetActiveDutyCyclePercentage(this.RestingPulseLegnth);
            this.panServo.Start();
            this.tiltServo.SetActiveDutyCyclePercentage(this.RestingPulseLegnth);
            this.tiltServo.Start();

            Task.Run(() =>
            {
                System.Threading.Tasks.Task.Delay(250).Wait();
                this.tiltServo.Stop();
                this.panServo.Stop();
            });
        }

        public async Task ExecuteCommand(string command)
        {
            switch (command)
            {
                case Commands.TiltUpValue:
                    await this.TiltUp().ConfigureAwait(false);
                    return;
                case Commands.TiltDownValue:
                    await this.TiltDown().ConfigureAwait(false);
                    return;
                case Commands.PanLeftValue:
                    await this.PanLeft().ConfigureAwait(false);
                    return;
                case Commands.PanRightValue:
                    await this.PanRight().ConfigureAwait(false);
                    return;
                case Commands.PanTiltCenterValue:
                    await this.Center().ConfigureAwait(false);
                    return;
            }
        }

        public async Task TiltDown()
        {
            this.tiltAngle += IncrementFactor;
            if (this.tiltAngle >= 180)
            {
                this.tiltAngle = 180.0;
            }

            var dutyCycle = (this.tiltAngle / 18 + 3) / 100.0;
            this.tiltServo.SetActiveDutyCyclePercentage(dutyCycle);
            this.tiltServo.Start();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            this.tiltServo.Stop();
        }

        public async Task TiltUp()
        {
            this.tiltAngle -= IncrementFactor;
            if (this.tiltAngle <= 0)
            {
                this.tiltAngle = 0;
            }

            var dutyCycle = (this.tiltAngle / 18 + 3) / 100.0;
            this.tiltServo.SetActiveDutyCyclePercentage(dutyCycle);
            this.tiltServo.Start();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            this.tiltServo.Stop();
        }

        public async Task PanLeft()
        {
            this.panAngle += IncrementFactor;
            if (this.panAngle >= 180)
            {
                this.panAngle = 180.0;
            }

            var dutyCycle = (this.panAngle / 18 + 3) / 100.0;
            this.panServo.SetActiveDutyCyclePercentage(dutyCycle);
            this.panServo.Start();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            this.panServo.Stop();
        }

        public async Task PanRight()
        {
            this.panAngle -= IncrementFactor;
            if (this.panAngle <= 0)
            {
                this.panAngle = 0;
            }

            var dutyCycle = (this.panAngle / 18 + 3) / 100.0;
            this.panServo.SetActiveDutyCyclePercentage(dutyCycle);
            this.panServo.Start();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            this.panServo.Stop();
        }

        public async Task Center()
        {
            this.tiltAngle = 90.0;
            this.panAngle = 90.0;

            var dutyCycle = (this.tiltAngle / 18 + 3) / 100.0;
            this.tiltServo.SetActiveDutyCyclePercentage(dutyCycle);
            this.tiltServo.Start();
            this.panServo.SetActiveDutyCyclePercentage(dutyCycle);
            this.panServo.Start();
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            this.tiltServo.Stop();
            this.panServo.Stop();
        }
    }
}