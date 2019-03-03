using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MongoDB.Driver;
using Microsoft.Azure.Documents.Client;
using System.Linq;
using MongoDB.Driver.Linq;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;


//function to update a texturl for a book
namespace Functions
{
    public static class Text
    {
        [FunctionName("text")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous,
        "get", "post",
        Route = "books/{bookId}/pages/{pageId}/languages/{languageCode}/text")]
        HttpRequest req,
        string bookid,
        string pageid,
        string languagecode,
        ILogger log,
        ExecutionContext context)
        {
            try
            {
                log.LogInformation("Http function to put/post text");

                //get POST body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                log.LogInformation($"data -> {data}");

                //get environment variables
                /*
                var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
                */

                //access azure keyvault
                var serviceTokenProvider = new AzureServiceTokenProvider();
                log.LogInformation("serviceTokenProvider");
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
                log.LogInformation("keyVaultClient");

                //SecretBundle secretValues;
                String uri = String.Empty;
                String key = String.Empty;
                String database = String.Empty;
                String collection = String.Empty;

                try
                {
                    SecretBundle secretURI = await keyVaultClient.GetSecretAsync("https://francesco-key-vault.vault.azure.net/secrets/cosmos-uri/");
                    SecretBundle secretKey = await keyVaultClient.GetSecretAsync("https://francesco-key-vault.vault.azure.net/secrets/cosmos-key/");
                    SecretBundle secretDB = await keyVaultClient.GetSecretAsync("https://francesco-key-vault.vault.azure.net/secrets/cosmos-db-name/");
                    SecretBundle secretTable = await keyVaultClient.GetSecretAsync("https://francesco-key-vault.vault.azure.net/secrets/cosmos-table/");

                    uri = secretURI.Value;
                    key = secretKey.Value;
                    database = secretDB.Value;
                    collection = secretTable.Value;
                    log.LogInformation("Secret Values retrieved from KeyVault.");
                }
                catch (Exception kex)
                {
                    return (ObjectResult)new ObjectResult(kex.Message.ToString());
                }

                //declare client
                DocumentClient dbClient = new DocumentClient(new Uri(uri), key);
                log.LogInformation("new DocumentClient");

                try
                {
                    var collectionUri = UriFactory.CreateDocumentCollectionUri(database, collection);
                    var query = "SELECT * FROM Books b WHERE b.id=\"" + bookid + "\"";
                    var crossPartition = new FeedOptions { EnableCrossPartitionQuery = true };
                    var documents = dbClient.CreateDocumentQuery(collectionUri, query, crossPartition).ToList();
                    log.LogInformation($"document retrieved -> {documents.Count().ToString()}");

                    Book b = documents.ElementAt(0);
                    //update
                    b.Pages.ElementAt(int.Parse(pageid) - 1).Languages.ElementAt(languagecode.Equals("en_US") ? 0 : 1).Text_Url = data.text.ToString();
                    var result = await dbClient.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), b);
                    log.LogInformation($"document updated -> {result}");

                }
                catch (Exception wrt)
                {
                    return (ObjectResult)new ObjectResult(wrt.Message.ToString());
                }

                return (ActionResult)new OkObjectResult($"200ok, DB write successful -> , {data}");
            }
            catch (Exception e)
            {
                return (ActionResult)new BadRequestObjectResult("400");
            }
        }
    }

    public class Book
    {
        [JsonProperty(PropertyName = "id")]
        public String Id { get; set; }

        [JsonProperty(PropertyName = "description")]
        public String Description { get; set; }

        [JsonProperty(PropertyName = "author")]
        public String Author { get; set; }

        [JsonProperty(PropertyName = "cover_image")]
        public String Cover_Image { get; set; }

        [JsonProperty(PropertyName = "title")]
        public String Title { get; set; }

        [JsonConverter(typeof(StringConverter<Page>))]
        [JsonProperty(PropertyName = "pages")]
        public List<Page> Pages { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }

    [JsonObject]
    public class Page
    {
        [JsonProperty(PropertyName = "number")]
        public string Number { get; set; }

        [JsonProperty(PropertyName = "image_url")]
        public string Image_Url { get; set; }

        [JsonConverter(typeof(StringConverter<Language>))]
        [JsonProperty(PropertyName = "languages")]
        public List<Language> Languages { get; set; }

    }

    [JsonObject]
    public class Language
    {
        [JsonProperty(PropertyName = "language")]
        public string language { get; set; }

        [JsonProperty(PropertyName = "text_url")]
        public string Text_Url { get; set; }

        [JsonProperty(PropertyName = "audio_url")]
        public string Audio_Url { get; set; }

    }

    public class StringConverter<T> : JsonConverter
    {
        /// <summary>
        /// Checks type
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        /// <summary>
        /// Reads and converts object if of not an array
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>();
            }
            return new List<T> { token.ToObject<T>() };
        }

        /// <summary>
        /// Writes object
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            List<T> list = (List<T>)value;
            if (list.Count == 1)
            {
                value = list[0];
            }
            serializer.Serialize(writer, value);
        }

        /// <summary>
        /// Can get object
        /// </summary>
        public override bool CanWrite
        {
            get { return true; }
        }
    }
}