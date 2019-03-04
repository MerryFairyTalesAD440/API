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

namespace Functions
{
    public static class DeletePage
    {
        [FunctionName("DeletePage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/{bookid}/pages-toDelete/{pagenum}/")] HttpRequest req, string bookid, string pagenum, ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

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
            Book oBook = document.ElementAt(0);

            // resource not found 
            if (oBook.Id == null) { return (ActionResult)new StatusCodeResult(404); }

            // Bad page input
            if (pagenumber < 1 || pagenumber > oBook.Pages.Count()) { return (ActionResult)new StatusCodeResult(400); }

            // =====================================================================================================
            //                                         CREATE A NEW BOOK
            // =====================================================================================================
            Book nBook = new Book();
            //create a new book from the old book omitting the language
            nBook.Id = oBook.Id;
            nBook.Cover_Image = oBook.Cover_Image;
            nBook.Author = oBook.Author;
            nBook.Description = oBook.Description;
            nBook.Title = oBook.Title;
            nBook.Pages = oBook.Pages;

            List<Page> pageArr = nBook.Pages.ToList<Page>();
            pageArr.RemoveAt(pagenumber - 1);
            nBook.Pages = pageArr;

            //go through a list of pages, setting the page number to the proper location
            for (int i = 0; i < nBook.Pages.Count; i++)
            {
                nBook.Pages[i].Number = (i + 1).ToString();
            }

            // =====================================================================================================
            //                                         UPSERT TO COSMOS DB
            // =====================================================================================================
            var result = await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(cosmosDBName, cosmosDBCollection), nBook);
            log.LogInformation($"{result}");

            return (ActionResult)new StatusCodeResult(200);
        }
    }
}
