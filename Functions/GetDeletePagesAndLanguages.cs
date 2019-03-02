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
    public static class GetDeletePagesAndLanguages
    {
        [FunctionName("GetDeletePagesAndLanguages")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req, ILogger log, ExecutionContext context)
        {
            log.LogInformation("----- function to get the pages and languages from a book");
        
            string cosmosURI = System.Environment.GetEnvironmentVariable("CosmosURI");
            string cosmosKey = System.Environment.GetEnvironmentVariable("CosmosKey");


            DocumentClient client = new DocumentClient(new Uri(cosmosURI), cosmosKey);
           

            //pass in the query parameters as such EX: "?title=Snow White&page="
            string bookTitle = req.Query["title"];
            string page = req.Query["page"];
            string lang = req.Query["lang"];


            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };


            IQueryable<Book> bookQuery = client.CreateDocumentQuery<Book>(
                 UriFactory.CreateDocumentCollectionUri("MerryFairyTalesDB", "Books"), queryOptions)
                 .Where(f => f.Title == bookTitle);

            Book bookFromObject = new Book();
            // Go through the object and collect the data.
            foreach (Book b in bookQuery)
            {
                //Console.WriteLine(b);
                bookFromObject.Title = b.Title;
                bookFromObject.Cover_Image = b.Cover_Image;
                bookFromObject.Author = b.Author;
                bookFromObject.Description = b.Description;
                bookFromObject.Id = b.Id;
                bookFromObject.Pages = b.Pages;
            }

            if (bookFromObject != null)
            {
                string pages = JsonConvert.SerializeObject(bookFromObject.Pages, Formatting.Indented);
                return (ActionResult)new OkObjectResult(pages);
                //log.LogInformation(JsonConvert.SerializeObject(bookFromObject.Pages, Formatting.Indented));
            }
            else
            {
                return (ActionResult)new OkObjectResult("NO BOOK FOUND");
            }


            // if set, display results,
            // else, return not found HttpResponse



            //return bookTitle != null
                //? (ActionResult)new OkObjectResult(bookTitle)
                //: new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }
    }
}
