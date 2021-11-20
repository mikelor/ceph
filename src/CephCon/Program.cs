using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Ceph.Airport;
using Ceph.Airport.Models;


namespace CephCon
{
    class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            });

            ILogger log = loggerFactory.CreateLogger<Program>();



            log.LogInformation($"Current Run Time: {DateTime.Now}");
            DateTime searchDate = DateTime.Now.AddDays(2);
            log.LogInformation($"Flight Schedule for : {searchDate.ToShortDateString()}.");

            List<String> airportList = Environment.GetEnvironmentVariable("airportList").Split('-').ToList();
            List<Airport> airports = new List<Airport>();
            foreach(string airportItem in airportList)
            {
                airports.Add(JsonSerializer.Deserialize<Airport>(airportItem));
            }

            foreach (Airport airport in airports)
            {
                // Get the Flight Schedule for 
                List<FlightScheduleForDateResponse> scheduleForDateResponses = await FlightSchedule.GetFlightScheduleAsync(_httpClient, airport, searchDate, log);

                // Send the email if we have flights
                if(scheduleForDateResponses.Count > 0)
                    await FlightSchedule.SendEmailAsync(scheduleForDateResponses, airport, searchDate, log);

                log.LogInformation($"{airport.Code} : {searchDate.ToShortDateString()} - {scheduleForDateResponses.Count} Flights Eligible.");
            }

        }
    }
}
