using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;


using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;



using CsvHelper;
using CsvHelper.Configuration;

using SendGrid;
using SendGrid.Helpers.Mail;

using CephSked.Models;


namespace CephSked.Automation
{
    public static class EmailFlightSchedule
    {
        private static readonly HttpClient _httpClient = new HttpClient();
 

        [FunctionName("EmailFlightSchedule")]
        public static async void Run([TimerTrigger("1/1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            Attachment a = await GetFlightSchedule(log);
            log.LogInformation(a.Name);
     
            var mailMessage = new MailMessage();
            try
            {
                mailMessage.Attachments.Add(a);
                await SendEmail(mailMessage);
            }
            catch (Exception ex)
            {
                log.LogError($"Exception: {ex.Message}");
            }
        }

        private static async Task SendEmail(MailMessage mailMessage)
        {
            var apiKey = Environment.GetEnvironmentVariable("sendGridAPIKey");
            var sendGridTemplateId = Environment.GetEnvironmentVariable("sendGridTemplateId");
            var fromEmailAddress = Environment.GetEnvironmentVariable("fromEmailAddress");
            var fromEmailName = Environment.GetEnvironmentVariable("fromEmailName");
            var toEmailAddress = Environment.GetEnvironmentVariable("toEmailAddress");
            var toEmailName = Environment.GetEnvironmentVariable("toEmailName");

            var from = new EmailAddress(fromEmailAddress, fromEmailName);
            var to = new EmailAddress(toEmailAddress, toEmailName);
            var msg = MailHelper.CreateSingleTemplateEmail(from, to, sendGridTemplateId, mailMessage);
            var client = new SendGridClient(apiKey);
            var response = await client.SendEmailAsync(msg);
        }



        //
        // GetFlightSchedule(log)
        // Returns the Url of the latest TsaThroughputFile
        private static async Task<Attachment> GetFlightSchedule(ILogger log)
        {
            Uri getTokenUri = new Uri("https://api.betterairport.com/token");
            TokenRequest tokenRequest = new TokenRequest
            {

            };

            TokenResponse tokenResponse = await GetTokenAsync(getTokenUri, tokenRequest);

            FlightScheduleForDateRequest flightScheduleForDateRequest = new FlightScheduleForDateRequest
            {
                SearchDate = DateTime.UtcNow
            };
            Uri getFlightScheduleForDateUri = new Uri(String.Format("https://api.betterairport.com/forecast/scheduleFlights/{0}", flightScheduleForDateRequest.SearchDate.ToString("yyyy-MM-dd")));
            List<FlightScheduleForDateResponse> scheduleForDateResponses = await GetFlightScheduleForDateAsync(tokenResponse, getFlightScheduleForDateUri, flightScheduleForDateRequest);

            CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var ms = new MemoryStream();
            using var streamWriter = new StreamWriter(ms);
            using var csvWriter = new CsvWriter(streamWriter, csvConfig);
            csvWriter.WriteRecords<FlightScheduleForDateResponse>(scheduleForDateResponses);


            using TextWriter tw = new StreamWriter(ms);
            using CsvWriter csv = new CsvWriter(tw, csvConfig);
            csv.WriteRecords(scheduleForDateResponses); // Converts error records to CSV

            tw.Flush(); // flush the buffered text to stream
            ms.Seek(0, SeekOrigin.Begin); // reset stream position
            Attachment a = new Attachment(ms, "flightSchedule.csv");



            return attachment;
        }

        //
        // GetAsyncString(url)
        // Returns a string representation of the webpage for the given URL
        private static async Task<TokenResponse> GetTokenAsync(Uri getTokenUri, TokenRequest tokenRequest)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, getTokenUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("x-api-user", tokenRequest.User);
            request.Headers.TryAddWithoutValidation("x-api-key", tokenRequest.Key);

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();

            TokenResponse tokenResponse =
                await JsonSerializer.DeserializeAsync<TokenResponse>(
                    responseStream,
                    new JsonSerializerOptions
                    {
                        IgnoreNullValues = true,
                        PropertyNameCaseInsensitive = true
                    }
                );

            return tokenResponse;
        }


        //
        // GetAsyncString(url)
        // Returns a string representation of the webpage for the given URL
        private static async Task<List<FlightScheduleForDateResponse>> GetFlightScheduleForDateAsync(TokenResponse tokenResponse, Uri getFlightScheduleForDateUri, FlightScheduleForDateRequest flightScheduleForDateRequest)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, getFlightScheduleForDateUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();

            List<FlightScheduleForDateResponse> flightScheduleForDateResponses =
                await JsonSerializer.DeserializeAsync<List<FlightScheduleForDateResponse>>(
                    responseStream,
                    new JsonSerializerOptions
                    {
                        IgnoreNullValues = true,
                        PropertyNameCaseInsensitive = true
                    }
                );

            return flightScheduleForDateResponses;
        }

        //
        // SaveThroughtputPdfAsync(url)
        // Saves the PDF at the given URL to blob storage
        private static async Task<string> SaveThroughputPdfAsync(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            request.Headers.TryAddWithoutValidation("Accept", "application/pdf");
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.2; WOW64; rv:19.0) Gecko/20100101 Firefox/19.0");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var streamReader = new StreamReader(responseStream);

            string pdf = await streamReader.ReadToEndAsync().ConfigureAwait(false);
            return pdf;
        }


    }

}
