SharEasy
========

The official repo of the SharEasy Windows 8 app.

To be able to correctly build & run the solution, you must create a 'Constants.cs' class, 
inside 'SharEasy/Common/' folder. The file structure should be:

    namespace SharEasy.ViewModels {
        public class Constants {
            public static readonly string FacebookAppId = "...";
            public static string MobileServiceAppURL = "...";
            public static string MobileServiceAppKey = "...";
            public static string LiveClientID = "...";
        }
    }

You must connect the Facebook App and Live App to the MobileService via Azure Management Portal.

Have fun!
