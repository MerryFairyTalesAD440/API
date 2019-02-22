using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Extensions.Configuration;
using System.Net;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Text;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;

namespace Functions
{
    public static class getSAS
    {
        /// <summary>
        /// Azure function for generating a SAS token on a container
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [FunctionName("sastoken")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous,"post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("SAS token creation.");
            string containerName = String.Empty;
            //use configuration builder for variables
            //azure functions does not use configuration manager in .net core 2
            //key vault uri stored in local.settings.json file
            //key vault uri stored in function app settings after deployment 
            //variables stored in key vault for dev and prod
            var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            //uncomment for testing and at "get" to method
            //containerName = "getsastoken";
            containerName = data?.container;
            if (containerName != null)
            {
                //apply for key vault client
                var serviceTokenProvider = new AzureServiceTokenProvider();
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
                //storage variables for secrets
                SecretBundle secretUri;
                SecretBundle secrectKey;
                SecretBundle secretAccount;
                //try and get storage uri
                try
                {
                    secretUri = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/uri/");
                }
                //display unauthorize error.  Im not sure which code to return for this catch
                catch (KeyVaultErrorException ex)
                {
                    return new ForbidResult("Unable to access URI in vault!");
                }
                //try and get storage account name
                try
                {
                    secretAccount = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/account/");
                }
                //display unauthorize error.  Im not sure which code to return for this catch
                catch (KeyVaultErrorException ex)
                {
                    return new ForbidResult("Unable to access account name in vault!");
                }
                //try and get storage account key
                try
                {
                    secrectKey = await keyVaultClient.GetSecretAsync($"{config["KeyVaultUri"]}secrets/key/");
                }
                //display unauthorize error.  Im not sure which code to return for this catch
                catch (KeyVaultErrorException ex)
                {
                    return new ForbidResult("Unable to access key in vault!");
                }

                //set uri
                Uri address = new Uri(secretUri.Value.ToString() + containerName);
                StorageCredentials credentials = new StorageCredentials(secretAccount.Value.ToString(), secrectKey.Value.ToString());
                //apply credentials
                CloudBlobContainer name = new CloudBlobContainer(address, credentials);
                //check if container exists
                bool exist = await (name.ExistsAsync());

                //if container exists
                if (exist)
                {
                    String[] result = getContainerSasUri(name);
                    //return uri, sas token, and message
                    return (ActionResult)new OkObjectResult(new
                    {
                        uri = result[0],
                        token = result[1],
                        message = "SAS Token good for 60 minutes.  Token has Read/Write/Delete Privileges. File name should be appended in between uri and sas token on upload."
                    });
                }
                //else return bad request error
                else
                {
                    //return error telling user container doesnt exist
                    return new BadRequestObjectResult("Specified container does not exist!");

                }
            }
            else {
                //return error telling user wrong information passed in body
                return new BadRequestObjectResult("Wrong information passed in the body! For example: container:getsastoken");
            }



        }

        /// <summary> 
        /// helper function uses issue sas token on container passed in
        /// </summary>
        /// <param name="container"></param>
        /// <returns>string [] with uri and sas token</returns>
        private static string[] getContainerSasUri(CloudBlobContainer container)
        {
            string sasContainerToken;
            string[] result = new string[2];
            //create policy
            SharedAccessBlobPolicy adHocPolicy = new SharedAccessBlobPolicy()
            {
                //set sas token expiration and access policy
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(60),
                Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Delete
            };

            //generate sas token on container using adhoc policy
            sasContainerToken = container.GetSharedAccessSignature(adHocPolicy, null);
            result[0] = container.Uri.ToString();
            result[1] = sasContainerToken.ToString();
            // Return an array containing the uri and sas token
            return result;
        }

    }

}
