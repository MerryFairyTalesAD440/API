
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

            Console.WriteLine("============ Creating blob storage ============");
            Console.WriteLine();
            ProcessAsync(name).GetAwaiter().GetResult();

            Console.WriteLine("Press \"Control + C\" to exit.");

            return name != null
                ? (ActionResult)new OkObjectResult("Container created with the name \"" + name + "\"")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");


            //Console.ReadLine();
            //Console.WriteLine("===============================================");

        }

        private static async Task ProcessAsync(string containerName)
        {
            CloudStorageAccount storageAccount = null;
            CloudBlobContainer cloudBlobContainer = null;
            //string sourceFile = null;
            //string destinationFile = null;

            // Retrieve the connection string for use with the application. The storage connection string is stored
            // in an environment variable on the machine running the application called storageconnectionstring.
            // If the environment variable is created after the application is launched in a console or with Visual
            // Studio, the shell needs to be closed and reloaded to take the environment variable into account.
            //string storageConnectionString = Environment.GetEnvironmentVariable("storageconnectionstring");
            string storageConnectionString = "<OR-YOU-CAN-PLACE-THE-STORAGE-STRING-DIRECTLY-RIGHT-HERE>";

            // Check whether the connection string can be parsed.
            if (CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
            {
                try
                {
                    // Create the CloudBlobClient that represents the Blob storage endpoint for the storage account.
                    CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

                    // Create a name for the container
                    //Console.Write("---> Enter name of book: ");
                    //string containerName = Console.ReadLine();

                    // Fix the string to make it comply with the rules
                    containerName = containerName.ToLower();
                    containerName = containerName.Replace(" ", "-");

                    // Create a container called '...' and append a GUID value to it to make the name unique. 
                    // if we want to add an GUID to the end of the book container. Then append  --> + "-"  + Guid.NewGuid().ToString()
                    cloudBlobContainer = cloudBlobClient.GetContainerReference(containerName);
                    await cloudBlobContainer.CreateAsync();

                    // Let it be known the container name appended with the GUID
                    Console.WriteLine("... Created container '{0}'\n", cloudBlobContainer.Name);

                    // Set the permissions so the blobs are public. 
                    BlobContainerPermissions permissions = new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    };
                    await cloudBlobContainer.SetPermissionsAsync(permissions);
                    
                }
                catch (StorageException ex)
                {
                    Console.WriteLine("Error returned from the service: {0}", ex.Message);
                }

            }
            else
            {
                Console.WriteLine(
                    "A connection string has not been defined in the system environment variables. " +
                    "Add a environment variable named 'storageconnectionstring' with your storage " +
                    "connection string as a value.");
            }
        }

    }
}

