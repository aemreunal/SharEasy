﻿using Facebook;
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
using Windows.UI.Xaml;
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
        private List<Upload> PendingUploads = new List<Upload>();

        private string SkyDriveFolderToUpload = "me/skydrive/public_documents";

        private StorageFile currentSelectedFile;

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
                } catch (Exception) {
                    Logout();
                    //Application.Current.Exit();
                }
            }
        }

        public async void FetchUserData(FriendsPage friendsPage) {
            this.friendsPage = friendsPage;
            await FetchUserInfo();
            await FetchFriends();
            await RefreshItems();
            if(SomeoneSharedSomething()) {
                ShowSharingFriends();
            } else {
                ShowAllFriends();
            }
        }

        private bool SomeoneSharedSomething() {
            foreach (bool shared in friendShareStatus.Values) {
                if (shared) {
                    return true;
                }
            }
            return false;
        }

        public void Logout() {
            while (msLoggedIn || fbLoggedIn) {
                try {
                    MobileServiceClient.Logout();
                    clearMSData();
                    FacebookSessionClient.Logout();
                    clearFBData();
                    //LiveAuthClient.Logout();
                    clearLiveData();
                } catch (InvalidOperationException) {
                    MessageDialog dialog = new MessageDialog("Error when logging out!");
                    dialog.Commands.Add(new UICommand("Ok"));
                    dialog.ShowAsync();
                } catch (NullReferenceException) {
                    /* Ignore it */
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
                FacebookSession = await FacebookSessionClient.LoginAsync("user_friends");
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
                    try {
                        LiveAuthClient = new LiveAuthClient();
                        LiveLoginResult authResult = await LiveAuthClient.LoginAsync(new List<string>() { "wl.signin", "wl.skydrive", "wl.skydrive_update", "wl.offline_access", "wl.contacts_skydrive" });
                        if (authResult.Status == LiveConnectSessionStatus.Connected) {
                            LiveSession = authResult.Session;
                            LiveConnectClient = new LiveConnectClient(LiveSession);
                            Debug.WriteLine("Connected to Live.");
                        }
                    } catch (NullReferenceException) {
                        MessageDialog dialog = new MessageDialog("Login failed! Network connection unavailable.");
                        dialog.Commands.Add(new UICommand("Ok", new UICommandInvokedHandler((cmd) => App.Current.Exit())));
                        dialog.ShowAsync();
                    }
                }
            } catch (InvalidOperationException e) {
                MessageDialog dialog = new MessageDialog("Login failed! Exception details: " + e.Message);
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private async Task AuthenticateMobileService() {
            MobileServiceAuthenticationProvider provider = MobileServiceAuthenticationProvider.Facebook;
            while (MobileServiceUser == null) {
                try {
                    MobileServiceClient = new MobileServiceClient(Constants.MobileServiceAppURL, Constants.MobileServiceAppKey);
                    MobileServiceUser = await MobileServiceClient.LoginAsync(provider, JObject.Parse("{\"access_token\":\"" + AccessToken + "\"}"));
                    msLoggedIn = true;
                    Debug.WriteLine("Azure Mobile Services login succeeded.");
                } catch (InvalidOperationException e) {
                    var dialog = new MessageDialog("Error when logging in to Azure Mobile Services: " + e.Message);
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
        }

        public bool ShowAllFriends() {
            if (friendsListLoaded) {
                friendsPage.SetShowAllFriends(true);
                friendsPage.SetFriendsButtonText("Show: Friends who've shared");
                friendsPage.DefaultViewModel["Friends"] = friends.OrderBy(x => x.Name);
                return true;
            }
            return false;
        }

        public bool ShowSharingFriends() {
            if (friendsListLoaded && itemsLoaded) {
                friendsPage.DefaultViewModel["Friends"] = friends.Where(x => friendShareStatus[x.facebookUserID]);
                friendsPage.SetShowAllFriends(false);
                friendsPage.SetFriendsButtonText("Show: All friends");
                return true;
            }
            return false;
        }

        public async Task RefreshItems() {
            if (friendsListLoaded) {
                Debug.WriteLine("Attemting to refresh items.");
                itemsByFriends.Clear();
                friendShareStatus.Clear();
                List<SharedItem> allItems;
                List<SharedItem> itemsByFriend;
                sharedItemsTable = MobileServiceClient.GetTable<SharedItem>();

                //await sharedItemsTable.InsertAsync(new SharedItem { facebookUserID = "1234", name = "Deneme", date = DateTime.Now, description = "deneme", url = "www.deneme.com" });

                allItems = await sharedItemsTable.ToListAsync();

                foreach (SharedItem item in allItems) {
                    Debug.WriteLine("UID: " + item.facebookUserID);
                }

                //IMobileServiceTableQuery<SharedItem> query = sharedItemsTable.Where(x => x.facebookUserID == FacebookId);

                //foreach (Friend friend in friends) {
                //    query = query.Where(x => x.facebookUserID == friend.facebookUserID);
                //}

                //List<SharedItem> allItems = await query.ToListAsync();

                //string[] fbIDs = new string[friends.Count + 1];
                //fbIDs[0] = FacebookId;

                //for (int i = 0; i < friends.Count; i++) {
                //    fbIDs[i + 1] = friends[i].facebookUserID;
                //}

                //allItems = await sharedItemsTable.Where(x => fbIDs.Contains<string>(x.facebookUserID)).ToListAsync();

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
                if (fileInfoDict != null) {
                    SharedItem itemToShare = new SharedItem { facebookUserID = FacebookId, description = description, url = fileInfoDict["link"], name = fileInfoDict["name"], date = System.DateTime.UtcNow };
                    await sharedItemsTable.InsertAsync(itemToShare);
                    var dialog = new MessageDialog("Successfully shared item: \"" + fileInfoDict["name"] + "\".");
                    dialog.Commands.Add(new UICommand("Ok"));
                    dialog.ShowAsync();
                }
                RefreshItems();
            } catch (MobileServiceInvalidOperationException) {
                var dialog = new MessageDialog("Error when sharing item!");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        public async Task DeleteItem(SharedItem itemToDelete) {
            try {
                await sharedItemsTable.DeleteAsync(itemToDelete);
                var dialog = new MessageDialog("Successfully deleted " + itemToDelete.name + ".");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
                RefreshItems();
            } catch (MobileServiceInvalidOperationException) {
                var dialog = new MessageDialog("Error when deleting item!");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
        }

        private async Task<Dictionary<string, string>> UploadFile(StorageFile file) {
            Upload Upload = new Upload(file);
            try {
                if (file != null) {
                    Dictionary<string, string> fileInfoDict = new Dictionary<string, string>();

                    Upload.ProgressHandler = new Progress<LiveOperationProgress>((progress) => Upload.SetProgressValue(progress.ProgressPercentage));
                    Upload.CancellationToken = new System.Threading.CancellationTokenSource();

                    PendingUploads.Add(Upload);

                    await LiveConnectClient.BackgroundUploadAsync(SkyDriveFolderToUpload, Upload.File.Name, Upload.File, OverwriteOption.Rename, Upload.CancellationToken.Token, Upload.ProgressHandler);

                    Upload.SetProgressValue(100);

                    fileInfoDict.Add("name", Upload.File.Name);
                    string fileID = await GetSkyDriveFileID(Upload.File.Name);
                    Debug.WriteLine("Upload completed, file name: " + Upload.File.Name + " file ID: " + fileID);
                    string fileLink = await GetLinkOfFile(fileID);
                    fileInfoDict.Add("link", fileLink);

                    friendsPage.HidePendingUploadsPopup();
                    PendingUploads.Remove(Upload);

                    return fileInfoDict;
                }
            } catch (TaskCanceledException) {
                PendingUploads.Remove(Upload);
                friendsPage.HidePendingUploadsPopup();
                var dialog = new MessageDialog("Upload of file " + file.Name + " has been cancelled.");
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            } catch (LiveConnectException exception) {
                PendingUploads.Remove(Upload);
                friendsPage.HidePendingUploadsPopup();
                var dialog = new MessageDialog("Error uploading file: " + exception.Message);
                dialog.Commands.Add(new UICommand("Ok"));
                dialog.ShowAsync();
            }
            return null;
        }

        public List<Upload> GetPendingUploads() {
            return PendingUploads;
        }

        public bool UploadingFiles() {
            return PendingUploads.Count != 0;
        }

        public async Task CancelUpload(Upload upload) {
            if (upload.CancellationToken != null) {
                upload.CancellationToken.Cancel();
            }
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
            LiveOperationResult operationResult = await LiveConnectClient.GetAsync(SkyDriveFolderToUpload + "/files");
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

        public List<SharedItemsListElement> GetItemsOfUser(String facebookUserID) {
            List<SharedItemsListElement> items = new List<SharedItemsListElement>();
            if (itemsLoaded && friendsListLoaded) {
                foreach (SharedItem item in itemsByFriends[facebookUserID].OrderBy(x => x.date.Ticks).Reverse()) {
                    items.Add(new SharedItemsListElement(ProcessDate(item.date.ToLocalTime()), item.description, item.name, item.url, item.facebookUserID, item));
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
