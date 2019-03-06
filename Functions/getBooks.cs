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
using System.Net.Http;
using Microsoft.Azure.Documents.Client;
using System.Linq;
using Microsoft.Azure.Documents;

using System.Net;
using System.Configuration;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Functions
{
    public static class GetBooks
    {
        /* GET /books - function to get or list all books. */
        [FunctionName("books")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req,
            ExecutionContext context,
            ILogger log)
        {
            log.LogInformation("Getting list of books.");
            if (req.Method == HttpMethod.Post) {
                return (ActionResult)new StatusCodeResult(405);
            }

            var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

            log.LogInformation("Config >>> " + config);


             //storage variables for secrets
            SecretBundle secrets;
            String cosmosEndpointUrl = String.Empty;
            String cosmosAuthorizationKey = String.Empty;
            String databaseId = String.Empty;
            String collection = String.Empty;


            try
            {
                var serviceTokenProvider = new AzureServiceTokenProvider();
                log.LogInformation("serviceTokenProvider >>> " + serviceTokenProvider);

                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
                log.LogInformation("keyVaultClient >>> " + keyVaultClient);


                secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_NAME"]}/");
                log.LogInformation("Secrets retrieved... ");

                //parse json stored.
                JObject details = JObject.Parse(secrets.Value.ToString());
                cosmosEndpointUrl = (string)details["COSMOS_URI"];
                cosmosAuthorizationKey = (string)details["COSMOS_KEY"];
                databaseId = (string)details["COSMOS_DB"];
                collection = (string)details["COSMOS_COLLECTION"];


            } catch (Exception ex) {
                log.LogError(ex.Message);
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }


            // Connect to the cosmos endpoint with authorization key.
            try
            {
                DocumentClient client = new DocumentClient(new Uri(cosmosEndpointUrl), cosmosAuthorizationKey);
                log.LogInformation("new DocumentClient created... ");
                // Set some common query options
                FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true };
                log.LogInformation("queryOptions... ");

                IQueryable<Book> queryBooks = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri(databaseId, collection),
                   queryOptions);
                log.LogInformation("queryBooks reached here... ");

                List<Book> bookList = queryBooks.ToList<Book>();
                string allBooks = JsonConvert.SerializeObject(bookList, Formatting.Indented);

                log.LogInformation("Now returning all books...");
                return (ActionResult)new OkObjectResult(allBooks);

            }
            catch (Exception ex)
            {
                log.LogError("ERROR: " + ex.Message);

                return (ActionResult)new StatusCodeResult(500);
            }

        }
            
    }
}
