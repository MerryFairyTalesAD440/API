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

namespace Functions
{
    public static class AddBook
    {
        /* AddBook - Adds a new book. Returns the book id. */
        [FunctionName("PostBook")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "books")] HttpRequest req,
            [CosmosDB(
                databaseName: "MerryFairyTalesDB",
                collectionName: "Books",
                ConnectionStringSetting = "CosmosDBConnection")]
            IAsyncCollector<NewBook> books, 
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to add a new book");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic book = JsonConvert.DeserializeObject<NewBook>(requestBody);
            book.id = System.Guid.NewGuid().ToString();
            log.LogInformation("Saving book to database.");
            await books.AddAsync(book);String name = book.title;       
            log.LogInformation("Starting container build function.");
            // call Brad's Containter Util
            ContainerUtil.ProcessAsync(name, log).GetAwaiter().GetResult();
            return book != null
                ? (ActionResult)new OkObjectResult(book.id)
                : new BadRequestObjectResult("Please pass a valid book in the request body");
        }

    /* New Book Data Model */
    public class NewBook {
        public string id { get; set; }
        public string description { get; set; }
        public string author { get; set; }
        public string title { get; set; }
    }

    }
}
