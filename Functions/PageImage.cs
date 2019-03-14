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
    public static class PageImage
    {
        [FunctionName("PageImage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete", Route = "books/{bookid}/pages/{pageid}/imageurl")] HttpRequestMessage req,
            ExecutionContext context,
            string bookid, string pageid,
            ILogger log)
        {
            int pagenumber = Convert.ToInt32(pageid);

            CloudStorageAccount storageAccount;
            CloudBlobClient cloudBlobClient;
            CloudBlobContainer cloudBlobContainer;
            var fileName = String.Empty;

            //storage variables for secrets
            SecretBundle secrets;
            String cosmosEndpointUrl = String.Empty;
            String cosmosAuthorizationKey = String.Empty;
            String database = String.Empty;
            String collection = String.Empty;

            var container_images = "images";

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
            if (page == null) {
                log.LogError("Book's page not found.");
                return (ActionResult)new StatusCodeResult(404);
            }

            // =========================================================== //
            //                  GET method
            // =========================================================== //
            if (req.Method == HttpMethod.Get)
            {
                return (ActionResult)new OkObjectResult(page.Image_Url);
            }

            // =========================================================== //
            //                  POST method
            // =========================================================== //
            else if (req.Method == HttpMethod.Post) {
                //if (page.Image_Url == null) { // TODO: add this check back 
                // Retrieve the connection string for use with the application. The storage connection string is stored
                // in an environment variable on the machine running the application called StorageConnectionString.
                // If the environment variable is created after the application is launched in a console or with Visual
                // Studio, the shell or application needs to be closed and reloaded to take the environment variable into account.
                string storageConnectionString = config["StorageConnectionString"];

                    //if (!req.Content.IsMimeMultipartContent("form-data"))
                    //{
                    //    throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
                    //}

                    // Check whether the connection string can be parsed.
                    if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
                    {
                        try
                        {
                            log.LogInformation("Storage account accessed.");
                            // If the connection string is valid, proceed with operations against Blob storage here.
                            // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                            cloudBlobClient = storageAccount.CreateCloudBlobClient();

                            // Create a container called 'images' 
                            cloudBlobContainer = cloudBlobClient.GetContainerReference(container_images);

                            // TODO: Added so that we can create public access to the blob
                            await cloudBlobContainer.CreateIfNotExistsAsync(
                                      BlobContainerPublicAccessType.Blob,
                                      new BlobRequestOptions(),
                                      new OperationContext());

                            var provider = new AzureStorageMultipartFormDataStreamProvider(cloudBlobContainer);

                            try
                            {
                                await req.Content.ReadAsMultipartAsync(provider);
                            }
                            catch (Exception ex)
                            {
                                log.LogError("An error has occured. Details: " + ex.Message);
                                return (ActionResult)new StatusCodeResult(400);
                            }

                            // Retrieve the filename of the file you have uploaded
                            fileName = provider.FileData.FirstOrDefault()?.LocalFileName;
                            if (string.IsNullOrEmpty(fileName))
                            {
                                log.LogError("An error has occured while uploading your file. Please try again.");
                                return (ActionResult)new StatusCodeResult(500);
                            }


                        }
                        catch (StorageException ex)
                        {
                            log.LogInformation("Error returned from the service: {0}", ex.Message);
                            return (ActionResult)new StatusCodeResult(500);
                        }


                    }
                    else
                    {
                        // Otherwise, let the user know that they need to define the environment variable.
                        log.LogInformation("Key Vault access failed. A connection string has not been " +
                                           "defined in the system environment variables.");
                        return (ActionResult)new StatusCodeResult(500);

                }



                    var storageURI = config["STORAGE_URI"];
                    page.Image_Url = storageURI + container_images + "/" + fileName;

                    await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);

                    return (ActionResult)new OkObjectResult($"File: {fileName} has successfully uploaded");
                //} else { // TODO: check if the image exists
                //    // 
                //    log.LogInformation("Image already exists");
                //    return (ActionResult)new StatusCodeResult(409);
                //}
            }

            // =========================================================== //
            //                  PUT method
            // =========================================================== //
            else if (req.Method == HttpMethod.Put) { 
                if (page.Image_Url != null) { 
                    // TODO: 
                    // replace image by deleting first then upload new image
                }
            }

            // =========================================================== //
            //                  DELETE method
            // =========================================================== //
            else if (req.Method == HttpMethod.Delete) { 
                if (page.Image_Url != null) {

                    // Check whether the connection string can be parsed.
                    if (CloudStorageAccount.TryParse(config["StorageConnectionString"], out storageAccount))
                    {
                        try
                        {
                            log.LogInformation("Storage account accessed.");
                            // If the connection string is valid, proceed with operations against Blob storage here.
                            // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                            cloudBlobClient = storageAccount.CreateCloudBlobClient();

                            // Create a container called 'images' 
                            cloudBlobContainer = cloudBlobClient.GetContainerReference(container_images);
                            var pattern = "(\\{){0,1}[0-9a-fA-F]{8}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{4}\\-[0-9a-fA-F]{12}(\\}){0,1}";
                            var match = Regex.Match(page.Image_Url, pattern);
                            fileName = match.Value; 
                            var blockBlob = cloudBlobContainer.GetBlockBlobReference(fileName);

                            // Deleting the block blob
                            await blockBlob.DeleteIfExistsAsync();

                            // Setting the image url to null
                            page.Image_Url = null;
                            await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);
                            return (ActionResult)new OkObjectResult($"File: {fileName} has successfully deleted");

                        }
                        catch (StorageException ex)
                        {
                            log.LogError("Error returned from the service: {0}", ex.Message);
                            return (ActionResult)new StatusCodeResult(400);
                        }


                    }
                    else
                    {
                        // Otherwise, let the user know that they need to define the environment variable.
                        log.LogError("Key Vault access failed. A connection string has not been " +
                                           "defined in the system environment variables.");
                        return (ActionResult)new StatusCodeResult(500);

                    }

                } else {
                    log.LogError("No page image to delete.");
                    return (ActionResult)new StatusCodeResult(404);

                }
            }


            return (ActionResult)new OkObjectResult("OK");

        }

    }


}
