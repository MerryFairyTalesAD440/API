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
    public static class GetBooks
    {
        /* GET /books - function to get or list all books. */
        [FunctionName("books")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get list of books");
           
            string jsonFormatted = Newtonsoft.Json.JsonConvert.SerializeObject(getJsonBooks(), Newtonsoft.Json.Formatting.Indented);
           
            return (ActionResult)new OkObjectResult(jsonFormatted);
        }

        /*
        Creating Dummy Books Object for now for testing purpose and so that the UI can use this function for their end.
         */
        public static List<Book> getJsonBooks() 
        {
            List<Book> books = new List<Book>();

            Book bookCinderella = createBook("1", 
                                            "Cinderella", 
                                            "Marcia Brown", 
                                            "Cinderella is a folk tale embodying a myth-element of unjust oppression and triumphant reward.");
            bookCinderella.pages = createPages(bookCinderella.title, 2); // creating 2 pages
            createLanguagesPerPage(bookCinderella.title, bookCinderella.pages);

            Book bookPrincePauper = createBook("2", 
                                            "The Prince and the Pauper", 
                                            "Mark Twain", 
                                            "The novel represents Twain's first attempt at historical fiction.");
            bookPrincePauper.pages = createPages(bookPrincePauper.title, 2); // creating 2 pages
            createLanguagesPerPage(bookPrincePauper.title, bookPrincePauper.pages);

            Book bookSnowWhite = createBook("3", 
                                            "Snow White", 
                                            "Jacob Grimm", 
                                            "The fairy tale features such elements as the magic mirror, the poisoned apple, the glass coffin, and the characters of the evil queen and the Seven Dwarfs.");
            bookSnowWhite.pages = createPages(bookSnowWhite.title, 3); // creating 3 pages
            createLanguagesPerPage(bookSnowWhite.title, bookSnowWhite.pages);

            // Adding each book to the list.
            books.Add(bookCinderella);
            books.Add(bookPrincePauper);
            books.Add(bookSnowWhite);

            return books;
        }

        /*
        Creates a book
         */
        public static Book createBook(string id, string title, string author, string description) {
            Book book = new Book();
            book.id = id;
            book.title = title;
            book.author = author;
            book.description = description;
            return book;
        }

        /* Creates list of pages */
        public static List<Page> createPages(string bookTitle, int numberOfPages) {
            List<Page> pages = new List<Page>();
            for(int i=0; i<numberOfPages; i++) {
                Page page = new Page();
                page.number = i + 1;
                bookTitle = bookTitle.Replace(" ", "");
                page.imageUrl = "http://imageUrl_" + bookTitle + "_page" + page.number + ".jpg";
                pages.Add(page);
            }
            return pages;
        }

        /* Creates list of languges */
        public static List<Language> createLanguages(string bookTitle, int pageNumber) {
            List<Language> languages = new List<Language>();
            Language language1 = new Language();
            language1.languageCode = "en_US";
            Language language2 = new Language();
            language2.languageCode = "fr_FR";

            languages.Add(language1);
            languages.Add(language2);
            bookTitle = bookTitle.Replace(" ", "");

            foreach (Language element in languages) {
                element.text = "https://text_" + bookTitle + "_" + element.languageCode + "_page" + pageNumber + ".txt";
                element.audioUrl = "https://audio_" + bookTitle + "_" + element.languageCode + "_page" + pageNumber+ ".mp3";
            }
            return languages;
        }

        public static void createLanguagesPerPage(string bookTitle, List<Page> pages) {
            foreach (Page page in pages) {
                page.languages = createLanguages(bookTitle, page.number);
            }
        }
    }


    /* Book Data Model */
    public class Book
    {
        public string id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string author { get; set; }
        public List<Page> pages {get; set;}
    }

    /* Page Data Model */
    public class Page
    {
        public int number {get; set;}
        public string imageUrl {get; set;}
        public List<Language> languages {get; set;}
        
    }

    /* Language Data Model */
    public class Language
    {
        public string languageCode {get; set;}
        public string text {get; set;}
        public string audioUrl {get; set;}
    }
}
