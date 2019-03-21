using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Functions
{
    [JsonObject]
    public class CosmosDatabase
    {
        [JsonProperty(PropertyName = "COSMOS_URI")]
        public String COSMOS_URI { get; set; }

        [JsonProperty(PropertyName = "COSMOS_KEY")]
        public String COSMOS_KEY { get; set; }

        [JsonProperty(PropertyName = "COSMOS_DB")]
        public String COSMOS_DB { get; set; }

        [JsonProperty(PropertyName = "COSMOS_COLLECTION")]
        public String COSMOS_COLLECTION { get; set; }

        public override string ToString()
        {
           return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

    }
}
