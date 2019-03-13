using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Documents.Client;
using System.Net.Http;
using System.Net;
using Microsoft.Azure.Documents;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Configuration;

namespace Functions
{
    public static class GetDeleteLanguage
    {
        [FunctionName("GetDeleteLanguage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "delete", Route = "books/{bookid}/pages/{pageid}/language/{languagecode}")] HttpRequestMessage req, string bookid, string pageid, string languagecode, ILogger log, ExecutionContext context)
        {

            // convert "pageid" from string to integer
            int pagenumber = Convert.ToInt32(pageid);

            //set configuration
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

            // =====================================================================================================
            //                                          CONNECT TO COSMOS DB
            // =====================================================================================================
            DocumentClient client    = new DocumentClient(new Uri(uri), key);
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
            var collectionLink       = UriFactory.CreateDocumentCollectionUri(database, collection);
            var query                = "SELECT * FROM Books b WHERE b.id = \'" + bookid + "\'";
            var document             = client.CreateDocumentQuery(collectionLink, query, queryOptions).ToList();
            log.LogInformation(document.Count.ToString());

            // =====================================================================================================
            //                                             VALIDATE INPUT
            // =====================================================================================================
            if (document.Count == 0) { return (ActionResult)new StatusCodeResult(404); }

            Book oBook = document.ElementAt(0);

            // resource not found 
            if (oBook.Id == null) { return (ActionResult)new StatusCodeResult(404); }

            // Bad page input
            if (oBook.Pages.Find(x=>x.Number.Contains(pageid)) == null)
            {
                return (ActionResult)new StatusCodeResult(404);
            }

            //check if languages are null
            Page page = oBook.Pages.Find(y => y.Number.Contains(pageid));
            if (page != null && page.Languages[0] == null)
            {
                return (ActionResult)new StatusCodeResult(404);
            }

                // No resource found with that language if language array isnt null
                if (oBook.Pages.Find(y=>y.Number.Contains(pageid)).Languages.Find(z => z.language.Contains(languagecode)) == null)
            {
                return (ActionResult)new StatusCodeResult(404);
            }

            // ========================================================================================================
            // ============================================   IF - GET   ==============================================
            // ========================================================================================================
            if (req.Method == HttpMethod.Get)
            {
                log.LogInformation("----- function GET was executed");

                // =====================================================================================================
                //                                         DISPLAY RESULTS 
                // =====================================================================================================
                if (oBook.Title != null)
                {
                    // grab the language
                    string language = JsonConvert.SerializeObject(oBook.Pages.Find(a => a.Number.Contains(pageid))
                        .Languages.Find(b =>b.language.Contains(languagecode)), Formatting.Indented);
                    return (ActionResult)new OkObjectResult(language);
                }
                else
                {
                    return (ActionResult)new StatusCodeResult(404); // resource not found
                }
            }

            // ===========================================================================================================
            // =========================================   ELSE IF - DELETE   ============================================
            // ===========================================================================================================
            else if (req.Method == HttpMethod.Delete)
            {
                log.LogInformation("----- function DELETE was executed");

                // =====================================================================================================
                //                                         CREATE A NEW BOOK
                // =====================================================================================================
                Book nBook = new Book();

                nBook.Id          = oBook.Id;
                nBook.Cover_Image = oBook.Cover_Image;
                nBook.Author      = oBook.Author;
                nBook.Description = oBook.Description;
                nBook.Title       = oBook.Title;
                nBook.Pages       = oBook.Pages;

                //remove language from page
                List<Language> langArr = nBook.Pages.Find(c => c.Number.Contains(pageid)).Languages;
                langArr.RemoveAll(d => d.language == languagecode);
                nBook.Pages.Find(e => e.Number.Contains(pageid)).Languages = langArr;

                // =====================================================================================================
                //                                         UPSERT TO COSMOS DB
                // =====================================================================================================
                var result = await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), nBook);
                log.LogInformation($"{result}");

                return (ActionResult)new OkObjectResult(new { message = "Language successfully removed."});
            }
            //not really needed, but I need a return statement
            else
            {
                return (ActionResult)new StatusCodeResult(404);
            }
        }
    }
}
