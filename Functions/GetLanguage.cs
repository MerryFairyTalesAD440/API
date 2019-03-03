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
using System.Net.Http;
using System.Net;
using Microsoft.Azure.Documents;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Functions
{
    public static class GetDeleteLanguages
    {
        [FunctionName("GetDeleteLanguages")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/{bookid}/pages/{pagenum}/languages/{code}")] HttpRequest req, string bookid, string pagenum, string code, ILogger log, ExecutionContext context)
        {
            log.LogInformation("----- function to get the pages and languages from a book");

            string cosmosURI = System.Environment.GetEnvironmentVariable("CosmosURI");
            string cosmosKey = System.Environment.GetEnvironmentVariable("CosmosKey");
            int pagenumber = Convert.ToInt32(pagenum);

            DocumentClient client = new DocumentClient(new Uri(cosmosURI), cosmosKey);


            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };


            IQueryable<Book> bookQuery = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri("MerryFairyTalesDB", "Books"),
                  "SELECT a.id, a.title, a.description, a.author, a.pages " +
                  "FROM Books a " +
                  "JOIN b IN a.pages " +
                  "JOIN c IN b.languages  " +
                  "WHERE a.id = \'" + bookid + "\'",
                  queryOptions);

            // Set some common query options
            //FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };

            //IQueryable<Book> bookQuery = client.CreateDocumentQuery<Book>(
            //UriFactory.CreateDocumentCollectionUri("MerryFairyTalesDB", "Books"), queryOptions)
            //.Where(f => f.Title == bookid);
             

            Book bookFromObject = new Book();
            // Go through the object and collect the data.
            foreach (Book b in bookQuery)
            {
                bookFromObject.Title = b.Title;
                bookFromObject.Cover_Image = b.Cover_Image;
                bookFromObject.Author = b.Author;
                bookFromObject.Description = b.Description;
                bookFromObject.Id = b.Id;
                bookFromObject.Pages = b.Pages;
            }

            // no matching page number
            if (pagenumber < 1 || pagenumber > bookFromObject.Pages.Count())
            {
                return new BadRequestObjectResult("Page not found");
            }

            //length of the json languahe array
            int len = bookFromObject.Pages[pagenumber - 1].Languages.Count();
            int idxOfLangCode = -1;

            //search for the index of the language
            for (int i = 0; i < len; i++)
            {
                // if they match, save the index
                if (bookFromObject.Pages[pagenumber - 1].Languages[i].language.ToLower().Equals(code)) 
                {
                    idxOfLangCode = i;
                    break;
                }

            }

            //no matching language
            if (idxOfLangCode == -1)
            {
                return new BadRequestObjectResult("Language not found");
            }


            if (bookFromObject.Title != null)
            {
                //the Pages[] is indexed from 0 and the pages start at 1, so I minus one to counter it
                string pages = JsonConvert.SerializeObject(bookFromObject.Pages[pagenumber - 1].Languages[idxOfLangCode], Formatting.Indented);
                return (ActionResult)new OkObjectResult(pages);
                //log.LogInformation(JsonConvert.SerializeObject(bookFromObject.Pages, Formatting.Indented));
            }
            else
            {
                return new BadRequestObjectResult("Book not found");
            }


            //TODO: update keys in Azure.
        }
    }
}
