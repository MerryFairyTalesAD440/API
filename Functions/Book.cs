
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
        public String Id { get; set; }

        [JsonProperty(PropertyName = "description")]
        public String Description { get; set; }

        [JsonProperty(PropertyName = "author")]
        public String Author { get; set; }

        [JsonProperty(PropertyName = "cover_image")]
        public String Cover_Image { get; set; }

        [JsonConverter(typeof(StringConverter<Page>))]
        [JsonProperty(PropertyName = "pages")]
        public List<Page> Pages { get; set; }

        public override string ToString()
        {
           return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

    }
}
