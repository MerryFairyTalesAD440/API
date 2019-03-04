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
    public static class GetDeleteLanguage
    {
        [FunctionName("GetDeleteLanguage")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "delete", Route = "books/{bookid}/pages/{pageid}/language/{languagecode}")] HttpRequestMessage req, string bookid, string pageid, string languagecode, ILogger log, ExecutionContext context)
        {

            // convert "pageid" from string to integer
            int pagenumber = Convert.ToInt32(pageid);

            // =====================================================================================================
            //                                            GET MY VARIABLES 
            // =====================================================================================================
            string cosmosURI          = System.Environment.GetEnvironmentVariable("CosmosURI");
            string cosmosKey          = System.Environment.GetEnvironmentVariable("CosmosKey");
            string cosmosDBName       = System.Environment.GetEnvironmentVariable("CosmosDBName");
            string cosmosDBCollection = System.Environment.GetEnvironmentVariable("CosmosDBCollection");

            // =====================================================================================================
            //                                          CONNECT TO COSMOS DB
            // =====================================================================================================
            DocumentClient client    = new DocumentClient(new Uri(cosmosURI), cosmosKey);
            FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery = true };
            var collectionLink       = UriFactory.CreateDocumentCollectionUri(cosmosDBName, cosmosDBCollection);
            var query                = "SELECT * FROM Books b WHERE b.id = \'" + bookid + "\'";
            var document             = client.CreateDocumentQuery(collectionLink, query, queryOptions).ToList();
            log.LogInformation(document.Count.ToString());

            // =====================================================================================================
            //                                             VALIDATE INPUT
            // =====================================================================================================
            Book oBook = document.ElementAt(0);

            // resource not found 
            if (oBook.Id == null) { return (ActionResult)new StatusCodeResult(404); }

            // Bad page input
            if (pagenumber < 1 || pagenumber > oBook.Pages.Count()) { return (ActionResult)new StatusCodeResult(400); }


            int len = oBook.Pages[pagenumber - 1].Languages.Count();
            int idxOfLangCode = -1;

            //search for the index of the language
            for (int i = 0; i < len; i++)
            {
                // if they match, save the index
                if (oBook.Pages[pagenumber - 1].Languages[i].language.ToLower() == languagecode.ToLower())
                {
                    idxOfLangCode = i;
                    break;
                }
            }
            // No resource found with that language
            if (idxOfLangCode == -1) { return (ActionResult)new StatusCodeResult(404); }

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
                    //the Pages[] is indexed from 0 and the pages start at 1, so I minus one to counter it
                    string pages = JsonConvert.SerializeObject(oBook.Pages[pagenumber - 1].Languages[idxOfLangCode], Formatting.Indented);
                    return (ActionResult)new OkObjectResult(pages);
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
                nBook.Id = oBook.Id;
                nBook.Cover_Image = oBook.Cover_Image;
                nBook.Author = oBook.Author;
                nBook.Description = oBook.Description;
                nBook.Title = oBook.Title;
                nBook.Pages = oBook.Pages;

                List<Language> langArr = nBook.Pages[pagenumber - 1].Languages.ToList<Language>();
                langArr.RemoveAt(idxOfLangCode);
                nBook.Pages[pagenumber - 1].Languages = langArr;

                // =====================================================================================================
                //                                         UPSERT TO COSMOS DB
                // =====================================================================================================
                var result = await client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(cosmosDBName, cosmosDBCollection), nBook);
                log.LogInformation($"{result}");

                return (ActionResult)new StatusCodeResult(200);
            }


            else
            {
                return (ActionResult)new StatusCodeResult(400);
            }
        }
    }
}
