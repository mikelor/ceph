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
using System.Reflection.Metadata;

namespace CephSked.Automation
{
    public static class EmailFlightSchedule
    {
        private static readonly HttpClient _httpClient = new HttpClient();
 

        [FunctionName("EmailFlightSchedule")]
        public static async void Run([TimerTrigger("1/1 * * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            List<FlightScheduleForDateResponse> scheduleForDateResponses = await GetFlightSchedule(log);
            await SendEmail(scheduleForDateResponses, log);

        }

        private static async Task SendEmail(List<FlightScheduleForDateResponse> scheduleForDateResponses, ILogger log)
        {
            var apiKey = Environment.GetEnvironmentVariable("sendGridAPIKey");
            var fromEmailAddress = Environment.GetEnvironmentVariable("fromEmailAddress");
            var fromEmailName = Environment.GetEnvironmentVariable("fromEmailName");
            var toEmailAddress = Environment.GetEnvironmentVariable("toEmailAddress");
            var toEmailName = Environment.GetEnvironmentVariable("toEmailName");

            var from = new EmailAddress(fromEmailAddress, fromEmailName);
            var to = new EmailAddress(toEmailAddress, toEmailName);
            var msg = MailHelper.CreateSingleEmail(from, to, "SEA Spot Saver Flights", "Here's the File", null);


            CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var csvWriter = new CsvWriter(streamWriter, csvConfig);
            csvWriter.WriteRecords<FlightScheduleForDateResponse>(scheduleForDateResponses);

            streamWriter.Flush(); // flush the buffered text to stream
            ms.Seek(0, SeekOrigin.Begin); // reset stream position

            await msg.AddAttachmentAsync("SEASpotSaverFlights.csv", ms);
            var client = new SendGridClient(apiKey);
            var response = await client.SendEmailAsync(msg);
            log.LogInformation(response.StatusCode.ToString());
        }



        //
        // GetFlightSchedule(log)
        // Returns the Url of the latest TsaThroughputFile
        private static async Task<List<FlightScheduleForDateResponse>> GetFlightSchedule(ILogger log)
        {
            Uri getTokenUri = new Uri("https://api.betterairport.com/token");
            TokenRequest tokenRequest = new TokenRequest
            {
                User = Environment.GetEnvironmentVariable("BetterAirportsApiUser"),
                Key = Environment.GetEnvironmentVariable("BetterAirportsApiKey"),
            };

            TokenResponse tokenResponse = await GetTokenAsync(getTokenUri, tokenRequest);

            FlightScheduleForDateRequest flightScheduleForDateRequest = new FlightScheduleForDateRequest
            {
                SearchDate = DateTime.UtcNow
            };
            Uri getFlightScheduleForDateUri = new Uri(String.Format("https://api.betterairport.com/forecast/scheduleFlights/{0}", flightScheduleForDateRequest.SearchDate.ToString("yyyy-MM-dd")));
            List<FlightScheduleForDateResponse> scheduleForDateResponses = await GetFlightScheduleForDateAsync(tokenResponse, getFlightScheduleForDateUri, flightScheduleForDateRequest);

            return scheduleForDateResponses;
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
