using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Chrome;
using System.Threading;
using System.Net;
using System.Net.Mail;
using RestSharp;
using RestSharp.Serializers.Newtonsoft;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace VSlotNotification
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("please enter pin code");
            var pincode = Console.ReadLine();
            pincode = pincode.Trim();
            Match match = Regex.Match(pincode, @"^[1-9][0-9]{5}$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                Console.WriteLine("invalid pincode");
            }
            Console.WriteLine("please enter comma separated emailids for notification");
            var emails = Console.ReadLine();
            var emailSplit = emails.Split(',');
            List<string> emailList = new List<string>();
            foreach (var email in emailSplit)
            {
                if (!string.IsNullOrEmpty(email.Trim()))
                {
                    emailList.Add(email.Trim());
                }
            }


            //using cowin Apis
            CallCowinApi(pincode, emailList);
            //using cowin web page
            //CrawlCowinWebPage(pincode, emailList);
        }
        public static void CallCowinApi(string pinCode, List<string> emails) {
            int exceptionCount = 0;
            while (true)
            {
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();

                    var client = new RestSharp.RestClient("https://cdn-api.co-vin.in/api/");
                    var request = new RestRequest("v2/appointment/sessions/public/calendarByPin", DataFormat.Json);
                    request.AddHeader("Accept-Language", "en_US");
                    request.AddQueryParameter("pincode", pinCode);
                    request.AddQueryParameter("date", DateTime.Today.Day.ToString() + "-" + DateTime.Today.Month.ToString()
                   + "-" + DateTime.Today.Year.ToString());

                    var response = client.Get(request).Content;
                    var result = JsonConvert.DeserializeObject<Response>(response);

                    string emailBody = "";
                    bool sendEmail = false;
                    if (result != null && result.centers != null && result.centers.Count > 0)
                    {
                        foreach (var center in result.centers)
                        {
                            if (center.sessions != null)
                            {
                                foreach (var session in center.sessions)
                                {
                                    int avilableCapecity = Convert.ToInt32(session.available_capacity);
                                    int avilableCapecitySecondDose = Convert.ToInt32(session.available_capacity_dose2);
                                    if (Convert.ToInt32(session.min_age_limit) == 18 && (avilableCapecity - avilableCapecitySecondDose) > 0)
                                    {
                                        emailBody = emailBody + center.name + "  " + session.date + "  available slots-" + session.available_capacity + "<br/>";
                                        sendEmail = true;
                                    }
                                }
                            }
                        }
                    }
                    if (sendEmail)
                    {
                        Email(emailBody, emails);
                    }
                    stopWatch.Stop();
                    TimeSpan ts = stopWatch.Elapsed;

                    // Format and display the TimeSpan value.
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                        ts.Hours, ts.Minutes, ts.Seconds,
                        ts.Milliseconds / 10);
                    Console.WriteLine("RunTime " + elapsedTime);
                    Console.WriteLine("scan complete at " + DateTime.Now.ToShortTimeString() + " with error count " + exceptionCount);
                    Thread.Sleep(4000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    exceptionCount++;
                }
            }
        } 
        public static void CrawlCowinWebPage(string pinCode, List<string> emails) {
            IWebDriver webDriver = new ChromeDriver(Environment.CurrentDirectory);
            webDriver.Navigate().GoToUrl("https://www.cowin.gov.in/home");

            var pinInput = webDriver.FindElement(By.Id("mat-input-0"));
            pinInput.SendKeys(pinCode);

            var pinInputSubmitButton = webDriver.FindElement(By.ClassName("pin-search-btn"));
            pinInputSubmitButton.Click();


            var ageGroup18PlusOption = webDriver.FindElement(By.Id("flexRadioDefault3"));
            ((IJavaScriptExecutor)webDriver).ExecuteScript("arguments[0].click();", ageGroup18PlusOption);

            var today = DateTime.Today.Date;

            var resultBlock = webDriver.FindElement(By.ClassName("matlistingblock"));
            var resultRows = resultBlock.FindElements(By.ClassName("row"));
            string mailBody = "";
            bool sendMail = false;
            foreach (var row in resultRows)
            {
                var centerName = row.FindElement(By.ClassName("center-name-title")).Text;
                Console.WriteLine(centerName);
                var slotBlock = row.FindElement(By.ClassName("slot-available-wrap"));
                var slotsPerDay = slotBlock.FindElements(By.TagName("li"));
                var dateOfProcessing = today;
                foreach (var slotOftheDay in slotsPerDay)
                {

                    var booked = slotOftheDay.FindElements(By.CssSelector("div.slots-box.no-seat"));
                    var na = slotOftheDay.FindElements(By.CssSelector("div.slots-box.no-available"));
                    var available = slotOftheDay.FindElements(By.CssSelector("div.slots-box"));
                    if (booked != null && booked.Count > 0)
                    {
                        Console.WriteLine("slots are already booked at " + centerName + " on date- " + dateOfProcessing.ToShortDateString());
                    }
                    else if (na != null && na.Count > 0)
                    {
                        Console.WriteLine("slots are not available at " + centerName + " on date- " + dateOfProcessing.ToShortDateString());
                    }
                    else if (available != null && available.Count > 0)
                    {
                        Console.WriteLine("slots are available at " + centerName + " on date- " + dateOfProcessing.ToShortDateString());
                        mailBody = mailBody + centerName + " date- " + dateOfProcessing.ToShortDateString() + "<br/>";
                        sendMail = true;
                    }
                    dateOfProcessing = dateOfProcessing.AddDays(1);
                }

            }
            if (sendMail)
            {
                Email(mailBody, emails);
            }


        }
        public static void Email(string body, List<string> emails) {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appSettings.json");
            var configuration = builder.Build();

            MailMessage message = new MailMessage();
            SmtpClient smtp = new SmtpClient();
            message.From = new MailAddress(configuration["email"]);
            foreach (var email in emails) {
                message.To.Add(new MailAddress(email));
            }
            message.Subject = "SLOT AVAILABLE";
            message.IsBodyHtml = true; //to make message body as html  
            message.Body = body;
            smtp.Port = 587;
            smtp.Host = "smtp.gmail.com"; //for gmail host  
            smtp.EnableSsl = true;
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(configuration["email"],configuration["password"]);
            smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtp.Send(message);
        }
    }
    public class Response {
        public List<Center> centers { get; set; }
    }
    public class Center {
        public string name { get; set; }
        public List<Session> sessions { get; set; }
    }
    public class Session {
        public string available_capacity { get; set; }
        public string date { get; set; }
        public int min_age_limit { get; set; }
        public string available_capacity_dose2 { get; set; }
    }
}
