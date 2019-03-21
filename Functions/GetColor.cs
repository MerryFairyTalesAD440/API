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

namespace Functions
{
    //Class to get all colors from test CosmosDB. 
    public static class GetColors
    {
        [FunctionName("getColors")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestMessage req,
            ExecutionContext context,
            ILogger log)
        {
            if (req.Method == HttpMethod.Post) {
                return (ActionResult)new StatusCodeResult(405);
            }

            //Setup a ConfigurationBuilder to pull config values from Application Settings.
            var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();

            //Setup an Azure Service Token Provider as a part of gaining access to the Key Vault.
            AzureServiceTokenProvider azureServiceTokenProvider = new AzureServiceTokenProvider();

            //Setup a Cosmos Object containing DB information
            CosmosDatabase cosmosDatabase = new CosmosDatabase();

            try
            {

                var keyVaultClient = new KeyVaultClient(
                    new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));

                //Pull the SecretsBundle containing all CosmosDB information stored as a JSON object: { URI, Primary Key, Database Name, Collection Name }
                var secret = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_KEY_VAULT_NAME"]}/");

                //The CosmosDB object is stored in the SecretBundle as "Value". The following sets
                //the CosmosDatabase object values to match those stored in the key vault.
                JObject details = JObject.Parse(secret.Value.ToString());
                cosmosDatabase.COSMOS_URI = (string)details["COSMOS_URI"];
                cosmosDatabase.COSMOS_KEY = (string)details["COSMOS_KEY"];
                cosmosDatabase.COSMOS_DB = (string)details["COSMOS_DB"];
                cosmosDatabase.COSMOS_COLLECTION = (string)details["COSMOS_COLLECTION"];

                log.LogInformation("Secret retreived from key vault.");

            }   
            //Throw an error if key vault access or parsing fails.
            catch (Exception ex) {
                log.LogError(ex.Message);
                return new ForbidResult("Unable to access secrets in vault!" + ex.Message);
            }


            //Connect to the Cosmos Database and return all stored colors.
            try
            {
                DocumentClient client = new DocumentClient(new Uri(cosmosDatabase.COSMOS_URI), cosmosDatabase.COSMOS_KEY);
                
                FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1, EnableCrossPartitionQuery = true };

                IQueryable<Colors> queryColors = client.CreateDocumentQuery<Colors>(
                    UriFactory.CreateDocumentCollectionUri(cosmosDatabase.COSMOS_DB, cosmosDatabase.COSMOS_COLLECTION), queryOptions);

                List<Colors> colorsList = queryColors.ToList<Colors>();
                string allColors = JsonConvert.SerializeObject(colorsList, Formatting.Indented);

                log.LogInformation("Returning all colors stored in database: ");
                return (ActionResult)new OkObjectResult(allColors);
                return (ActionResult)new OkObjectResult(true);

            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
                return (ActionResult)new StatusCodeResult(500);
            }

        }
            
    }
}
