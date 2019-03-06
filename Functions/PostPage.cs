using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Documents.Client;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;

namespace Functions
{
    public static class PostPages
    {
        [FunctionName("PostPage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "books/{bookid}/pages/")] HttpRequestMessage req, ExecutionContext context,
            ILogger log, string bookid)
        {
            log.LogInformation("Http function to put/post language");
            //declare client
            DocumentClient client;
            //declare query
            IQueryable<Book> bookQuery;
            //not fool proof but will work for now
            bookid = bookid.Replace(" ", "");
            //only allow post methods
            if (req.Method != HttpMethod.Post)
            {
                return (ActionResult)new StatusCodeResult(405);
            }
           
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
            List<Book> books = bookQuery.ToList<Book>();

            if (books.Count == 0)
            {
                return (ActionResult)new NotFoundObjectResult(new { message = "Book ID not found" });
            }
            else
            {
                //get the page array length
                int length = books[0].Pages.Count;
                //create a new page with length +1
                Page page = new Page();
                length += 1;
                page.Number = length.ToString();
                books[0].Pages.Add(page);
                //update document in db if route variables and returned book matches
                await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), books[0]);
                //return page number 
                return (ActionResult)new OkObjectResult(new {message = "Page added to book: " + bookid ,  page = length.ToString() });

            }

        }
    }
}
