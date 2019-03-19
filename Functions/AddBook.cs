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
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Documents.Client;
using System.Net;
using Microsoft.Azure.Documents;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Functions
{
    public static class AddBook
    {
        /* AddBook - Adds a new book. Returns the book id. */
        [FunctionName("PostBook")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "books")] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request to add a new book");

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

            // variables for storage
            SecretBundle storageSecrets;
            String storageUri = String.Empty;
            String storageConnectionString = String.Empty;

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

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic book = JsonConvert.DeserializeObject<Book>(requestBody);
            book.Id = System.Guid.NewGuid().ToString();
            DocumentClient client = new DocumentClient(new Uri(uri), key);
            var option = new FeedOptions { EnableCrossPartitionQuery = true };
            IQueryable<Book> query;
            try
            {
                query = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri(database, collection), "SELECT * FROM Books b WHERE b.title = \'" + book.Title + "\'", option);
            }
            catch (Exception ex)
            {
                return (ActionResult)new StatusCodeResult(500);
            }
            List<Book> books = query.ToList<Book>();
            if (books.Count == 0)
            {
                await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);
                log.LogInformation("Saving new book to the database.");
            }
            else
            {
                return (ActionResult)new ConflictObjectResult("A book with that title already exists.");
            }
            String containerName = book.Title;
            // Create book container
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
                    await cloudBlobContainer.CreateAsync();
                    // // Allows public access to the blob, comment out if want to require private access only
                    // await cloudBlobContainer.CreateIfNotExistsAsync(
                    //           BlobContainerPublicAccessType.Container, // can set to .Container or .Blob
                    //           new BlobRequestOptions(),
                    //           new OperationContext());
                    log.LogInformation("New container was created.");

                }
                catch (StorageException ex)
                {
                    log.LogInformation("Error returned from the service: {0}", ex.Message);
                    if (ex.Message.Contains("container already exists"))
                    {
                        return (ActionResult)new ConflictObjectResult("A container with that title already exists.");
                    }
                    else
                    {
                        return (ActionResult)new StatusCodeResult(500);
                    }
                }

            }
            else
            {
                log.LogInformation("Key Vault access failed.");
                return (ActionResult)new StatusCodeResult(500);

            }

            return book != null
                ? (ActionResult)new OkObjectResult(book.Id)
                : new BadRequestObjectResult("Please pass a valid book in the request body");
        }
    }

}
