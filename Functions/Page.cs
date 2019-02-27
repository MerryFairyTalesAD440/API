
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
        public string Number { get; set; }

        [JsonProperty(PropertyName = "image_url")]
        public string Image_Url { get; set; }

        [JsonConverter(typeof(StringConverter<Language>))]
        [JsonProperty(PropertyName = "languages")]
        public List<Language> Languages { get; set; }
    }
}
