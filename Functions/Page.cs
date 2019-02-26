using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Functions
{
    [JsonObject]
    class Page
    {
        [JsonProperty(PropertyName = "number")]
        public string number { get; set; }

        [JsonProperty(PropertyName = "image_url")]
        public string image_url { get; set; }

        [JsonConverter(typeof(StringConverter<Language>))]
        [JsonProperty(PropertyName = "languages")]
        public List<Language> languages { get; set; }
    }
}
