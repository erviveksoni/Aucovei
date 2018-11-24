//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using aucovei.uwp.Helpers;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace aucovei.uwp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public static MainPage Current;

        //
        // The Client ID is used by the application to uniquely identify itself to Azure AD.
        // The Tenant is the name of the Azure AD tenant in which this application is registered.
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Authority is the sign-in URL of the tenant.
        //
        const string tenant = "e57a8383-f9e1-457e-bb36-96a141f26049";
        const string clientId = "4e187e7e-bfb1-4b84-b301-21223e2c0637";
        const string aadInstance = "https://login.microsoftonline.com/{0}";
        private CancellationTokenSource tokenSrc;

        static string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);
        //
        // To authenticate to the To Do list service, the client needs to know the service's App ID URI.
        // To contact the To Do list service we need it's URL as well.
        //
        const string aucoveiResourceId = "https://aucoveidemo.azurewebsites.net/iotsuite";

        private Uri redirectURI = null;

        public MainPage()
        {
            this.InitializeComponent();

            // This is a static public property that allows downstream pages to get a handle to the MainPage instance
            // in order to call methods that are in this class.
            Current = this;
            this.SampleTitle.Text = FEATURE_NAME;
            App.AppCommandBar = this.AppCommandBar;
            App.AppData.PropertyChanged += this.AppDataPropertyChanged;
            this.redirectURI = Windows.Security.Authentication.Web.WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
            App.AppData.AuthContext = new AuthenticationContext(authority);
            this.FooterPanel.Visibility = Visibility.Collapsed;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await this.AuthenticateUser();

            if (App.AppData.AuthResult == null) { return; }

            // Populate the scenario list from the SampleConfiguration.cs file
            this.ScenarioControl.ItemsSource = this.scenarios;
            if (Window.Current.Bounds.Width < 640)
            {
                this.ScenarioControl.SelectedIndex = -1;
            }
            else
            {
                this.ScenarioControl.SelectedIndex = 0;
            }
        }

        private async Task AuthenticateUser()
        {
            try
            {
                App.AppData.AuthResult = await App.AppData.AuthContext.AcquireTokenAsync(aucoveiResourceId, clientId, this.redirectURI,
                    new PlatformParameters(PromptBehavior.Auto, false));
                this.LoginName.Text = $"{App.AppData.AuthResult.UserInfo.GivenName} {App.AppData.AuthResult.UserInfo.FamilyName}";
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == "authentication_canceled")
                {
                    MessageDialog dialog = new MessageDialog("Sing-in operation cancelled by user.");
                    this.ShowError(dialog);
                }
                else
                {
                    MessageDialog dialog =
                        new MessageDialog(
                            string.Format(
                                "If the error continues, please contact your administrator.\n\nError Description:\n\n{0}",
                                ex.Message), "Sorry, an error occurred while signing you in.");
                    this.ShowError(dialog);
                }

                return;
            }
        }

        /// <summary>
        /// Called whenever the user changes selection in the scenarios list.  This method will navigate to the respective
        /// sample scenario page.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScenarioControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear the status block when navigating scenarios.
            this.NotifyUser(String.Empty, NotifyType.StatusMessage);

            ListBox scenarioListBox = sender as ListBox;
            Scenario s = scenarioListBox.SelectedItem as Scenario;
            if (s != null)
            {
                this.ScenarioFrame.Navigate(s.ClassType);
                if (Window.Current.Bounds.Width < 640)
                {
                    this.Splitter.IsPaneOpen = false;
                }
            }
        }

        public List<Scenario> Scenarios => this.scenarios;

        /// <summary>
        /// Used to display messages to the user
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    this.StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    this.StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            this.StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            this.StatusBorder.Visibility = (this.StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (this.StatusBlock.Text != String.Empty)
            {
                this.StatusBorder.Visibility = Visibility.Visible;
                this.StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                this.StatusBorder.Visibility = Visibility.Collapsed;
                this.StatusPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void ProgressBar(bool show)
        {
            if (show)
            {
                this.progressBar.Visibility = Visibility.Visible;
            }
            else
            {
                this.progressBar.Visibility = Visibility.Collapsed;
            }
        }

        public async void ShowError(MessageDialog dialog)
        {
            await dialog.ShowAsync();
        }

        async void Footer_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(((HyperlinkButton)sender).Tag.ToString()));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Splitter.IsPaneOpen = !this.Splitter.IsPaneOpen;
        }

        public void UpdateNavigation(int index)
        {
            this.ScenarioControl.SelectedIndex = index;
        }

        private void AppDataPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName.Equals(nameof(App.AppData.IsConnected)))
                {
                    if (App.AppData.IsConnected)
                    {
                        this.tokenSrc = new CancellationTokenSource();
                        this.GetDeviceDelemetryAsync();
                    }
                    else
                    {
                        this.tokenSrc.Cancel();
                    }
                }
                else if (e.PropertyName.Equals(nameof(App.AppData.IsObstacleDetected)))
                {
                    if (App.AppData.IsObstacleDetected)
                    {
                        this.HeaderPanelSub.Background = this.HeaderPanel.Background = new SolidColorBrush(Colors.Red);

                        this.PopObstacleToast();
                    }
                    else
                    {
                        this.HeaderPanelSub.Background = this.HeaderPanel.Background =
                            Application.Current.Resources["SystemControlBackgroundChromeMediumBrush"] as SolidColorBrush;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageDialog dialog = new MessageDialog("An error has occured. Details: " + ex.Message);
                this.ShowError(dialog);
            }
        }

        private async void GetDeviceDelemetryAsync()
        {
            var svcHelper = new ServiceHelper();

            while (!this.tokenSrc.IsCancellationRequested)
            {
                try
                {
                    dynamic result = await svcHelper.GetDeviceTelemetryAsync(App.AppData.ConnectedAucovei.Id);
                    JArray telemetry = JArray.Parse(result?.ToString() ?? string.Empty);
                    if (telemetry.Count > 0)
                    {
                        var lastrecord = telemetry.Last;
                        if (lastrecord["boolValues"] != null)
                        {
                            if (lastrecord["boolValues"]["cameraStatus"] != null)
                            {
                                var camStatus = lastrecord["boolValues"]["cameraStatus"].ToObject<bool>();
                                if (camStatus)
                                {
                                    if (!App.AppData.IsVideoFeedActive)
                                    {
                                        App.AppData.IsVideoFeedActive = true;
                                    }
                                }
                                else
                                {
                                    if (App.AppData.IsVideoFeedActive)
                                    {
                                        App.AppData.IsVideoFeedActive = false;
                                    }
                                }
                            }

                            if (lastrecord["boolValues"]["isObstacleDetected"] != null)
                            {
                                var obstacleDetected = lastrecord["boolValues"]["isObstacleDetected"].ToObject<bool>();
                                if (obstacleDetected)
                                {
                                    if (!App.AppData.IsObstacleDetected)
                                    {
                                        App.AppData.IsObstacleDetected = true;
                                    }
                                }
                                else
                                {
                                    if (App.AppData.IsObstacleDetected)
                                    {
                                        App.AppData.IsObstacleDetected = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (App.AppData.IsVideoFeedActive)
                            {
                                App.AppData.IsVideoFeedActive = false;
                            }

                            if (App.AppData.IsObstacleDetected)
                            {
                                App.AppData.IsObstacleDetected = false;
                            }
                        }
                    }
                    else
                    {
                        if (App.AppData.IsVideoFeedActive)
                        {
                            App.AppData.IsVideoFeedActive = false;
                        }

                        if (App.AppData.IsObstacleDetected)
                        {
                            App.AppData.IsObstacleDetected = false;
                        }
                    }
                }
                catch
                {
                    //do nothing
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }

            if (App.AppData.IsVideoFeedActive)
            {
                App.AppData.IsVideoFeedActive = false;
            }

            if (App.AppData.IsObstacleDetected)
            {
                App.AppData.IsObstacleDetected = false;
            }
        }

        private void PopObstacleToast()
        {
            // In a real app, these would be initialized with actual data
            string title = "Roadblock Alert";
            string content = "Possible roadblock ahead!";
            string description = "Please check the app for more information.";
            string logo = "ms-appx:///Assets/warningsign.png";

            // Construct the visuals of the toast
            ToastVisual visual = new ToastVisual()
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children =
                    {
                        new AdaptiveText()
                        {
                            Text = title
                        },

                        new AdaptiveText()
                        {
                            Text = content,
                        },

                        new AdaptiveText()
                        {
                            Text = description
                        }
                    },

                    AppLogoOverride = new ToastGenericAppLogo()
                    {
                        Source = logo,
                        HintCrop = ToastGenericAppLogoCrop.Default
                    }
                }
            };

            ToastContent toastContent = new ToastContent()
            {
                Scenario = ToastScenario.Alarm,
                Visual = visual,
            };

            // And create the toast notification
            var toast = new ToastNotification(toastContent.GetXml());
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };

    public class ScenarioBindingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Scenario s = value as Scenario;
            if (s.ClassType == typeof(AddWaypoints) || s.ClassType == typeof(ManualMode))
            {
                return " ➡️ " + s.Title;
            }
            else if (s.ClassType == typeof(SendWaypoints))
            {
                return "    ↪️ " + s.Title;
            }
            else
            {
                return s.Title;
                // return (MainPage.Current.Scenarios.IndexOf(s) + 1) + ") " + s.Title;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}
