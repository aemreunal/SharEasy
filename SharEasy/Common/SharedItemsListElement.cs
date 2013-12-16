namespace SharEasy.Common {

    public class SharedItemsListElement {

        public string Date { get; set; }

        public string Description { get; set; }

        public string URL { get; set; }

        public string Name { get; set; }

        public string UserID { get; set; }

        public SharedItem SharedItem { get; set; }

        public SharedItemsListElement(string date, string description, string name, string url, string userID, SharedItem sharedItem) {
            this.Date = date;
            this.Description = description;
            this.Name = name;
            this.URL = url;
            this.UserID = userID;
            this.SharedItem = sharedItem;
        }
    }
}
