using System;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Ceph.Airport.Models
{
    public class FlightScheduleForDateRequest
    {
        public DateTime SearchDate;
    }

    public class Field
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class FlightScheduleForDateResponse
    {
        [JsonPropertyName("scheduleTime")]
        public DateTime ScheduleTime { get; set; }

        [JsonPropertyName("airlineIATA")]
        public string AirlineIata { get; set; }

        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; }

        [JsonPropertyName("destinationIATA")]
        public string DestinationIata { get; set; }

        [JsonPropertyName("aircraftTypeIATA")]
        public string AircraftTypeIata { get; set; }

        [JsonPropertyName("aircraftTypeICAO")]
        public string AircraftTypeIcao { get; set; }

        [JsonPropertyName("airlineIcao")]
        public string AirlineIcao { get; set; }

        [JsonPropertyName("destinationICAO")]
        public string DestinationIcao { get; set; }

        [JsonPropertyName("flightNature")]
        public string FlightNature { get; set; }

        [JsonPropertyName("flightTypeIATA")]
        public string FlightTypeIata { get; set; }

        [JsonPropertyName("flightTypeICAO")]
        public string FlightTypeIcao { get; set; }

        [JsonPropertyName("seatCapacity")]
        public int SeatCapacity { get; set; }

        [JsonPropertyName("sector")]
        public string Sector { get; set; }

        [JsonPropertyName("terminal")]
        public string Terminal { get; set; }

        [JsonPropertyName("warnings")]
        public List<object> Warnings { get; set; }

        [JsonPropertyName("pax")]
        public double Pax { get; set; }
        [JsonPropertyName("loadFactor")]

        public double LoadFactor { get; set; }
        [JsonPropertyName("fields")]

        public List<Field> Fields { get; set; }
    }

}
