using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace API.Function
{
    public static class AddBook
    {
        /* AddBook - Adds a new book. Includes code to connect to test Cosmos DB resource that was deleted. For now, returns
        just a generated book id. For next sprint, connect to Brad's container function and Francesco's Cosmos DB function*/
        [FunctionName("PostBook")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "books")] HttpRequest req,
            /* This was my test implementation that is no longer running */
            // [CosmosDB(
            //     databaseName: "MerryFairyTalesDB",
            //     collectionName: "Books",
            //     ConnectionStringSetting = "CosmosDBConnection")]
            // IAsyncCollector<Book> books, 
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to add a new book");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic book = JsonConvert.DeserializeObject<Book>(requestBody);
            // TODO: create container for new book
            book.id = System.Guid.NewGuid().ToString();
            // TODO: connect to new database, the code was for the SQL API used by my test database
            // await books.AddAsync(book);
            // return (ActionResult)new OkObjectResult(book);
            return (ActionResult)new OkObjectResult(book.id);
        }

    /* New Book Data Model */
    public class Book {
        public string id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string author { get; set; }
    }

    }
}