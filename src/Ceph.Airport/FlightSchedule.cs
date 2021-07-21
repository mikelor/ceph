using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
            TokenResponse tokenResponse = await GetTokenAsync(httpClient, getTokenUri, tokenRequest, log);

            // Get Flight Schedule for Search Date
            FlightScheduleForDateRequest flightScheduleForDateRequest = new FlightScheduleForDateRequest
            {
                 SearchDate = searchDate
            };
            Uri getFlightScheduleForDateUri = new Uri(String.Format("https://api.betterairport.com/forecast/scheduleFlights/{0}", flightScheduleForDateRequest.SearchDate.ToString("yyyy-MM-dd")));

            List<FlightScheduleForDateResponse> vqEligibleFlights = new List<FlightScheduleForDateResponse>();
            List<FlightScheduleForDateResponse> scheduleForDateResponses = await GetFlightScheduleForDateAsync(httpClient, tokenResponse, getFlightScheduleForDateUri, flightScheduleForDateRequest, log);
            foreach(FlightScheduleForDateResponse flight in scheduleForDateResponses)
            {
                // TODO: Could probably do a Find Predicate
                foreach (Field f in flight.Fields)
                {
                    if (f.Name.Equals("VQ") && f.Value.Equals("VQ-5 VQ-3"))
                    {
                        vqEligibleFlights.Add(flight);
                        break;
                    }
                }
            }

            log.LogInformation($"{scheduleForDateResponses.Count} Flights Retrieved for {searchDate.ToShortDateString()}.");
            log.LogInformation($"{vqEligibleFlights.Count} are eligible for Virtual Queuing.");

            return vqEligibleFlights;
        }

        //
        // GetTokenAsync
        // Authenticates against the BetterAirports Api
        public static async Task<TokenResponse> GetTokenAsync(HttpClient httpClient, Uri getTokenUri, TokenRequest tokenRequest, ILogger log)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, getTokenUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("x-api-user", tokenRequest.User);
            request.Headers.TryAddWithoutValidation("x-api-key", tokenRequest.Key);

            var response = await httpClient.SendAsync(request).ConfigureAwait(false);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch(Exception e)
            {
                log.LogInformation($"GetTokenAsync() Failure: {e.Message}");
                throw e;
            }

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
        public static async Task<List<FlightScheduleForDateResponse>> GetFlightScheduleForDateAsync(HttpClient httpClient, TokenResponse tokenResponse, Uri getFlightScheduleForDateUri, FlightScheduleForDateRequest flightScheduleForDateRequest, ILogger log)
        {

            using var request = new HttpRequestMessage(HttpMethod.Get, getFlightScheduleForDateUri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            using var response = await httpClient.SendAsync(request).ConfigureAwait(false);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                log.LogInformation($"GetFlightScheduleForDateAsync() Failure: {e.Message}");
                throw e;
            }

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
            var fromEmailAddress = Environment.GetEnvironmentVariable("fromAddress");
            var fromEmailName = Environment.GetEnvironmentVariable("fromName");

            var from = new EmailAddress(fromEmailAddress, fromEmailName);

            // Send to One or more Emails
            List<EmailAddress> toAddresses = new List<EmailAddress>();
            List<String> toAddressList = Environment.GetEnvironmentVariable("toAddressList").Split(',').ToList();
            List<String> toNameList = Environment.GetEnvironmentVariable("toNameList").Split(',').ToList();

            // Create a Tuple so we don't have to iterate through the lists twice
            foreach (var toAddress in toAddressList.Zip(toNameList, Tuple.Create))
            {
                toAddresses.Add(new EmailAddress(toAddress.Item1, toAddress.Item2));
            }

            var multimsg = MailHelper.CreateSingleEmailToMultipleRecipients(from, toAddresses, 
                $"{scheduleForDateResponses.Count} SEA Spot Saver Flights for {searchDate.ToShortDateString()}", 
                $"The attached file contains {scheduleForDateResponses.Count} flights that are eligible for SEA Spot Saver on {searchDate.ToShortDateString()}.\nThis file was generated at {DateTime.Now}, by Ceph - Version 1.0.2", null);

            // Add CCs
            List<EmailAddress> ccAddresses = new List<EmailAddress>();
            List<String> ccAddressList = Environment.GetEnvironmentVariable("ccAddressList").Split(',').ToList();
            List<String> ccNameList = Environment.GetEnvironmentVariable("ccNameList").Split(',').ToList();

            // Create a Tuple so we don't have to iterate through the lists twice
            foreach (var ccAddress in ccAddressList.Zip(ccNameList, Tuple.Create))
            {
                ccAddresses.Add(new EmailAddress(ccAddress.Item1, ccAddress.Item2));
            }
            multimsg.AddCcs(ccAddresses);

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

            log.LogInformation($"SendEmailAsync() - Response Code:{response.StatusCode}");
        }
    }
}
