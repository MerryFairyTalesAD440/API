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
            ILogger log)
        {
            if (req.Method == HttpMethod.Post) {
                return (ActionResult)new StatusCodeResult(405);
            }

  
            //apply for key vault client
            var serviceTokenProvider = new AzureServiceTokenProvider();
          
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
            
            //storage variables for secrets
            SecretBundle secrets;
            String cosmosEndpointUrl = String.Empty;
            String cosmosAuthorizationKey = String.Empty;
            String databaseId = String.Empty;
            String collection = String.Empty;


            try
            {

                secrets = await keyVaultClient.GetSecretAsync("https://maria-key-vault.vault.azure.net/secrets/cosmos-connection/");

                //parse json stored.
                JObject details = JObject.Parse(secrets.Value.ToString());
                cosmosEndpointUrl = (string)details["COSMOS_URI"];
                cosmosAuthorizationKey = (string)details["COSMOS_KEY"];
                databaseId = (string)details["COSMOS_DB"];
                collection = (string)details["COSMOS_COLLECTION"];


            } catch (KeyVaultErrorException ex) {
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }


            // Connect to the cosmos endpoint with authorization key.
            try
            {
                DocumentClient client = new DocumentClient(new Uri(cosmosEndpointUrl), cosmosAuthorizationKey);

                // Set some common query options
                FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

                IQueryable<Book> queryBooks = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri(databaseId, collection),
                   queryOptions);

                return (ActionResult)new OkObjectResult(queryBooks);

            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);

                return (ActionResult)new StatusCodeResult(500);
            }

        }
            
    }
}
