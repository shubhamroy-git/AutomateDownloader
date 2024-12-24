using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RekhtaAutomationTests
{
    [TestFixture]
    public class TestSuite
    {
        [Test]
        public void RunTestsInSequence()
        {
            // Step 1: Run the BookLinkExtractorTests
            var extractor = new BookLinkExtractorTests();
            extractor.SetUp();
            extractor.ExtractBookLinks_ShouldReturnAllLinks();
            extractor.TearDown();

            // Step 2: Verify that links are extracted and passed to the next test
            Console.WriteLine($"Extracted {BookLinkExtractorTests.BookLinks.Count} links");

            // Step 3: Run the BookDownloaderTests
            var downloader = new BookDownloaderTests();
            downloader.SetUp();
            downloader.DownloadBooks_ShouldDownloadAllLinks();
            downloader.TearDown();
        }
    }

    public class BookLinkExtractorTests
    {
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        private IWebDriver _driver;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        public static List<string> BookLinks = new List<string>(); // Make this static and public

        [SetUp]
        public void SetUp()
        {
            _driver = new ChromeDriver();
            _driver.Manage().Window.Maximize();
        }

        [TearDown]
        public void TearDown()
        {
            _driver.Quit();
        }

        [Test]
        public void ExtractBookLinks_ShouldReturnAllLinks()
        {
            BookLinks = ExtractBookLinks(_driver);
            Assert.IsNotEmpty(BookLinks, "Book links should not be empty.");

            // Log the total number of extracted links
            Console.WriteLine($"Total number of links extracted: {BookLinks.Count}");

            // Log the last five links
            Console.WriteLine("Last five extracted book links:");
            var lastFiveLinks = BookLinks.Skip(Math.Max(0, BookLinks.Count - 5)).ToList();
            foreach (var link in lastFiveLinks)
            {
                Console.WriteLine(link);
            }
        }

        private List<string> ExtractBookLinks(IWebDriver driver)
        {
            List<string> bookLinks = new List<string>();
            driver.Navigate().GoToUrl("https://www.rekhta.org/ebooks/category/poetry/couplets#");

            bool hasNextPage = true;

            while (hasNextPage)
            {
                try
                {
                    // Wait for ebookCard elements to load
                    WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(5));
                    wait.Until(d => d.FindElements(By.ClassName("ebookCard")).Count > 0);

                    // Refetch the ebookCard elements to avoid stale references
                    var ebookCards = driver.FindElements(By.ClassName("ebookCard"));

                    foreach (var card in ebookCards)
                    {
                        try
                        {
                            // Refetch individual card elements to avoid stale references
                            var linkElements = card.FindElements(By.TagName("a"));
                            if (linkElements.Count > 1)
                            {
                                string bookLink = linkElements[1].GetAttribute("href");
                                if (!string.IsNullOrEmpty(bookLink) && !bookLinks.Contains(bookLink))
                                {
                                    bookLinks.Add(bookLink);
                                }
                            }
                        }
                        catch (StaleElementReferenceException)
                        {
                            // Continue if individual card becomes stale
                            continue;
                        }
                    }

                    wait.Until(d => !d.FindElement(By.ClassName("pageLoader")).Displayed);

                    try
                    {
                        // Check for the presence of the next page button
                        var nextPageButton = wait.Until(driver =>
                        {
                            var element = driver.FindElement(By.ClassName("pgNext"));
                            return element.Displayed ? element : null;
                        });

                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", nextPageButton);
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextPageButton);
                    }
                    catch (StaleElementReferenceException)
                    {
                        // Refetch the next page button if it becomes stale
                        var nextPageButton = wait.Until(driver =>
                        {
                            var element = driver.FindElement(By.ClassName("pgNext"));
                            return element.Displayed ? element : null;
                        });

                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", nextPageButton);
                    }
                    catch (NoSuchElementException)
                    {
                        hasNextPage = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error occurred while extracting book links: {ex.Message}");
                    hasNextPage = false; // Exit loop if a critical error occurs
                }
            }

            return bookLinks;
        }
    }

    public class BookDownloaderTests
    {
#pragma warning disable NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method
        private IWebDriver _driver;
#pragma warning restore NUnit1032 // An IDisposable field/property should be Disposed in a TearDown method

        [SetUp]
        public void SetUp()
        {
            _driver = new ChromeDriver();
        }

        [TearDown]
        public void TearDown()
        {
            _driver.Quit();
        }

        [Test]
        public void DownloadBooks_ShouldDownloadAllLinks()
        {
            // Ensure links are extracted before proceeding to download
            var bookLinks = BookLinkExtractorTests.BookLinks;

            Assert.IsNotEmpty(bookLinks, "No links available to download.");
            DownloadBooks(_driver, bookLinks);
        }

        private void DownloadBooks(IWebDriver driver, List<string> bookLinks)
        {
            Console.WriteLine("Starting book download process...");

            // Ensure we have unique links
            var uniqueLinks = bookLinks.Distinct().ToList();
            Console.WriteLine($"Number of unique links to process: {uniqueLinks.Count}");

            foreach (var link in uniqueLinks)
            {
                Console.WriteLine($"Processing link: {link}");
                int retryCount = 0;
                bool downloadSuccess = false;

                while (retryCount<3 && !downloadSuccess)
                {
                    try
                    {
                        // Navigate to the downloader page
                        driver.Navigate().GoToUrl("https://www.rekhtadownload.com/");

                        // Wait for the page to load and input field to be ready
                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromMinutes(3));
                        wait.Until(d => d.FindElement(By.CssSelector("input[type='text']")).Displayed);

                        // Clear the input field before entering the next link
                        var inputField = driver.FindElement(By.CssSelector("input[type='text']"));
                        inputField.Clear();  // Ensure the field is cleared
                        inputField.SendKeys(link);  // Enter the new link

                        // Log the link being processed for debugging
                        Console.WriteLine($"Entered link: {link}");

                        // Click the "Download" button
                        var downloadButton = driver.FindElement(By.XPath("//*[@id='frontend-app']/div/div[1]/div/div[3]/div/div[1]/div[1]/button"));
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", downloadButton);

                        // Wait for the "Download Another" button to appear after the download starts
                        wait.Until(d =>
                        {
                            var downloadAnotherButton = d.FindElement(By.LinkText("Download Another"));
                            return downloadAnotherButton.Displayed;
                        });

                        downloadSuccess = true; // Mark download as successful
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        Console.WriteLine($"Attempt {retryCount}: Error processing link {link}. Error: {ex.Message}");
                        if (retryCount < 3)
                        {
                            Thread.Sleep(2000); // Adding a short delay before retrying
                        }
                    }
                }

                if (!downloadSuccess)
                {
                    Console.WriteLine($"Failed to process link after 3 attempts: {link}");
                }
            }
        }
    }
}
