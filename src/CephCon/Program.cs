using System;
using System.Collections.Generic;
using System.Net.Http;
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


            // Get the Flight Schedule for 
            List<FlightScheduleForDateResponse> scheduleForDateResponses =  await FlightSchedule.GetFlightScheduleAsync(_httpClient, searchDate, log);

            // Send the email
            await FlightSchedule.SendEmailAsync(scheduleForDateResponses, searchDate, log);

        }
    }
}
