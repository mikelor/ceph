using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Azure.WebJobs;

using Microsoft.Extensions.Logging;

using Ceph.Airport;
using Ceph.Airport.Models;


namespace CephSked.Automation
{
    public static class EmailFlightSchedule
    {
        private static readonly HttpClient _httpClient = new HttpClient();
 

        [FunctionName("EmailFlightSchedule")]
        public static async Task Run([TimerTrigger("00 15 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            DateTime searchDate = DateTime.Now.AddDays(2);
            log.LogInformation($"Flight Schedule for : {searchDate.ToShortDateString()}.");

            List<String> airportList = Environment.GetEnvironmentVariable("airportList").Split('-').ToList();
            List<Airport> airports = new List<Airport>();
            foreach (string airportItem in airportList)
            {
                airports.Add(JsonSerializer.Deserialize<Airport>(airportItem));
            }

            foreach (Airport airport in airports)
            {

                List<FlightScheduleForDateResponse> scheduleForDateResponses = await FlightSchedule.GetFlightScheduleAsync(_httpClient, airport, searchDate, log);

                // Send the email if we have flights
                if (scheduleForDateResponses.Count > 0)
                    await FlightSchedule.SendEmailAsync(scheduleForDateResponses, airport, searchDate, log);

                log.LogInformation($"{airport.Code} : {searchDate.ToShortDateString()} - {scheduleForDateResponses.Count} Flights Eligible.");
            }

        }
    }

}
