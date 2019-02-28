using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using MongoDB.Driver;
using System.Security.Authentication;
using MongoDB.Bson;
using Microsoft.Azure.Documents.Client;
using System.Linq;
using MongoDB.Driver.Linq;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Functions
{
    public static class PagesAndLanguages
    {
        [FunctionName("pagesandlanguages")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", "post", Route = "books/{bookid}/pages/{pageid}/language/{languagecode}")] HttpRequestMessage req, ILogger log, string bookid, string pageid, string languagecode, ExecutionContext context)
        {
            log.LogInformation("Http function to put page and language");
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
            //TODO: Set up key vault with variables and change config to look for key vault url
            //TODO: Get data from post/put rather than reading json from a file
            dynamic data = JsonConvert.DeserializeObject(System.IO.File.ReadAllText(@"C:\Users\mvien\desktop\sample.json"));
            
            //TODO: Make sure all return paths from the swagger document are impletmented
            //validate json
            if (validDocument(data))
            {
                //adding comment to test push
                //set options client and query
                FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
                client = new DocumentClient(new Uri($"{config["COSMOS_URI"]}"), $"{config["COSMOS_KEY"]}");
                IQueryable<Book> bookQuery = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri($"{config["COSMOS_DB"]}", $"{config["COSMOS_COLLECTION"]}"),
                  "SELECT a.id, a.title, a.description, a.author, a.pages FROM Books a JOIN b IN a.pages JOIN c IN b.languages  WHERE a.id = \'" + bookid + "\' AND b.number = \'"
                  + pageid + "\' AND c.language = \'" + languagecode + "\'",
                  queryOptions);
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
                            //set book
                            Book newBook = new Book();
                            newBook.Author = data?.author;
                            newBook.Id = data?.id;
                            newBook.Pages = data?.pages.ToObject<List<Page>>();
                            newBook.Cover_Image = data?.cover_image;
                            newBook.Description = data?.description;
                            newBook.Title = data?.title;
                            //create document
                            await client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri($"{config["COSMOS_DB"]}", $"{config["COSMOS_COLLECTION"]}"), newBook);
                            return (ActionResult)new OkObjectResult("Language successfully added for page.");
                        }
                        catch (Exception ex)
                        {
                            return (ActionResult)new StatusCodeResult(500);
                        }
                    }

                }
                //if post
                if (req.Method == HttpMethod.Put)
                {
                    //check if book is returned
                    if (returnsValue<Book>(bookQuery))
                    {
                        try
                        {
                            //set book
                            Book newBook = new Book();
                            newBook.Author = data?.author;
                            newBook.Id = data?.id;
                            newBook.Pages = data?.pages.ToObject<List<Page>>();
                            newBook.Cover_Image = data?.cover_image;
                            newBook.Description = data?.description;
                            newBook.Title = data?.title;
                            //update document in db
                            await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri($"{config["COSMOS_DB"]}", $"{config["COSMOS_COLLECTION"]}"), newBook);
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
                //i love linq
                return !enumerable.FirstOrDefault().Equals(default(T)) && !enumerable.Skip(1).Any();
            }
            catch (Exception ex)
            {
                return false;
            }
        }

    }
}
