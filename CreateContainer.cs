
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;


namespace CreateContainer
{
    public static class CreateContainer
    {
        [FunctionName("CreateContainer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            // "name" is the query string -> ?name=
            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            ProcessAsync(name).GetAwaiter().GetResult();


            return name != null
                ? (ActionResult)new OkObjectResult("Container created with the name \"" + name + "\"")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");


        }

        private static async Task ProcessAsync(string containerName)
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;

            // Code to grab the key from the Key Vault.
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            var secret = await keyVaultClient.GetSecretAsync("https://key-vault-for-container.vault.azure.net/secrets/connection-string/");
            string storageConnectionString = secret.Value.ToString();


            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Fix the string to make it comply with the rules
                    containerName = containerName.ToLower();
                    containerName = containerName.Replace(" ", "-");

                    cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
                    await cloudBlobContainer.CreateAsync();

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);

                }
                catch (StorageException ex)
                {
                    // Fix - log this instead 
                    Console.WriteLine("Error returned from the service: {0}", ex.Message);
                }

            }
            else
            {
                    // fix - log this instead
                Console.WriteLine(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value.");
            }
        }


    }

}

