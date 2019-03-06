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
    public static class deleteBook
    {
        [FunctionName("deleteBook")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "books/{bookId}")] HttpRequestMessage req,
            string bookid,
            ExecutionContext context,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function to delete book by id.");


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

            }
            catch (KeyVaultErrorException ex)
            {
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }

           

            try
            {


                DocumentClient client = new DocumentClient(new Uri(cosmosEndpointUrl), cosmosAuthorizationKey);

                var collectionLink = UriFactory.CreateDocumentCollectionUri(databaseId, collection);

                var query = "SELECT * FROM Books b WHERE b.id = \'" + bookid + "\'";

                log.LogInformation("query: " + query);

                // Currently getting issue about EnableCrossPartitionQuery disabled.
                FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };


                log.LogInformation("queryOptions: " + queryOptions);


                var document = client.CreateDocumentQuery(collectionLink, query, queryOptions).ToList();

                log.LogInformation("document: " + document);


                // Getting the book element
                Book book = document.ElementAt(0);

                log.LogInformation("book: " + book);

                // Book ID not found 
                if (book.Id == null) { 
                    return (ActionResult)new StatusCodeResult(404); 
                }


                Document doc = client.CreateDocumentQuery(collectionLink)
                            .Where(d => d.Id == bookid)
                            .AsEnumerable()
                            .FirstOrDefault();

                log.LogInformation("doc: " + doc);

                await client.DeleteDocumentAsync(doc.SelfLink);

                return (ActionResult)new OkObjectResult("Successfully deleted {0} : " + bookid);

            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);

                return (ActionResult)new StatusCodeResult(500);
            }
        }

    }


}
