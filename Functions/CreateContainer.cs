using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using System;

namespace CreateContainer
{
    public static class CreateContainer
    {
        [FunctionName("CreateContainer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            log.LogInformation("---- starting container build function.");
            ProcessAsync(name, log, context).GetAwaiter().GetResult();


            return name != null
                ? (ActionResult)new OkObjectResult("Container created with the name \"" + name + "\"")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }


        private static async Task ProcessAsync(string containerName, ILogger log, ExecutionContext context)
        {
            log.LogInformation("---- in ProcessAsync function");

            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;


            //grab the environment variabled from the local.settings.json file
            string key_vault_uri = System.Environment.GetEnvironmentVariable("KEY_VAULT_URI");
            string storage_name = System.Environment.GetEnvironmentVariable("STORAGE_NAME");
            string storageConnectionString = System.Environment.GetEnvironmentVariable("CONNECTION_STRING");
            log.LogInformation("---- setting my local variables for the key vault");

            // Make a connection to key vault
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
            log.LogInformation("---- create a Key Vault client");

            // Grab the secrets from local.settings.json file
            //string connectionString = $"{config["KEY_VAULT_URI"]}/secrets/{config["STORAGE_NAME"]}/";
            string keyVaultURI = key_vault_uri + "/secrets/" + storage_name;
            log.LogInformation("---- create the key vault URI");


            try 
            {
                var secret = await keyVaultClient.GetSecretAsync(keyVaultURI);
                var storagePrimaryAccessKey = secret.Value;
                storageConnectionString = storagePrimaryAccessKey;
                log.LogInformation("----- connection string was retrieved from the Key Vault");
            }
            catch (Exception ex)
            {
                log.LogInformation("----- Cannot access the Key Vault.");
            }

            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    log.LogInformation("Key Vault accessed.");
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Fix the string to make it comply with the rules
                    containerName = containerName.ToLower();
                    containerName = containerName.Replace(" ", "-");

                    cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
                    await cloudBlobContainer.CreateAsync();

                    //// Set the permissions so the blobs are public. If commented out, the default should make it only readable to the owner of the storage account.
                    //BlobContainerPermissions permissions = new BlobContainerPermissions
                    //{
                    //    PrivateAccess = 
                    //    PublicAccess = BlobContainerPublicAccessType.Blob
                    //};
                    //await cloudBlobContainer.SetPermissionsAsync(permissions);

                }
                catch (StorageException ex)
                {
                    log.LogInformation("Error returned from the service: {0}", ex.Message);
                }

            }
            else
            {
                log.LogInformation("Key Vault access failed. A connection string has not been " +
                                    "defined in the system environment variables.");
            }
        }


    }

}

