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

using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
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
            SampleTitle.Text = FEATURE_NAME;
            App.AppCommandBar = this.AppCommandBar;

            redirectURI = Windows.Security.Authentication.Web.WebAuthenticationBroker.GetCurrentApplicationCallbackUri();
            App.AuthContext = new AuthenticationContext(authority);
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await AuthenticateUser();

            if (App.AuthResult == null) { return; }

            // Populate the scenario list from the SampleConfiguration.cs file
            ScenarioControl.ItemsSource = scenarios;
            if (Window.Current.Bounds.Width < 640)
            {
                ScenarioControl.SelectedIndex = -1;
            }
            else
            {
                ScenarioControl.SelectedIndex = 0;
            }
        }

        private async Task AuthenticateUser()
        {
            try
            {
                App.AuthResult = await App.AuthContext.AcquireTokenAsync(aucoveiResourceId, clientId, redirectURI,
                    new PlatformParameters(PromptBehavior.Auto, false));
                this.LoginName.Text = $"{App.AuthResult.UserInfo.GivenName} {App.AuthResult.UserInfo.FamilyName}";
            }
            catch (AdalException ex)
            {
                if (ex.ErrorCode == "authentication_canceled")
                {
                    MessageDialog dialog = new MessageDialog("Sing-in operation cancelled by user.");
                    ShowError(dialog);
                }
                else
                {
                    MessageDialog dialog =
                        new MessageDialog(
                            string.Format(
                                "If the error continues, please contact your administrator.\n\nError Description:\n\n{0}",
                                ex.Message), "Sorry, an error occurred while signing you in.");
                    ShowError(dialog);
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
            NotifyUser(String.Empty, NotifyType.StatusMessage);

            ListBox scenarioListBox = sender as ListBox;
            Scenario s = scenarioListBox.SelectedItem as Scenario;
            if (s != null)
            {
                ScenarioFrame.Navigate(s.ClassType);
                if (Window.Current.Bounds.Width < 640)
                {
                    Splitter.IsPaneOpen = false;
                }
            }
        }

        public List<Scenario> Scenarios
        {
            get { return this.scenarios; }
        }

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
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
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
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }

        public void UpdateNavigation(int index)
        {
            this.ScenarioControl.SelectedIndex = index;
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
            return (MainPage.Current.Scenarios.IndexOf(s) + 1) + ") " + s.Title;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return true;
        }
    }
}
