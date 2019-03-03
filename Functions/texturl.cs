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
                log.LogInformation("Http function to put/post texturl");

                //get POST body
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

                //SecretBundle secretValues;
                String uri = String.Empty;
                String key = String.Empty;
                String database = String.Empty;
                String collection = String.Empty;

                try
                {
                    SecretBundle secretURI = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/cosmos-uri/");
                    SecretBundle secretKey = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/cosmos-key/");
                    SecretBundle secretDB = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/cosmos-db-name/");
                    SecretBundle secretTable = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/cosmos-table/");

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
}