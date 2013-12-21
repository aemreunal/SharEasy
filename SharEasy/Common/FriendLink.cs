using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharEasy.Common {
    public class FriendLink {
        public string id { get; set; }

        /*
        [JsonProperty(PropertyName = "userId")]
        public string userId { get; set; } */

        [JsonProperty(PropertyName = "friendId")]
        public string friendId { get; set; }

        public FriendLink(string friendId) {
            this.friendId = friendId;
        }
    }
}
