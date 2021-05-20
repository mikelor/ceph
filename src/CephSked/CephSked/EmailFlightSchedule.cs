using System;
using System.Collections.Generic;

using System.Net.Http;
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
        public static async Task Run([TimerTrigger("15 13 * * *")] TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            DateTime searchDate = DateTime.Now.AddDays(1);
            log.LogInformation($"Flight Schedule for : {searchDate.ToShortDateString()}.");

            List<FlightScheduleForDateResponse> scheduleForDateResponses = await FlightSchedule.GetFlightScheduleAsync(_httpClient, searchDate, log);
 
            await FlightSchedule.SendEmailAsync(scheduleForDateResponses, searchDate, log);

        }
    }

}
