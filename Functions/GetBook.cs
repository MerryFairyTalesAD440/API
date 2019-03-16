using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json.Linq;

namespace Functions
{
    public static class GetBook
    {
        /* GetBook - Retrieves an existing book. */
        [FunctionName("GetBook")]
        [Produces("application/json")]
        public static async Task<IActionResult> Retrieve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/{bookid}")]HttpRequest req,
        ILogger log, ExecutionContext context, string bookid)
        {
            log.LogInformation("C# HTTP trigger function processed a request to update a book");
            //set configuration
            var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

            var serviceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

            // variables for cosmos db
            SecretBundle secrets;
            String uri = String.Empty;
            String key = String.Empty;
            String database = String.Empty;
            String collection = String.Empty;

            try
            {
                secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_NAME"]}/");
                JObject details = JObject.Parse(secrets.Value.ToString());
                uri = (string)details["COSMOS_URI"];
                key = (string)details["COSMOS_KEY"];
                database = (string)details["COSMOS_DB"];
                collection = (string)details["COSMOS_COLLECTION"];
            }

            catch (KeyVaultErrorException ex)
            {
                return new ForbidResult("Unable to access secrets in vault!");
            }
            {
                log.LogInformation("C# HTTP trigger function processed a request to get a book");
                log.LogInformation("Attempting to retrieve book from database - bookid: " + bookid);
                DocumentClient client = new DocumentClient(new Uri(uri), key);
                var option = new FeedOptions { EnableCrossPartitionQuery = true };
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(database, collection);
                dynamic document = client.CreateDocumentQuery<Book>(collectionUri, option).Where(b => b.Id == bookid)
                                .AsEnumerable().FirstOrDefault();
                if (document == null)
                {
                    return new NotFoundResult();
                }
                Book book = (dynamic)document;
                return new OkObjectResult(book);
            }
        }

    }
}