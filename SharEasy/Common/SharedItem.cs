using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharEasy.Common {
    public class SharedItem {
        public string id { get; set; }

        [JsonProperty(PropertyName = "facebookUserID")]
        public string facebookUserID { get; set; }

        [JsonProperty(PropertyName = "description")]
        public string description { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string url { get; set; }

        [JsonProperty(PropertyName = "date")]
        public DateTime date { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string name { get; set; }
    }
}
