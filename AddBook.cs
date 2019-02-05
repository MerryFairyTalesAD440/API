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
        /* AddBook - Adds a new book. */
        [FunctionName("PostBook")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "books")] HttpRequest req,
            [CosmosDB(
                databaseName: "MerryFairyTalesDB",
                collectionName: "Books",
                ConnectionStringSetting = "CosmosDBConnection")]
            IAsyncCollector<Book> books, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to add a new book");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic book = JsonConvert.DeserializeObject<Book>(requestBody);
            // TODO: create container for new book
            book.id = System.Guid.NewGuid().ToString();
            await books.AddAsync(book);
            return (ActionResult)new OkObjectResult(book);
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
