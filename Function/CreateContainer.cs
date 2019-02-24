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



namespace CreateContainer
{
    public static class CreateContainer
    {
        [FunctionName("CreateContainer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequest req, ILogger log)
        {
            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            log.LogInformation("starting container build function.");
            ProcessAsync(name, log).GetAwaiter().GetResult();


            return name != null
                ? (ActionResult)new OkObjectResult("Container created with the name \"" + name + "\"")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }


        private static async Task ProcessAsync(string containerName, ILogger log)
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;

            log.LogInformation("In ProcessAsync function, attemping to access Key Vault.");
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
                    log.LogInformation("Key Vault accessed.");
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

