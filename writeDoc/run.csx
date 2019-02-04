#r "Newtonsoft.Json"

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static void Run(HttpRequest req, out object documentToSave, TraceWriter log)
{
    log.Info($"C# HTTP trigger function processed: {req}");
    
    string requestBody = new StreamReader(req.Body).ReadToEndAsync().Result;
    
    log.Info($"string requestBody = new StreamReader(req.Body).ReadToEndAsync().ToString();: {requestBody}");
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    
    documentToSave = new {
        id = data.id,
        name = data.name,
    };
}