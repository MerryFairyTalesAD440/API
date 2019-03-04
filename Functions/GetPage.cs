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
        [FunctionName("GetPages")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/{bookid}/pages/{pagenum}/")] HttpRequest req, string bookid, string pagenum, ILogger log, ExecutionContext context)
        {
            log.LogInformation("----- function to get the pages and languages from a book");

            // convert "pagenumber" to an integer
            int pagenumber = Convert.ToInt32(pagenum);

            // =====================================================================================================
            //                                            GET MY VARIABLES 
            // =====================================================================================================
            //TODO: change this to read from Azure
            string cosmosURI = System.Environment.GetEnvironmentVariable("CosmosURI");
            string cosmosKey = System.Environment.GetEnvironmentVariable("CosmosKey");
            string cosmosDBName = System.Environment.GetEnvironmentVariable("CosmosDBName");
            string cosmosDBCollection = System.Environment.GetEnvironmentVariable("CosmosDBCollection");

            // =====================================================================================================
            //                                         CONNECT TO COSMOS DB
            // =====================================================================================================
            DocumentClient client = new DocumentClient(new Uri(cosmosURI), cosmosKey);
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
            var collectionLink = UriFactory.CreateDocumentCollectionUri(cosmosDBName, cosmosDBCollection);
            var query = "SELECT * FROM Books b WHERE b.id = \'" + bookid + "\'";
            var document = client.CreateDocumentQuery(collectionLink, query, queryOptions).ToList();
            log.LogInformation(document.Count.ToString());

            // =====================================================================================================
            //                                              VALIDATION
            // =====================================================================================================
            Book bookFromObject = document.ElementAt(0);

            // resource not found 
            if (bookFromObject.Id == null) { return (ActionResult)new StatusCodeResult(404); }

            // Bad page input
            if (pagenumber < 1 || pagenumber > bookFromObject.Pages.Count()) { return (ActionResult)new StatusCodeResult(400); }

            // =====================================================================================================
            //                                         DISPLAY RESULTS 
            // =====================================================================================================
            if (bookFromObject.Id != null)
            {
                //the Pages[] is indexed from 0 and the pages start at 1, so I minus one to counter it
                string pages = JsonConvert.SerializeObject(bookFromObject.Pages[pagenumber - 1], Formatting.Indented);
                return (ActionResult)new OkObjectResult(pages);
                //log.LogInformation(JsonConvert.SerializeObject(bookFromObject.Pages, Formatting.Indented));
            }
            else
            {
                //return new BadRequestObjectResult("Book not found");
                return (ActionResult)new StatusCodeResult(404); // resource not found
            }


            //TODO: update keys in Azure.
        }
    }
}
