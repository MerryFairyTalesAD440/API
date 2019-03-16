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
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Functions
{
    public static class deleteBook
    {
        [FunctionName("deleteBook")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "books/{bookId}")] HttpRequestMessage req,
            string bookid,
            ExecutionContext context,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function to delete book by id.");

            var config = new ConfigurationBuilder()
                       .SetBasePath(context.FunctionAppDirectory)
                       .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                       .AddEnvironmentVariables()
                       .Build();


            //apply for key vault client
            var serviceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

            IQueryable<Book> bookQuery;

            //storage variables for secrets
            SecretBundle secrets;
            String cosmosEndpointUrl = String.Empty;
            String cosmosAuthorizationKey = String.Empty;
            String database = String.Empty;
            String collection = String.Empty;

            // variables for storage
            SecretBundle storageSecrets;
            String storageUri = String.Empty;
            String storageConnectionString = String.Empty;

            // Connect to vault client
            try
            {
                secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_NAME"]}/");

                //parse json stored.
                JObject details = JObject.Parse(secrets.Value.ToString());
                cosmosEndpointUrl = (string)details["COSMOS_URI"];
                cosmosAuthorizationKey = (string)details["COSMOS_KEY"];
                database = (string)details["COSMOS_DB"];
                collection = (string)details["COSMOS_COLLECTION"];

            }
            catch (KeyVaultErrorException ex)
            {
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }


            //set options client and query
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };

            DocumentClient client = new DocumentClient(new Uri(cosmosEndpointUrl), cosmosAuthorizationKey);
            List<Book> bookList = null;

            try {

             
                bookQuery = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri(database, collection),
                    "SELECT * FROM Books b  WHERE b.id = \'" + bookid + "\'", queryOptions);

                bookList = bookQuery.ToList<Book>();

                if (bookList.Count == 0)
                {
                    log.LogInformation("Book not found.");
                    return (ActionResult)new StatusCodeResult(404);
                }

                foreach (var book in bookList)
                {
                        Uri docUri = UriFactory.CreateDocumentUri(database, collection, book.Id);
                        
                        // Need to provide the partitionKey here
                        // The key can by found from database > collection > scale and settings partition key
                        // The partition key is: /title 
                        await client.DeleteDocumentAsync(docUri, new RequestOptions { PartitionKey = new PartitionKey(book.Title) });
                        Console.WriteLine(@"Deleted: {0}", book.Id);
                }



            } catch (Exception ex) {
                log.LogError("Unable to delete book: " + ex.Message);
                return (ActionResult)new StatusCodeResult(500);
            }

            // Delete the storage container associated with the book
            Book deletedBook = bookList.ElementAt(0);
            String containerName = deletedBook.Title;
            log.LogInformation(".");
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;

            try
            {
                storageSecrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["STORAGE_NAME"]}/");
                JObject details = JObject.Parse(storageSecrets.Value);
                storageUri = (string)details["STORAGE_URI"];
                storageConnectionString = (string)details["STORAGE_CONNECTION_STRING"];
            }
            catch (KeyVaultErrorException ex)
            {
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }

            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    log.LogInformation("Storage account accessed.");
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Fix the string to make it comply with the rules
                    containerName = containerName.ToLower();
                    containerName = containerName.Replace(" ", "-");

                    cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
                    await cloudBlobContainer.DeleteIfExistsAsync();
                    log.LogInformation("Container associated with deleted book was deleted.");

                }
                catch (StorageException ex)
                {
                    log.LogInformation("Error returned from the service: {0}", ex.Message);
                    return (ActionResult)new StatusCodeResult(500);
                }

            }
            else
            {
                log.LogInformation("Key Vault access failed.");
                return (ActionResult)new StatusCodeResult(500);

            }

            return (ActionResult)new OkObjectResult("Successfully deleted bookId : " + bookid);

        }

    }

}
