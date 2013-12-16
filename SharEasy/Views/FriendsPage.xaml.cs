using SharEasy.Common;
using SharEasy.ViewModels;
using System;
using System.Diagnostics;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// The Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234233

namespace SharEasy.Views {

    /// <summary>
    /// A page that displays a collection of item previews.  In the Split Application this page
    /// is used to display and select one of the available groups.
    /// </summary>
    public sealed partial class FriendsPage : Page {
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();
        private bool firstLaunch = true;
        private bool showingAllFriends = true;

        /// <summary>
        /// This can be changed to a strongly typed view model.
        /// </summary>
        public ObservableDictionary DefaultViewModel {
            get { return this.defaultViewModel; }
        }

        /// <summary>
        /// NavigationHelper is used on each page to aid in navigation and
        /// process lifetime management
        /// </summary>
        public NavigationHelper NavigationHelper {
            get { return this.navigationHelper; }
        }

        public FriendsPage() {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
        }

        #region NavigationHelper registration

        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        ///
        /// Page specific logic should be placed in event handlers for the
        /// <see cref="GridCS.Common.NavigationHelper.LoadState"/>
        /// and <see cref="GridCS.Common.NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method
        /// in addition to page state preserved during an earlier session.

        protected override void OnNavigatedTo(NavigationEventArgs e) {
            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e) {
            navigationHelper.OnNavigatedFrom(e);
        }

        #endregion NavigationHelper registration

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session.  The state will be null the first time a page is visited.</param>
        ///

        private void navigationHelper_LoadState(object sender, LoadStateEventArgs e) {
            // TODO: Assign a bindable collection of items to this.DefaultViewModel["Items"]
            //if (firstLaunch) {
            //    firstLaunch = false;
            //this.Frame.SetNavigationState(App.cleanNavigationState);
            preparePage();
            //}
        }

        //protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
        //    base.OnNavigatingFrom(e);
        //    fbData.Logout();
        //    fbData = null;
        //}

        // http://facebooksdk.net/docs/windows/tutorial/

        private void preparePage() {
            App.DataClient.FetchUserData(this);
        }

        public void SetUserInfo() {
            this.MyImage.Source = App.DataClient.GetProfilePic();
            this.MyImage.Opacity = 1;
            this.pageTitle.Text = "SharEasy - " + App.DataClient.GetUsername();
        }

        private async void shareButton_Click(object sender, RoutedEventArgs e) {
            await App.DataClient.PickFile();
            if (App.DataClient.UserChoseAFile()) {
                FileNameTextBox.Text = App.DataClient.GetChosenFileName();
                showSharingPopup();
            } else {
                var dialog = new MessageDialog("Cancelled sharing.");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private void refreshButton_Click(object sender, RoutedEventArgs e) {
            App.DataClient.RefreshItems();
        }

        private void logoutButton_Click(object sender, RoutedEventArgs e) {
            this.DefaultViewModel["Friends"] = null;
            this.pageTitle.Text = "SharEasy";
            this.MyImage.Opacity = 0.0;
            App.DataClient.Logout();
            App.DataClient = null;
            this.Frame.Navigate(typeof(LoadingPage));
        }

        private void showSharingPopup() {
            ShadowRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
            SharingPopup.IsOpen = true;
        }

        private void hideSharingPopup() {
            SharingPopup.IsOpen = false;
            ShadowRectangle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void PopupShareButton_Click(object sender, RoutedEventArgs e) {
            if (!String.IsNullOrWhiteSpace(DescriptionTextBox.Text)) {
                if (DescriptionTextBox.Text.Length <= 200) {
                    App.DataClient.ShareItem(DescriptionTextBox.Text);
                    hideSharingPopup();
                    DescriptionTextBox.Text = String.Empty;
                    App.DataClient.RefreshItems();
                } else {
                    MessageDialog dialog = new MessageDialog("Description must be less than 200 characters.");
                    dialog.Commands.Add(new UICommand("Ok"));
                    dialog.ShowAsync();
                }
            } else {
                MessageDialog dialog = new MessageDialog("You must write a description to your shared item.");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private void PopupCancelButton_Click(object sender, RoutedEventArgs e) {
            hideSharingPopup();
        }

        private void logoutButton_PointerEntered(object sender, PointerRoutedEventArgs e) {
            LogoutButtonText.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        private void logoutButton_PointerExited(object sender, PointerRoutedEventArgs e) {
            LogoutButtonText.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void showFriendsButton_Click(object sender, RoutedEventArgs e) {
            if (App.DataClient.friendsListIsLoaded()) {
                if (showingAllFriends) {
                    if (App.DataClient.ShowSharingFriends()) {
                        showingAllFriends = false;
                        showFriendsButton.Content = "Show: All friends";
                    } else {
                        MessageDialog dialog = new MessageDialog("Shared items are not yet loaded, please try again in a few seconds.");
                        dialog.Commands.Add(new UICommand("Ok"));
                        dialog.ShowAsync();
                    }
                } else {
                    if (App.DataClient.ShowAllFriends()) {
                        showingAllFriends = true;
                        showFriendsButton.Content = "Show: Friends who've shared";
                    } else {
                        MessageDialog dialog = new MessageDialog("List of friends isn't yet loaded, please try again in a few seconds.");
                        dialog.Commands.Add(new UICommand("Ok"));
                        dialog.ShowAsync();
                    }
                }
            }
        }

        // User detail event
        private void Friend_Tapped(object sender, TappedRoutedEventArgs e) {
            Grid grid = sender as Grid;
            Friend friend = grid.DataContext as Friend;
            ShowUserDetailsPopup(friend.facebookUserID);
        }

        // User detail event
        private async void mySharedItemsButton_Click(object sender, RoutedEventArgs e) {
            if (App.DataClient.UploadingFiles()) {
                MessageDialog dialog = new MessageDialog("Would you like to view your shared items or pending uploads?");
                dialog.Commands.Add(new UICommand("Shared Items", new UICommandInvokedHandler((cmd) => ShowUserDetailsPopup(App.DataClient.GetMyFacebookUserID()))));
                dialog.Commands.Add(new UICommand("Pending Uploads", new UICommandInvokedHandler((cmd) => ShowPendingUploadsPopup())));
                dialog.Commands.Add(new UICommand("Cancel"));
                await dialog.ShowAsync();
            } else {
                ShowUserDetailsPopup(App.DataClient.GetMyFacebookUserID());
            }
        }

        private void ShowPendingUploadsPopup() {
            this.DefaultViewModel["PendingUploads"] = App.DataClient.GetPendingUploads();
            ShadowRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
            PendingUploadsPopup.IsOpen = true;
        }

        public void HidePendingUploadsPopup() {
            PendingUploadsPopup.IsOpen = false;
            ShadowRectangle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void ShowUserDetailsPopup(string facebookUserID) {
            if (App.DataClient.friendsListIsLoaded() && App.DataClient.itemsAreLoaded()) {
                UserItemsPopupNameTextBlock.Text = App.DataClient.GetNameOfUser(facebookUserID);
                UserItemsPopupProfilePicture.Source = App.DataClient.GetProfilePicOfUser(facebookUserID);
                this.DefaultViewModel["UserItems"] = App.DataClient.GetItemsOfUser(facebookUserID);
                if (App.DataClient.GetItemsOfUser(facebookUserID).Count == 0) {
                    NoSharedItemsTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                ShadowRectangle.Visibility = Windows.UI.Xaml.Visibility.Visible;
                UserItemsPopup.IsOpen = true;
            }
        }

        private void HideUserDetailsPopup() {
            UserItemsPopup.IsOpen = false;
            NoSharedItemsTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            ShadowRectangle.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void UserItemsPopupCloseButton_Click(object sender, RoutedEventArgs e) {
            HideUserDetailsPopup();
        }

        private async void SharedItem_Tapped(object sender, TappedRoutedEventArgs e) {
            try {
                Grid grid = sender as Grid;
                SharedItemsListElement item = grid.DataContext as SharedItemsListElement;
                if (item.UserID == App.DataClient.GetMyFacebookUserID()) {
                    MessageDialog dialog = new MessageDialog("Would you like to view your item or delete it?");
                    dialog.Commands.Add(new UICommand("View", new UICommandInvokedHandler((cmd) => LaunchURL(item.URL))));
                    dialog.Commands.Add(new UICommand("Delete", new UICommandInvokedHandler((cmd) => DeleteAndClosePopup(item))));
                    dialog.Commands.Add(new UICommand("Cancel"));
                    await dialog.ShowAsync();
                } else {
                    MessageDialog dialog = new MessageDialog("Would you like to view this item?");
                    dialog.Commands.Add(new UICommand("View", new UICommandInvokedHandler((cmd) => LaunchURL(item.URL))));
                    dialog.Commands.Add(new UICommand("Cancel"));
                    await dialog.ShowAsync();
                }

            } catch (Exception) {
                MessageDialog dialog = new MessageDialog("Error occurred when getting shared item!");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private async void DeleteAndClosePopup(SharedItemsListElement item) {
            App.DataClient.DeleteItem(item.SharedItem);
            HideUserDetailsPopup();
        }

        private void LaunchURL(string urlToLaunch) {
            Uri uri = new Uri(ProcessURL(urlToLaunch));
            Launcher.LaunchUriAsync(uri);
        }

        private string ProcessURL(string url) {
            if (!url.StartsWith("http")) {
                return "http://" + url;
            }
            return url;
        }

        private void UploadsPopupCloseButton_Tapped(object sender, TappedRoutedEventArgs e) {
            HidePendingUploadsPopup();
        }

        private async void PendingUploadsGrid_Tapped(object sender, TappedRoutedEventArgs e) {
            try {
                Grid grid = sender as Grid;
                Upload upload = grid.DataContext as Upload;
                MessageDialog dialog = new MessageDialog("Would you like to cancel this upload?");
                dialog.Commands.Add(new UICommand("Yes, cancel upload", new UICommandInvokedHandler(async (cmd) => await App.DataClient.CancelUpload(upload))));
                dialog.Commands.Add(new UICommand("No, continue uploading"));
                await dialog.ShowAsync();
            } catch (Exception) {
                MessageDialog dialog = new MessageDialog("Error occurred when accessing upload!");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }
    }
}
