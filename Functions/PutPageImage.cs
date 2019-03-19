using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Web.Http;
using System.Linq;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;


namespace Functions
{
    public static class PutPageImage
    {
        [FunctionName("PutPageImage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "books/{bookid}/pages/{pageid}/image")] HttpRequestMessage req,
            ExecutionContext context,
            string bookid, string pageid,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function to update the existing page image.");

            int pagenumber = Convert.ToInt32(pageid);

            string requestBody = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            //storage variables for secrets
            SecretBundle secrets;
            String cosmosEndpointUrl = String.Empty;
            String cosmosAuthorizationKey = String.Empty;
            String database = String.Empty;
            String collection = String.Empty;

            var config = new ConfigurationBuilder()
                       .SetBasePath(context.FunctionAppDirectory)
                       .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                       .AddEnvironmentVariables()
                       .Build();

            //apply for key vault client
            var serviceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

            // Connect to vault client
            try
            {
                secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_NAME"]}/");

                //parse json stored.
                JObject details = JObject.Parse(secrets.Value);
                cosmosEndpointUrl = (string)details["COSMOS_URI"];
                cosmosAuthorizationKey = (string)details["COSMOS_KEY"];
                database = (string)details["COSMOS_DB"];
                collection = (string)details["COSMOS_COLLECTION"];

            }
            catch (KeyVaultErrorException ex)
            {
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }

            DocumentClient client = new DocumentClient(new Uri(cosmosEndpointUrl), cosmosAuthorizationKey);
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
            var collectionLink = UriFactory.CreateDocumentCollectionUri(database, collection);
            var query = "SELECT * FROM Books b WHERE b.id = \'" + bookid + "\'";
            var document = client.CreateDocumentQuery(collectionLink, query, queryOptions).ToList();
            log.LogInformation(document.Count.ToString());

            // Validation
            if (document.Count == 0) { return (ActionResult)new StatusCodeResult(404); }

            Book book = document.ElementAt(0);

            // resource not found 
            if (book.Id == null) { return (ActionResult)new StatusCodeResult(404); }

            Page page = book.Pages.Find(y => y.Number.Contains(pageid));

            // Bad page input
            if (page == null)
            {
                log.LogError("Book's page not found.");
                return (ActionResult)new StatusCodeResult(404);
            }

            // ---- PUT method to update image in the container --- ///
            if (page.Image_Url != null)
            {

                // Updates the page image url in the book json blob
                page.Image_Url = data?.image_url;

                try {
                    await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);
                } catch (Exception ex) {
                    log.LogError("Error returned when updating the page image: {0}", ex.Message);
                    return (ActionResult)new StatusCodeResult(500);
                }


                return (ActionResult)new OkObjectResult($"Page Image URL has successfully updated.");
            }
            else
            {
                // Reached this point because the image url is null
                // The client call should use POST method if trying to create new image for the page.
                return (ActionResult)new StatusCodeResult(409);
            }
        }
    }
}
