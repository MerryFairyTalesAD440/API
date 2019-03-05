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
//@author francesco
namespace Functions
{
    public static class Text
    {
        [FunctionName("text")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous,
        "put", "post",
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
                log.LogInformation("Http function to put/post texturl");

                //get request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                log.LogInformation($"data -> {data}");

                //get environment variables
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

                SecretBundle secrets;
                String uri = String.Empty;
                String key = String.Empty;
                String database = String.Empty;
                String collection = String.Empty;

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
                catch (Exception kex)
                {
                    return (ObjectResult)new ObjectResult("500" + kex.Message.ToString());
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
                    for (int i = 0; i < b.Pages.Count(); i++)
                    {
                        Page p = b.Pages.ElementAt(i);
                        for (int j = 0; j < p.Languages.Count(); j++)
                        {
                            p.Languages.ElementAt(j).Text_Url = data.pages[i].languages[j].text_url.ToString();
                        }
                    }

                    var result = await dbClient.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), b);
                    log.LogInformation($"document updated -> {result}");

                }
                catch (Exception wrt)
                {
                    return (ObjectResult)new ObjectResult("404 " + "The requested book was not found");
                }

                return (ActionResult)new OkObjectResult($"200, DB write successful -> , {data}");
            }
            catch (Exception e)
            {
                return (ActionResult)new ObjectResult("400" + e.Message.ToString());
            }
        }
    }
}