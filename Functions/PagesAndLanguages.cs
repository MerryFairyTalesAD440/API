using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MongoDB.Driver;
using Microsoft.Azure.Documents.Client;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json.Linq;

namespace Functions
{
    public static class PagesAndLanguages
    {
        [FunctionName("pagesandlanguages")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous,"put", "post", "delete", "get", Route = "books/{bookid}/pages/{pageid}/language/{languagecode}")] HttpRequestMessage req, ILogger log, string bookid, string pageid, string languagecode, ExecutionContext context)
        {
            log.LogInformation("Http function to put/post page and language");
        
            if (req.Method == HttpMethod.Delete || req.Method == HttpMethod.Get)
            {
                return (ActionResult)new StatusCodeResult(405);
            }
           

            string requestBody = await req.Content.ReadAsStringAsync();
            //declare client
            DocumentClient client;          
            //not fool proof but will work for now
            bookid = bookid.Replace(" ", "");
            pageid = pageid.Replace(" ", "");
            languagecode = languagecode.Replace(" ", "");
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
            //TODO: Get data from post/put rather than reading json from a file
            //dynamic data = JsonConvert.DeserializeObject(System.IO.File.ReadAllText(@"C:\Users\mvien\desktop\sample.json"));
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            //TODO: Make sure all return paths from the swagger document are impletmented
            //validate json
            if (validDocument(data))
            {
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
             
                //comos sql is a little different
                IQueryable<Book> bookQuery = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri(database, collection),
                  "SELECT a.id, a.title, a.description, a.author, a.pages FROM Books a JOIN b IN a.pages JOIN c IN b.languages  WHERE a.id = \'" + bookid + "\' AND b.number = \'"
                  + pageid + "\' AND c.language = \'" + languagecode + "\'",
                  queryOptions);
              
                //set book
                Book book = new Book();
                book.Author = data?.author;
                book.Id = data?.id;
                book.Pages = data?.pages.ToObject<List<Page>>();
                book.Cover_Image = data?.cover_image;
                book.Description = data?.description;
                book.Title = data?.title;
              
                // if post
                if (req.Method == HttpMethod.Post)
                {
                    //check if book is returned
                    if (returnsValue<Book>(bookQuery))
                    {
                        //return conflict since book already exists
                        return (ActionResult)new StatusCodeResult(409);
                    }
                    else
                    {
                        try
                        {
                            //create document
                            await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);
                            return (ActionResult)new OkObjectResult("Language successfully added for page.");
                        }
                        catch (Exception ex)
                        {
                            return (ActionResult)new StatusCodeResult(500);
                        }
                    }

                }
            
                //if put
                if (req.Method == HttpMethod.Put)
                {
                    //check if book is returned
                    if (returnsValue<Book>(bookQuery))
                    {
                        try
                        {
                            //update document in db
                            await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);
                            return (ActionResult)new OkObjectResult("Page language successfully updated.");
                        }
                        catch (Exception ex)
                        {
                            return (ActionResult)new StatusCodeResult(500);
                        }
                    }
                    else
                    {
                        return (ActionResult)new NotFoundObjectResult(new { message = "Book ID, page ID, or language code not found." });
                    }

                }
                else {
                    return (ActionResult)new StatusCodeResult(405);
                }
            }
            else
            {
                return (ActionResult)new BadRequestObjectResult("Json sent in wrong format or without the required information!");
            }
           

        }

        // TODO:  validate path variables and book contents match 
        /// <summary>
        /// Checking if the proper information was passed
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool validDocument(dynamic data)
        {
            bool valid = false;
            if (data?.title != null && data?.description != null && data?.author != null
                && data?.cover_image != null && data?.pages != null)
            {
                valid = true;
            }
            return valid;
        }
      
        /// <summary>
        /// Checks if a value has been returned by iqueryable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
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
