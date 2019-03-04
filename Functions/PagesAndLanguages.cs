using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", "post", Route = "books/{bookid}/pages/{pageid}/language/{languagecode}")] HttpRequestMessage req, ILogger log, string bookid, string pageid, string languagecode, ExecutionContext context)
        {
            log.LogInformation("Http function to put/post page and language");
            string requestBody = await req.Content.ReadAsStringAsync();
            //declare client
            DocumentClient client;
            //declare query
            IQueryable<Book> bookQuery;
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
                //set book
                Book book = new Book();
                book.Author = data?.author;
                book.Id = data?.id;
                //if pages are an array
                try
                {
                    book.Pages = data?.pages.ToObject<List<Page>>();
                }
                //if a single page
                catch (Exception ex)
                {
                    List<Page> pages = new List<Page> { data?.pages.ToObject<Page>() };
                    book.Pages = pages;
                }

                book.Cover_Image = data?.cover_image;
                book.Description = data?.description;
                book.Title = data?.title;

                if (!routeBookMatches(bookid, pageid, languagecode, data))
                {
                    return (ActionResult)new BadRequestObjectResult("Route information does not match book!.");
                }

                // if post
                if (req.Method == HttpMethod.Post)
                {
                    //check if book is returned
                    if (returnsValue<Book>(bookQuery))
                    {
                        Book bookReturned = new Book();
                        foreach (Book b in bookQuery)
                        {

                            bookReturned = b;
                        }

                        // if the page exists
                        if (bookReturned.Pages.Find(x => x.Number.Contains(pageid)) != null)
                        {
                            Page p = bookReturned.Pages.Find(y => y.Number.Contains(pageid));
                            //if the language doesnt exist
                            if (p.Languages.Find(z => z.language.Contains(languagecode)) == null)
                            {
                                try
                                {
                                    //create document
                                    await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(database, collection), book);
                                    return (ActionResult)new OkObjectResult("Language successfully added for page.");
                                }
                                catch (Exception ex)
                                {
                                    return (ActionResult)new StatusCodeResult(500);
                                }

                            }
                            //else return conflict since book/page/language already exists
                            return (ActionResult)new StatusCodeResult(409);
                        }
                        else
                        {
                            return (ActionResult)new NotFoundObjectResult(new { message = "Page ID not found" });
                        }
                    }
                    else {
                        return (ActionResult)new NotFoundObjectResult(new { message = "Book ID not found" });
                    }

                }

                //if put
                else if (req.Method == HttpMethod.Put)
                {
                    //check if book is returned
                    if (returnsValue<Book>(bookQuery))
                    {
                        Book bookReturned = new Book();
                        foreach (Book b in bookQuery)
                        {
                            bookReturned = b;
                        }
                        if (bookReturned.Pages.Find(x => x.Number.Contains(pageid)) != null)
                        {
                            Page p = bookReturned.Pages.Find(y => y.Number.Contains(pageid));
                            if (p.Languages.Find(z => z.language.Contains(languagecode)) != null)
                            {
                                try
                                {
                                    //update document in db if route variables and returned book matches
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
                                return (ActionResult)new NotFoundObjectResult(new { message = "Language code not found" });
                            }

                        }
                        else
                        {
                            return (ActionResult)new NotFoundObjectResult(new { message = "Page ID not found" });
                        }

                    }

                    else
                    {

                        return (ActionResult)new NotFoundObjectResult(new { message = "Book ID not found." });
                    }
                }

                return (ActionResult)new StatusCodeResult(405);

            }
            else
            {
                return (ActionResult)new BadRequestObjectResult("Could not complete request. Missing or invalid information provided.");
            }
        }

        // TODO:  validate path variables and book contents match 
        /// <summary>
        /// Checking if the proper information was passed
        /// </summary>
        /// <param name="data">dynamic data passed in body</param>
        /// <returns>boolean</returns>
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
        /// <typeparam name="T">Object type</typeparam>
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
        /// <summary>
        /// Makes sure route info matches book.
        /// </summary>
        /// <param name="bookid">route book id</param>
        /// <param name="pageid">route page id</param>
        /// <param name="languagecode">coute language code</param>
        /// <param name="data">dynamic data posted in body</param>
        /// <returns>boolean</returns>
        public static bool routeBookMatches(string bookid, string pageid, string languagecode, dynamic data)
        {
            bool result = false;
            //set book
            Book book = new Book();
            book.Author = data?.author;
            book.Id = data?.id;
            //if pages are an array
            try
            {
                book.Pages = data?.pages.ToObject<List<Page>>();
            }
            //if a single page
            catch (Exception ex)
            {
                List<Page> pages = new List<Page> { data?.pages.ToObject<Page>() };
                book.Pages = pages;
            }

            book.Cover_Image = data?.cover_image;
            book.Description = data?.description;
            book.Title = data?.title;
            if (book.Id.CompareTo(bookid) == 0)
            {
                if (book.Pages.Find(x => x.Number.Contains(pageid)) != null)
                {
                    Page p = book.Pages.Find(y => y.Number.Contains(pageid));
                    if (p.Languages.Find(z => z.language.Contains(languagecode)) != null)
                    {
                        result = true;
                    }
                }
            }
            return result;
        }

    }
}
