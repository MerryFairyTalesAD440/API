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


//function to add the audio for a specific page and language of a book
//@author: Sahand Milaninia
namespace Functions
{
    public static class GetAudio
    {
        [FunctionName("GetAudio")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous,
        "get", Route = "books/{bookId}/pages/{pageId}/languages/{languageCode}/audio")]
        HttpRequest req, string bookid, string pageid, string languagecode, ILogger log, ExecutionContext context)
        {
            log.LogInformation("Http function to get audio");
            //remove spaces from bookid
            bookid = bookid.Replace(" ", "");

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
            log.LogInformation("Got environment variables");

            //access azure keyvault
            var serviceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
            log.LogInformation("key vault accessed");

            //storage variables for secrets
            SecretBundle secrets;
            String uri = String.Empty;
            String key = String.Empty;
            String database = String.Empty;
            String collection = String.Empty;

            //variable to store result
            String result = null;

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
            catch (KeyVaultErrorException)
            {
                return new ForbidResult("Unable to access secrets in vault!");
            }

            //declare and set client
            DocumentClient dbClient = new DocumentClient(new Uri(uri), key);
            log.LogInformation("new DocumentClient");

            try
            {
                log.LogInformation("running query");
                //setup query, search by bookid to find the book
                var collectionUri = UriFactory.CreateDocumentCollectionUri(database, collection);
                var queryString = "SELECT * FROM Books b WHERE b.id=\"" + bookid + "\"";
                var crossPartition = new FeedOptions { EnableCrossPartitionQuery = true };
                var query = dbClient.CreateDocumentQuery(collectionUri, queryString, crossPartition).ToList();
                log.LogInformation($"document retrieved -> {query.Count().ToString()}");

                if (query.Count == 0)
                {
                    return (ActionResult)new NotFoundObjectResult(new { message = "Book ID not found" });
                }
                else
                {
                    //set the book to the first index in the list of books
                    Book b = query[0];
                    //get audio for the specific page we're interested in
                    Page p = b.Pages.ElementAt(int.Parse(bookid) - 1);
                    foreach (Language l in p.Languages)
                    {
                        if (l.language.Equals(languagecode))
                        {
                            result = l.Audio_Url;
                        }
                    }
                    log.LogInformation("pages searched");

                    //return success message
                    if (result != null)
                    {
                        return new ObjectResult(result);
                    }
                    else
                    {
                        return (ActionResult)new StatusCodeResult(404);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation("exception: " + ex);
                return (ActionResult)new StatusCodeResult(500);
            }
            
        }
    }
}