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
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;

namespace Functions
{
    public static class UpdateBook
    {
        /* UpdateBook - Updates an existing book. */
        [FunctionName("UpdateBook")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Edit(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "books/{bookid}")]HttpRequest req,
        [CosmosDB(
            databaseName: "MerryFairyTalesDB",
            collectionName: "Books",
            ConnectionStringSetting = "CosmosDBConnection")]
        DocumentClient client,
        ILogger log, string bookid)
        {
        log.LogInformation("C# HTTP trigger function processed a request to update a book");    
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var book = JsonConvert.DeserializeObject<Book>(requestBody);
        log.LogInformation("Book passed to function: " + book.ToString());
        log.LogInformation("Attempting to retrieve book from database - bookid: " + bookid);
        var option = new FeedOptions { EnableCrossPartitionQuery = true };
        Uri collectionUri = UriFactory.CreateDocumentCollectionUri("MerryFairyTalesDB", "Books");
        dynamic document = client.CreateDocumentQuery<Book>(collectionUri, option).Where(b => b.Id == bookid)
                        .AsEnumerable().FirstOrDefault();
        if (document == null)
        {
           return new NotFoundResult();
        }
        document = book;
        await client.UpsertDocumentAsync(collectionUri, document);
        Book book2 = (dynamic)document;
        return new OkObjectResult(book2);
        // return new OkObjectResult("Book was updated successfully.");
        }
    }
}
