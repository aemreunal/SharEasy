using SharEasy.Common;
using SharEasy.ViewModels;
using System;
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

        private void shareButton_Click(object sender, RoutedEventArgs e) {
            //App.DataClient.
            showSharingPopup();
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

        private async void Share(String description) {
            await App.DataClient.ShareItem(description);
        }

        private void PopupShareButton_Click(object sender, RoutedEventArgs e) {
            if (!String.IsNullOrWhiteSpace(DescriptionTextBox.Text)) {
                if (DescriptionTextBox.Text.Length <= 140) {
                    Share(DescriptionTextBox.Text);
                    DescriptionTextBox.Text = String.Empty;
                    hideSharingPopup();
                    App.DataClient.RefreshItems();
                } else {
                    MessageDialog dialog = new MessageDialog("Description must be less than 140 characters.");
                    dialog.Commands.Add(new UICommand("Ok"));
                    showDialog(dialog);
                }
            } else {
                MessageDialog dialog = new MessageDialog("You must write a description to your shared item.");
                dialog.Commands.Add(new UICommand("Ok"));
                showDialog(dialog);
            }
        }

        private async void showDialog(MessageDialog dialog) {
            dialog.ShowAsync();
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
                        showDialog(dialog);
                    }
                } else {
                    if (App.DataClient.ShowAllFriends()) {
                        showingAllFriends = true;
                        showFriendsButton.Content = "Show: Friends who've shared";
                    } else {
                        MessageDialog dialog = new MessageDialog("List of friends isn't yet loaded, please try again in a few seconds.");
                        dialog.Commands.Add(new UICommand("Ok"));
                        showDialog(dialog);
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
        private void mySharedItemsButton_Click(object sender, RoutedEventArgs e) {
            ShowUserDetailsPopup(App.DataClient.GetMyFacebookUserID());
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

        private void SharedItem_Tapped(object sender, TappedRoutedEventArgs e) {
            try {
                Grid grid = sender as Grid;
                SharedItemsListElement item = grid.DataContext as SharedItemsListElement;
                Uri uri = new Uri(ProcessURL(item.URL));
                Launcher.LaunchUriAsync(uri);
            } catch (Exception) {
                MessageDialog dialog = new MessageDialog("Error occurred when jumping to shared item URL!");
                dialog.Commands.Add(new UICommand("Ok"));
                showDialog(dialog);
            }
        }

        private string ProcessURL(string url) {
            if (!url.StartsWith("http")) {
                return "http://" + url;
            }
            return url;
        }
    }
}
