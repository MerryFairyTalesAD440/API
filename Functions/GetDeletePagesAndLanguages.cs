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
           

            //pass in the query parameters as such
            string bookTitle = req.Query["bookTitle"];
            string page = req.Query["page"];


            // Set some common query options
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };


            IQueryable<Book> bookQuery = client.CreateDocumentQuery<Book>(
                 UriFactory.CreateDocumentCollectionUri("MerryFairyTalesDB", "Books"), queryOptions)
                 .Where(f => f.Title == bookTitle);

            string b = "";
            // Go through the objects and collect the data.
            foreach (Book book in bookQuery)
            {
                Console.WriteLine("\tRead {0}", book);
                b = book.ToString();

            }

            return bookTitle != null
                ? (ActionResult)new OkObjectResult($"Hello, {b}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }
    }
}
