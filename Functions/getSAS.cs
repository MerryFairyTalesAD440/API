using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Google.Apis.Auth;
using Google.Apis.Auth.OAuth2;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace Functions
{
    public static class GetSAS
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", "post", "delete", "get", Route = null)] HttpRequestMessage req,
            ILogger log, ExecutionContext context)
        {

            log.LogInformation("SAS token creation.");
            //only allow post methods
            if (req.Method != HttpMethod.Post)
            {    
                return (ActionResult)new StatusCodeResult(405);
            }
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
            string requestBody = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            //uncomment for testing and add "get" to method
            //containerName = "merryfairytalesassets";
            containerName = data?.container;
            if (containerName != null)
            {
                //apply for key vault client
                var serviceTokenProvider = new AzureServiceTokenProvider();
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
                //storage variables for secrets
                SecretBundle secrets;
                String uri = String.Empty;
                String key = String.Empty;
                //try and get storage uri
                try
                {
                    //storage account is the keyvault key
                    secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["STORAGE_NAME"]}/");
                    //parse json stored in keyvalut
                    JObject details = JObject.Parse(secrets.Value.ToString());
                    uri = (string)details["uri"];
                    key =(string) details["key"];
                }
                //display unauthorize error.  Im not sure which code to return for this catch
                catch (KeyVaultErrorException ex)
                {
                    return new ForbidResult("Unable to access secrets in vault!" + ex.Message.ToString());
                }
                //set uri
                Uri address = new Uri(uri + containerName);
                StorageCredentials credentials = new StorageCredentials($"{config["STORAGE_NAME"]}", key);
                //apply credentials
                CloudBlobContainer name = new CloudBlobContainer(address, credentials);
                //check if container exists
                bool exist = await (name.ExistsAsync());

                //if container exists
                if (exist)
                {
                    String[] result = getContainerSasUri(name);
                    var obj = new { uri = result[0], token = result[1], message = "SAS Token good for 60 minutes.  Token has Read/Write/Delete Privileges. File name should be appended in between uri and sas token on upload." };
                    var jsonToReturn = JsonConvert.SerializeObject(obj, Formatting.Indented);
                    //return uri, sas token, and message
                    return (ActionResult)new OkObjectResult(jsonToReturn);
                 
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
                return new BadRequestObjectResult("Wrong information passed in the body! For example: container:name-of-container");
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
