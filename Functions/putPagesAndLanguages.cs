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

namespace Functions
{
    public static class PutPagesAndLanguages
    {
        [FunctionName("putPagesAndLanguages")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "books/{bid}/pages/{pid}/language/{lid}")] HttpRequest req, ILogger log)
        {
            DocumentClient client;
            log.LogInformation("Http function to put page and language");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            if (!validDocument(data))
            {
                FeedOptions queryOptions = new FeedOptions { EnableCrossPartitionQuery=true};
                client = new DocumentClient(new Uri("https://localhost:8081"), "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
                IQueryable<Book> familyQueryInSql = client.CreateDocumentQuery<Book>(UriFactory.CreateDocumentCollectionUri("MerryFairyTales", "Books"),
                "SELECT * FROM Books WHERE Books.id = '1'",
                queryOptions);

                foreach (Book b in familyQueryInSql)
                {
                    var checkValue = b;
                }
                return (ActionResult)new OkObjectResult("Returned Book");
            }
            else
            {
                return (ActionResult)new BadRequestObjectResult("Json sent in wrong format!");
            }
        }

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

    }
}
