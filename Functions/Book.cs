using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Functions
{
    [JsonObject]
    class Book
    {
        [JsonProperty(PropertyName = "id")]
        public String id { get; set; }
        [JsonProperty(PropertyName = "description")]
        public String description { get; set; }
        [JsonProperty(PropertyName = "author")]
        public String author { get; set; }
        [JsonProperty(PropertyName = "cover_image")]
        public String cover_image { get; set; }

        [JsonConverter(typeof(StringConverter<Page>))]
        [JsonProperty(PropertyName = "pages")]
        public List<Page> pages { get; set; }
        public override string ToString()
        {
           return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

    }
}
