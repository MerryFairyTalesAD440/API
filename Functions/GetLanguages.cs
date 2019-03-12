using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Documents.Client;
using System.Linq;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Functions
{
    public static class GetLanguages
    {
        [FunctionName("GetLanguages")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/{bookid}/pages/{pageid}/language/")] HttpRequestMessage req,
            ILogger log, ExecutionContext context, string bookid, string pageid)
        {
            log.LogInformation("Get Languages for book.");
            if (req.Method != HttpMethod.Get)
            {
                return (ActionResult)new StatusCodeResult(405);
            }
            //not fool proof but will work for now
            bookid = bookid.Replace(" ", "");
            pageid = pageid.Replace(" ", "");
            string requestBody = await req.Content.ReadAsStringAsync();
            //declare client
            DocumentClient client;

            //declare query
            IQueryable<Book> bookQuery;

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

            //apply for key vault client
            var serviceTokenProvider = new AzureServiceTokenProvider();
            var keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(serviceTokenProvider.KeyVaultTokenCallback));

            //storage variables for secrets
            SecretBundle secrets;
            String uri = String.Empty;
            String key = String.Empty;
            String database = String.Empty;
            String collection = String.Empty;
            //try and get storage uri

            try
            {
                //storage account is the keyvault key
                secrets = await keyVaultClient.GetSecretAsync($"{config["KEY_VAULT_URI"]}secrets/{config["COSMOS_NAME"]}/");
                //parse json stored in keyvalut
                JObject details = JObject.Parse(secrets.Value.ToString());
                uri = (string)details["COSMOS_URI"];
                key = (string)details["COSMOS_KEY"];
                database = (string)details["COSMOS_DB"];
                collection = (string)details["COSMOS_COLLECTION"];
            }

            //display unauthorize error.  Im not sure which code to return for this catch
            catch (KeyVaultErrorException ex)
            {
                return new ForbidResult("Unable to access secrets in vault!");
            }

            //set options client and query
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
            client = new DocumentClient(new Uri(uri), key);
            try
            {
                //set book query.  search for book id
                bookQuery = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri(database, collection),
                "SELECT a.id, a.title, a.description, a.author, a.pages FROM Books a  WHERE a.id = \'" + bookid + "\'", queryOptions);
            }
            catch (Exception ex)
            {
                return (ActionResult)new StatusCodeResult(500);
            }

            if (returnsValue<Book>(bookQuery))
            {
                Book bookReturned = bookQuery.ToList<Book>()[0];

                if (bookReturned.Pages.Count > 0)
                {
                    Page page = bookReturned.Pages.Find(x => x.Number.Contains(pageid));
                    if (page.Languages.Count > 0)
                    {
                        List<Language> languages = page.Languages;
                        string allLanguages = JsonConvert.SerializeObject(languages, Formatting.Indented);
                        return (ActionResult)new OkObjectResult(allLanguages);
                    }
                }
                else {
                    return (ActionResult)new OkObjectResult(new { });
                }

            }
            else {

                return (ActionResult)new OkObjectResult(new { });
            }



            return (ActionResult)new StatusCodeResult(500);

            }
        /// <summary>
        /// Checks if a value has been returned by iqueryable
        /// </summary>
        /// <typeparam name="T">Oject Type</typeparam>
        /// <param name="enumerable">Book Query</param>
        /// <returns>boolean</returns>
        public static bool returnsValue<T>(this IEnumerable<T> enumerable)
        {
            try
            {
                return !enumerable.FirstOrDefault().Equals(default(T));
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
