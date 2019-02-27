
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
        public string Text_Url { get; set; }

        [JsonProperty(PropertyName = "audio_url")]
        public string Audio_Url { get; set; }

    }
}
