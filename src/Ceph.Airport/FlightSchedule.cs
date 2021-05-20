using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using CsvHelper;
using CsvHelper.Configuration;

using Microsoft.Extensions.Logging;

using SendGrid;
using SendGrid.Helpers.Mail;

using Ceph.Airport.Models;

namespace Ceph.Airport
{
    public static class FlightSchedule
    {


        //
        // GetFlightScheduleAsync
        // Returns the Url of the latest TsaThroughputFile
        public static async Task<List<FlightScheduleForDateResponse>> GetFlightScheduleAsync(HttpClient httpClient, DateTime searchDate, ILogger log)
        {
            // Authenticate against the Api
            Uri getTokenUri = new Uri("https://api.betterairport.com/token");
            TokenRequest tokenRequest = new TokenRequest
            {
                User = Environment.GetEnvironmentVariable("BetterAirportsApiUser"),
                Key = Environment.GetEnvironmentVariable("BetterAirportsApiKey"),
            };
            TokenResponse tokenResponse = await GetTokenAsync(httpClient, getTokenUri, tokenRequest);

            // Get Flight Schedule for Search Date
            FlightScheduleForDateRequest flightScheduleForDateRequest = new FlightScheduleForDateRequest
            {
                 SearchDate = searchDate
            };
            Uri getFlightScheduleForDateUri = new Uri(String.Format("https://api.betterairport.com/forecast/scheduleFlights/{0}", flightScheduleForDateRequest.SearchDate.ToString("yyyy-MM-dd")));

            List<FlightScheduleForDateResponse> vqEligibleFlights = new List<FlightScheduleForDateResponse>();
            List<FlightScheduleForDateResponse> scheduleForDateResponses = await GetFlightScheduleForDateAsync(httpClient, tokenResponse, getFlightScheduleForDateUri, flightScheduleForDateRequest);
            foreach(FlightScheduleForDateResponse f in scheduleForDateResponses)
            {
                if(f.Fields[4].Name.Equals("VQ") && f.Fields[4].Value.Equals("VQ-5"))
                {
                    vqEligibleFlights.Add(f);
                }
            }

            log.LogInformation($"{scheduleForDateResponses.Count} Flights Retrieved for {searchDate.ToShortDateString()}.");
            log.LogInformation($"{vqEligibleFlights.Count} are eligible for Virtual Queuing.");

            return vqEligibleFlights;
        }

        //
        // GetTokenAsync
        // Authenticates against the BetterAirports Api
        public static async Task<TokenResponse> GetTokenAsync(HttpClient httpClient, Uri getTokenUri, TokenRequest tokenRequest)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, getTokenUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("x-api-user", tokenRequest.User);
            request.Headers.TryAddWithoutValidation("x-api-key", tokenRequest.Key);

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var responseStream = await response.Content.ReadAsStreamAsync();

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
        // GetFlightScheduleForDateAsync
        // Returns a the Flight Schedule for the Date
        public static async Task<List<FlightScheduleForDateResponse>> GetFlightScheduleForDateAsync(HttpClient httpClient, TokenResponse tokenResponse, Uri getFlightScheduleForDateUri, FlightScheduleForDateRequest flightScheduleForDateRequest)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, getFlightScheduleForDateUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);
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
        // SendEmailAsync
        // Sends the Flight Schedule as an attachment
        public static async Task SendEmailAsync(List<FlightScheduleForDateResponse> scheduleForDateResponses, DateTime searchDate, ILogger log)
        {
            var apiKey = Environment.GetEnvironmentVariable("sendGridApiKey");
            var fromEmailAddress = Environment.GetEnvironmentVariable("fromEmailAddress");
            var fromEmailName = Environment.GetEnvironmentVariable("fromEmailName");

            var from = new EmailAddress(fromEmailAddress, fromEmailName);

            // Add
            List<EmailAddress> addresses = new List<EmailAddress>();
            addresses.Add(new EmailAddress(
                Environment.GetEnvironmentVariable("toEmailListAddress1"),
                Environment.GetEnvironmentVariable("toEmailListName1")));

            
            addresses.Add(new EmailAddress(
                Environment.GetEnvironmentVariable("toEmailListAddress2"),
                Environment.GetEnvironmentVariable("toEmailListName2"))); 
            

            var multimsg = MailHelper.CreateSingleEmailToMultipleRecipients(from, addresses, 
                $"{scheduleForDateResponses.Count} SEA Spot Saver Flights for {searchDate.ToShortDateString()}", 
                $"The attached file contains {scheduleForDateResponses.Count} flights that are eligible for SEA Spot Saver on {searchDate.ToShortDateString()}.\nThis file was generated at {DateTime.Now}", null);

            // Create the CSV File for the attachment.
            CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture);
            using var ms = new MemoryStream();
            var streamWriter = new StreamWriter(ms);
            var csvWriter = new CsvWriter(streamWriter, csvConfig);
            csvWriter.WriteRecords<FlightScheduleForDateResponse>(scheduleForDateResponses);

            streamWriter.Flush(); // flush the buffered text to stream
            ms.Seek(0, SeekOrigin.Begin); // reset stream position

            await multimsg.AddAttachmentAsync($"{searchDate.ToShortDateString()}SEASpotSaverFlights.csv", ms);
            var client = new SendGridClient(apiKey);
            var response = await client.SendEmailAsync(multimsg);
            log.LogInformation(response.StatusCode.ToString());
        }
    }
}
