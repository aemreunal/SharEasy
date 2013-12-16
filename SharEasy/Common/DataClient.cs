using Facebook;
using Facebook.Client;
using Microsoft.Live;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using SharEasy.Common;
using SharEasy.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace SharEasy.ViewModels {

    public class DataClient {
        private FriendsPage friendsPage;

        private FacebookSessionClient FacebookSessionClient;
        private FacebookSession FacebookSession;

        private MobileServiceClient MobileServiceClient;
        private MobileServiceUser MobileServiceUser;

        private LiveAuthClient LiveAuthClient;
        private LiveConnectClient LiveConnectClient;
        private LiveConnectSession LiveSession;

        private string AccessToken = String.Empty;
        private string FacebookId = String.Empty;

        private bool fbLoggedIn = false;
        private bool msLoggedIn = false;
        private bool sdLoggedIn = false;
        private bool friendsListLoaded = false;
        private bool itemsLoaded = false;

        private ObservableCollection<Friend> friends = new ObservableCollection<Friend>();
        private IMobileServiceTable<SharedItem> sharedItemsTable;
        private Dictionary<string, List<SharedItem>> itemsByFriends = new Dictionary<string, List<SharedItem>>();
        private Dictionary<string, bool> friendShareStatus = new Dictionary<string, bool>();

        private BitmapImage userProfilePicture;
        private String username;

        public async Task Login(TextBlock LoadingTextBlock) {
            if (!fbLoggedIn) {
                try {
                    LoadingTextBlock.Text = "Connecting to Facebook";
                    await AuthenticateFacebook();
                    LoadingTextBlock.Text = "Connecting to SkyDrive";
                    await AuthenticateSkyDrive();
                    LoadingTextBlock.Text = "Connecting to Azure";
                    await AuthenticateMobileService();
                    LoadingTextBlock.Text = "Welcome";
                } catch (HttpRequestException) {
                    MessageDialog dialog = new MessageDialog("No network connection - unable to connect to Facebook.");
                    dialog.Commands.Add(new UICommand("Ok", new UICommandInvokedHandler((cmd) => App.Current.Exit())));
                    dialog.ShowAsync();
                }
            }
        }

        public async void FetchUserData(FriendsPage friendsPage) {
            this.friendsPage = friendsPage;
            await FetchUserInfo();
            await FetchFriends();
            await RefreshItems();
        }

        public void Logout() {
            while (msLoggedIn || fbLoggedIn) {
                try {
                    MobileServiceClient.Logout();
                    clearMSData();
                    FacebookSessionClient.Logout();
                    clearFBData();
                    LiveAuthClient.Logout();
                    clearLiveData();
                } catch (InvalidOperationException) {
                    MessageDialog dialog = new MessageDialog("Error when logging out!");
                    dialog.Commands.Add(new UICommand("Ok"));
                    dialog.ShowAsync();
                }
            }
        }

        private void clearMSData() {
            msLoggedIn = false;
            MobileServiceClient = null;
            MobileServiceUser = null;
            itemsByFriends = null;
            itemsLoaded = false;
        }

        private void clearFBData() {
            fbLoggedIn = false;
            FacebookSessionClient = null;
            FacebookSession = null;
            AccessToken = String.Empty;
            FacebookId = String.Empty;
            friends = null;
            username = String.Empty;
            userProfilePicture = null;
        }

        private void clearLiveData() {
            sdLoggedIn = false;
            LiveAuthClient = null;
        }

        private async Task AuthenticateFacebook() {
            try {
                FacebookSessionClient = new FacebookSessionClient(Constants.FacebookAppId);
                FacebookSession = await FacebookSessionClient.LoginAsync("user_about_me,user_friends");
                AccessToken = FacebookSession.AccessToken;
                FacebookId = FacebookSession.FacebookId;
                fbLoggedIn = true;
                Debug.WriteLine("Facebook login succeeded, facebook ID: " + FacebookId);
            } catch (InvalidOperationException e) {
                MessageDialog dialog = new MessageDialog("Login failed! Exception details: " + e.Message);
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private async Task AuthenticateSkyDrive() {
            try {
                if (!sdLoggedIn) {
                    LiveAuthClient = new LiveAuthClient();
                    LiveLoginResult authResult = await LiveAuthClient.LoginAsync(new List<string>() { "wl.signin", "wl.skydrive", "wl.skydrive_update", "wl.offline_access", "wl.contacts_skydrive" });
                    if (authResult.Status == LiveConnectSessionStatus.Connected) {
                        LiveSession = authResult.Session;
                        LiveConnectClient = new LiveConnectClient(LiveSession);
                        // Test
                        //LiveOperationResult liveOpResult = await LiveConnectClient.GetAsync("me");
                        //dynamic dynResult = liveOpResult.Result;
                        //if (dynResult != null) {
                        //    Debug.WriteLine("Hello, " + string.Join(" ", "Hello,", dynResult.name, "!"));
                        //}
                        // Test
                        Debug.WriteLine("Connected to Live.");
                    }
                }
            } catch (InvalidOperationException e) {
                MessageDialog dialog = new MessageDialog("Login failed! Exception details: " + e.Message);
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        //private async void LoadProfile() {
        //    LiveConnectClient client = new LiveConnectClient(App.LiveSession);
        //    LiveOperationResult liveOpResult = await client.GetAsync("me");
        //    dynamic dynResult = liveOpResult.Result;
        //    App.LiveUserName = dynResult.name;
        //    //LoadData();
        //}

        private async Task AuthenticateMobileService() {
            MobileServiceAuthenticationProvider provider = MobileServiceAuthenticationProvider.Facebook;
            while (MobileServiceUser == null) {
                try {
                    MobileServiceClient = new MobileServiceClient(Constants.MobileServiceAppURL, Constants.MobileServiceAppKey);
                    MobileServiceUser = await MobileServiceClient.LoginAsync(provider, JObject.Parse("{\"access_token\":\"" + AccessToken + "\"}"));
                    //message = string.Format("You are now logged in - {0}", user.UserId);
                    msLoggedIn = true;
                    sharedItemsTable = MobileServiceClient.GetTable<SharedItem>();
                    Debug.WriteLine("Azure Mobile Services login succeeded, user ID: " + MobileServiceUser.UserId);
                } catch (InvalidOperationException) {
                    var dialog = new MessageDialog("Error when logging in to Azure Mobile Services!");
                    dialog.Commands.Add(new UICommand("Ok"));
                    dialog.ShowAsync();
                }
            }
        }

        private async Task FetchUserInfo() {
            FacebookClient fb = new FacebookClient(AccessToken);

            dynamic parameters = new ExpandoObject();
            parameters.access_token = AccessToken;
            parameters.fields = "name, id";

            dynamic result = await fb.GetTaskAsync("me", parameters);

            string profilePictureUrl = string.Format("https://graph.facebook.com/{0}/picture?type={1}&access_token={2}", FacebookId, "large", fb.AccessToken);

            userProfilePicture = new BitmapImage(new Uri(profilePictureUrl));
            username = result.name;
            friendsPage.SetUserInfo();
        }

        private async Task FetchFriends() {
            FacebookClient fb = new FacebookClient(AccessToken);

            dynamic friendsTaskResult = await fb.GetTaskAsync("/me/friends");
            var result = (IDictionary<string, object>)friendsTaskResult;
            var data = (IEnumerable<object>)result["data"];
            foreach (var item in data) {
                var friend = (IDictionary<string, object>)item;
                friends.Add(new Friend {
                    Name = (string)friend["name"],
                    facebookUserID = (string)friend["id"],
                    PictureUri = new Uri(string.Format("https://graph.facebook.com/{0}/picture?type={1}&access_token={2}", (string)friend["id"], "large", AccessToken))
                });
            }
            friendsListLoaded = true;
            ShowAllFriends();
        }

        public bool ShowAllFriends() {
            if (friendsListLoaded) {
                friendsPage.DefaultViewModel["Friends"] = friends.OrderBy(x => x.Name);
                return true;
            }
            return false;
        }

        public bool ShowSharingFriends() {
            if (friendsListLoaded && itemsLoaded) {
                friendsPage.DefaultViewModel["Friends"] = friends.Where(x => friendShareStatus[x.facebookUserID]);
                return true;
            }
            return false;
        }

        public async Task RefreshItems() {
            if (friendsListLoaded) {
                Debug.WriteLine("Attemting to refresh items.");
                itemsByFriends.Clear();
                friendShareStatus.Clear();
                List<SharedItem> itemsByFriend;
                List<SharedItem> allItems = await sharedItemsTable.ToListAsync();

                // My items
                itemsByFriend = allItems.Where<SharedItem>(x => x.facebookUserID.Equals(FacebookId)).ToList<SharedItem>();
                itemsByFriends.Add(FacebookId, itemsByFriend);
                friendShareStatus.Add(FacebookId, itemsByFriend.Count != 0);
                // My items

                // Friends' items
                foreach (Friend friend in friends) {
                    itemsByFriend = allItems.Where<SharedItem>(x => x.facebookUserID.Equals(friend.facebookUserID)).ToList<SharedItem>();
                    itemsByFriends.Add(friend.facebookUserID, itemsByFriend);
                    friendShareStatus.Add(friend.facebookUserID, itemsByFriend.Count != 0);
                }
                // Friends' items

                allItems = null;
                Debug.WriteLine("Refreshed items.");
                ShowAllFriends();
                itemsLoaded = true;
            } else {
                Debug.WriteLine("Can't refresh items, friend list is not yet loaded!");
            }
        }

        public async Task ShareItem(string description) {
            try {
                StorageFile file = currentSelectedFile;
                currentSelectedFile = null;
                Dictionary<string, string> fileInfoDict = await UploadFile(file);
                SharedItem itemToShare = new SharedItem { facebookUserID = FacebookId, description = description, url = fileInfoDict["link"], name = fileInfoDict["name"], date = System.DateTime.UtcNow };
                await sharedItemsTable.InsertAsync(itemToShare);
                RefreshItems();
                var dialog = new MessageDialog("Successfully shared item: \"" + fileInfoDict["name"] + "\".");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            } catch (MobileServiceInvalidOperationException) {
                var dialog = new MessageDialog("Error when sharing item!");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private StorageFile currentSelectedFile;

        //private CancellationTokenSource ctsUpload;

        private async Task<Dictionary<string, string>> UploadFile(StorageFile file) {
            Dictionary<string, string> fileInfoDict = new Dictionary<string, string>();
            try {
                if (file != null) {
                    //this.progressBar.Value = 0;
                    //var progressHandler = new Progress<LiveOperationProgress>((progress) => { this.progressBar.Value = progress.ProgressPercentage; });
                    //this.ctsUpload = new System.Threading.CancellationTokenSource();
                    await LiveConnectClient.BackgroundUploadAsync("me/skydrive", file.Name, file, OverwriteOption.Rename);
                    fileInfoDict.Add("name", file.Name);
                    string fileID = await GetSkyDriveFileID(file.Name);
                    Debug.WriteLine("Upload completed, file name: " + file.Name + " file ID: " + fileID);
                    string fileLink = await GetLinkOfFile(fileID);
                    fileInfoDict.Add("link", fileLink);
                    return fileInfoDict;
                }
            } catch (System.Threading.Tasks.TaskCanceledException) {
                var dialog = new MessageDialog("Upload of file " + file.Name + " has been cancelled.");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            } catch (LiveConnectException exception) {
                var dialog = new MessageDialog("Error uploading file: " + exception.Message);
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
            return null;
        }

        public async Task PickFile() {
            currentSelectedFile = null;
            FileOpenPicker picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");
            currentSelectedFile = await picker.PickSingleFileAsync();
        }

        public bool UserChoseAFile() {
            return currentSelectedFile != null;
        }

        public string GetChosenFileName() {
            if (currentSelectedFile != null) {
                return currentSelectedFile.Name;
            }
            return String.Empty;
        }

        private async Task<string> GetSkyDriveFileID(string fileName) {
            LiveOperationResult operationResult = await LiveConnectClient.GetAsync("me/skydrive/files");
            var iEnum = operationResult.Result.Values.GetEnumerator();
            iEnum.MoveNext();
            var files = iEnum.Current as IEnumerable;
            foreach (dynamic file in files) {
                if (file.name == fileName) {
                    return file.id as string;
                }
            }
            return null;
        }

        private async Task<string> GetLinkOfFile(string fileID) {
            try {
                IDictionary<string, object> resultDict = (await LiveConnectClient.GetAsync(fileID + "/shared_read_link")).Result;
                return (resultDict["link"] as string);
            } catch (LiveConnectException exception) {
                Debug.WriteLine("Error getting shared read link: " + exception.Message);
            }
            return "";
        }

        //private void btnCancelUpload_Click(object sender, RoutedEventArgs e) {
        //    if (this.ctsUpload != null) {
        //        this.ctsUpload.Cancel();
        //    }
        //}

        public List<SharedItemsListElement> GetItemsOfUser(String facebookUserID) {
            List<SharedItemsListElement> items = new List<SharedItemsListElement>();
            if (itemsLoaded && friendsListLoaded) {
                foreach (SharedItem item in itemsByFriends[facebookUserID].OrderBy(x => x.date.Ticks).Reverse()) {
                    items.Add(new SharedItemsListElement(ProcessDate(item.date.ToLocalTime()), item.description, item.name, item.url));
                }
            }
            return items;
        }

        private String ProcessDate(DateTime date) {
            string dateString = ToDate(date.Hour) + ":" + ToDate(date.Minute);
            dateString += " " + ToDate(date.Day) + "/" + ToDate(date.Month) + "/" + ToDate(date.Year);
            return dateString;
        }

        private String ToDate(int date) {
            if (date.ToString().Length == 1) {
                return "0" + date.ToString();
            }
            return date.ToString();
        }

        public String GetNameOfUser(String facebookUserID) {
            if (facebookUserID == FacebookId) {
                return username;
            } else {
                foreach (Friend friend in friends) {
                    if (friend.facebookUserID == facebookUserID) {
                        return friend.Name;
                    }
                }
            }
            return "<Unknown person>";
        }

        public BitmapImage GetProfilePicOfUser(String facebookUserID) {
            if (facebookUserID == FacebookId) {
                return userProfilePicture;
            } else {
                foreach (Friend friend in friends) {
                    if (friend.facebookUserID == facebookUserID) {
                        return (new BitmapImage(friend.PictureUri));
                    }
                }
            }
            return null;
        }

        public ObservableCollection<Friend> Friends {
            get {
                return friends;
            }
        }

        public BitmapImage GetProfilePic() {
            return userProfilePicture;
        }

        public String GetUsername() {
            return username;
        }

        public String GetMyFacebookUserID() {
            return FacebookId;
        }

        public bool friendsListIsLoaded() {
            return friendsListLoaded;
        }

        public bool itemsAreLoaded() {
            return itemsLoaded;
        }
    }
}
