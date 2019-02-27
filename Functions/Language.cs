
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Functions
{
    [JsonObject]
    class Language
    {
        [JsonProperty(PropertyName = "language")]
        public string language { get; set; }

        [JsonProperty(PropertyName = "text_url")]
        public string text_url { get; set; }

        [JsonProperty(PropertyName = "audio_url")]
        public string audio_url { get; set; }

    }
}
