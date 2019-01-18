using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DemoFunctionsMikal
{
    public static class PrintName
    {
        /// <summary>
        /// asynch task
        /// function takes a name query parameter or request body
        /// and responds with json message base on which parameter was passed.
        /// </summary>
        /// <param name="req">Http request</param>
        /// <param name="log">Log function information</param>
        /// <returns>http status code and json message</returns>
        [FunctionName("printName")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger to test Azure Functions.");
            string name = req.Query["name"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            if (name != null)
            {
                switch (name.ToUpper())
                {
                    case "TODD":
                        return (ActionResult)new OkObjectResult(new { message = "Hello, " + name.ToString() + ".  Hasta la vista, baby!" });
                    case "DAVE":
                        return (ActionResult)new OkObjectResult(new { message = "I'm sorry, " + name.ToString() + ".  I'm afraid I can't do that!" });
                    case "MIKAL":
                        return (ActionResult)new OkObjectResult(new { message = "Hello, " + name.ToString() + ".  Theres no place like home!" });
                    case "DAN":
                        return (ActionResult)new OkObjectResult(new { message = "Hello, " + name.ToString() + ".  Go ahead.  Make my day!" });
                    case "BRAD":
                        return (ActionResult)new OkObjectResult(new { message = "Hello, " + name.ToString() + ".  May the Force be with you!" });
                    case "NATHAN":
                        return (ActionResult)new OkObjectResult(new { message = "Hello, " + name.ToString() + ".  Life is like a box of chocolates!" });
                    default:
                        return (ActionResult)new OkObjectResult(new { message = "Hello, " + name.ToString() + ".  Welcome to the Thunderdome!" });

                }

            }
            else { return new BadRequestObjectResult(new { message = "Bad request!  Please pass a name to print in the query string or in the request body.  For example: ?name=Dave" }); }

        }
    }
}
