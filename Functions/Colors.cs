using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Functions
{
    [JsonObject]
    
    public class Code {
        public List<int> rgba {get; set; }
        public string hex { get; set; }
    }
    
    public class Colors
    {
        [JsonProperty(PropertyName = "id")]
        public String Id { get; set; }

        [JsonProperty(PropertyName = "color")]
        public String Color { get; set; }

        [JsonProperty(PropertyName = "category")]
        public String Category { get; set; }

        [JsonProperty(PropertyName = "type")]
        public String Type { get; set; }

        [JsonProperty(PropertyName = "code")]
        public Code Code { get; set; }

        public override string ToString()
        {
           return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

    }
}
