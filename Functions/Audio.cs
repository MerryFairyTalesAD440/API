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


//function to update the audio for a specific page and language of a book
//@author: Sahand Milaninia
namespace Functions
{
    public static class Audio
    {
        [FunctionName("PostPutAudio")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous,
        "put", "post",
        Route = "books/{bookId}/pages/{pageId}/languages/{languageCode}/audio")]
        HttpRequest req,
        string bookid,
        string pageid,
        string languagecode,
        ILogger log,
        ExecutionContext context)
        {
            try
            {
                log.LogInformation("Http function to put/post audio");

                //get request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                log.LogInformation($"data -> {data}");

                //get environment variables
                //variables stored in key vault for development
                var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

                //access azure keyvault
                var serviceTokenProvider = new AzureServiceTokenProvider();
                log.LogInformation("serviceTokenProvider");
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
                log.LogInformation("keyVaultClient");

                //storage variables for secrets
                SecretBundle secrets;
                String uri = String.Empty;
                String key = String.Empty;
                String database = String.Empty;
                String collection = String.Empty;

                //get the audio_url from the data
                Page page = data.pages.ElementAt(int.Parse(bookid) - 1);

                try
                {
                    //storage account is the keyvault key
                    secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_NAME"]}/");
                    //parse json stored in keyvalut
                    JObject details = JObject.Parse(secrets.Value.ToString());
                    uri = (string)details["COSMOS_URI"];
                    key = (string)details["COSMOS_KEY"];
                    database = (string)details["COSMOS_DB"];
                    collection = (string)details["COSMOS_COLLECTION"];

                    log.LogInformation("Secret Values retrieved from KeyVault.");
                }
                //display error if key vault access fails
                catch (Exception ex)
                {
                    return (ObjectResult)new ObjectResult("500" + ex.Message.ToString());
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


                    if (req.Method == HttpMethod.Post)
                    {
                        //track down the specific languages audio file, check if it exists, add new audio file if it does
                        Page p = b.Pages.ElementAt(int.Parse(pageid) - 1);
                        //TODO: need to access specific language code on this page to access the specific audio file to replace

                    }
                    else if (req.Method == HttpMethod.Put)
                    {
                        //track down the specific languages audio file, check if it exists, update to new audio file if it does
                        Page p = b.Pages.ElementAt(int.Parse(pageid) - 1);
                        //TODO: need to access specific language code on this page to check if the audio file exists before updating it, replacing if it does

                        //foreach (Language l in p.Languages)
                        //{
                        //    if (l.language = languagecode)
                        //    {
                        //        if (l.Audio_Url == null)
                        //        {
                        //            l.Audio_Url = url;
                        //        }
                        //        else
                        //        {
                        //            return (ActionResult)new StatusCodeResult(409);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        return (ActionResult)new StatusCodeResult(500);
                        //    }
                        //}
                    }
                    else if (req.Method == HttpMethod.Get)
                    {
                        //track down the specific languages audio file, check if it exists, return url to user if it does
                        Page p = b.Pages.ElementAt(int.Parse(pageid) - 1);
                        //TODO: need to access the specific language code on this page to check if the audio file exists in order to retrieve it
                    }
                    else if (req.Method == HttpMethod.Delete)
                    {
                        //track down the specific languages audio file, check if it exists, delete url if it does
                        Page p = b.Pages.ElementAt(int.Parse(pageid) - 1);
                        //TODO: need to access the specific language code on this page to check if the audio file exists in order to delete it
                    }
                    else
                    {
                        return (ActionResult)new BadRequestObjectResult("Could not complete request. Missing or invalid information provided.");
                    }

                    var result = await dbClient.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), b);
                    log.LogInformation($"document updated -> {result}");

                }
                catch (Exception ex)
                {
                    return (ObjectResult)new ObjectResult("404 " + "The requested book was not found");
                }

                return (ActionResult)new OkObjectResult($"200, DB write successful -> , {data}");
            }
            catch (Exception ex)
            {
                return (ActionResult)new ObjectResult("400" + ex.Message.ToString());
            }
        }
    }
}