
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
using MongoDB.Driver;
using System.Security.Authentication;
using MongoDB.Bson;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

//Function to insert a new document into CosmosDB MongoDB API accepting a POST request
//@author Francesco Ward
namespace writeDoc
{
    public static class writeDoc
    {
        [FunctionName("writeDoc")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, ExecutionContext context)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                //get POST body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                log.LogInformation($"data -> {data}" );

                //get environment variables
                /*
                var config = new ConfigurationBuilder()
                        .SetBasePath(context.FunctionAppDirectory)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .Build();
                */                      

                //access azure keyvault
                var serviceTokenProvider = new AzureServiceTokenProvider();
                log.LogInformation("serviceTokenProvider");
                var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));
                log.LogInformation("keyVaultClient");

                SecretBundle secretValue;
                try
                {
                    secretValue = await keyVaultClient.GetSecretAsync("https://francesco-key-vault.vault.azure.net/secrets/mongodb/");
                    log.LogInformation("Secret Value retrieved from KeyVault.");
                }
                catch(Exception kex)
                {
                    return (ObjectResult)new ObjectResult(kex.Message.ToString());
                }

                //connect to MongoDB
                string connectionString = $@"{secretValue.Value}";
                MongoClientSettings settings = MongoClientSettings.FromUrl(
                  new MongoUrl(connectionString)
                );
                log.LogInformation($"connection established");

                //enable SSL
                settings.SslSettings =
                  new SslSettings() { EnabledSslProtocols = SslProtocols.Tls12 };
                var mongoClient = new MongoClient(settings);
                log.LogInformation($"SSL enabled");

                //get db handle
                IMongoDatabase db = mongoClient.GetDatabase("outDatabase");
                log.LogInformation($"DB handle aquired");

                //create collection
                //await db.CreateCollectionAsync("Books");
                //log.LogInformation($"Collection created");

                //create document
                var document = new BsonDocument
                {
                    {"id", BsonValue.Create(data["id"].ToString())},
                    {"title", BsonValue.Create(data["title"].ToString())},
                    {"description", BsonValue.Create(data["description"].ToString())},
                    {"author", BsonValue.Create(data["author"].ToString())}
                };
                log.LogInformation($"document created");

                //get collection handle we just created ??
                var collection = db.GetCollection<BsonDocument>("Books");

                //insert into db
                await collection.InsertOneAsync(document);
                log.LogInformation($"document inserted -> {document}");

                return (ActionResult)new OkObjectResult($"200ok, DB write successful -> , {data}");
            }
            catch(Exception e)
            {
                return (ActionResult)new BadRequestObjectResult("400");
            }
        }
    }
}
