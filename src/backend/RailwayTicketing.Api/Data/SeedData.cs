using RailwayTicketing.Api.Domain;

namespace RailwayTicketing.Api.Data;

public static class SeedData
{
    public static void Apply(RailwayDbContext db)
    {
        if (db.Stations.Any())
        {
            return;
        }

        db.Stations.AddRange(
            new Station { Code = "NDLS", Name = "New Delhi", City = "Delhi" },
            new Station { Code = "BCT", Name = "Mumbai Central", City = "Mumbai" },
            new Station { Code = "MAS", Name = "Chennai Central", City = "Chennai" },
            new Station { Code = "HWH", Name = "Howrah Junction", City = "Kolkata" },
            new Station { Code = "SBC", Name = "KSR Bengaluru", City = "Bengaluru" });

        var trains = new List<Train>
        {
            new() { Number = "12951", Name = "Rajdhani Express", SourceStationCode = "NDLS", DestinationStationCode = "BCT" },
            new() { Number = "12621", Name = "Tamil Nadu Express", SourceStationCode = "NDLS", DestinationStationCode = "MAS" },
            new() { Number = "12302", Name = "Howrah Rajdhani", SourceStationCode = "NDLS", DestinationStationCode = "HWH" },
            new() { Number = "12658", Name = "Bengaluru Mail", SourceStationCode = "MAS", DestinationStationCode = "SBC" }
        };

        db.Trains.AddRange(trains);
        db.SaveChanges();

        var baseDate = DateTime.UtcNow.Date.AddDays(1).AddHours(10);

        foreach (var train in trains)
        {
            for (var day = 0; day < 14; day++)
            {
                var departure = baseDate.AddDays(day);
                var schedule = new TrainSchedule
                {
                    TrainId = train.Id,
                    DepartureUtc = departure,
                    ArrivalUtc = departure.AddHours(16),
                    Status = "Scheduled",
                    SeatInventories = new List<SeatInventory>
                    {
                        new() { TravelClass = TravelClass.SecondSitting, TotalSeats = 120, AvailableSeats = 120, Fare = 450m },
                        new() { TravelClass = TravelClass.SleeperClass, TotalSeats = 90, AvailableSeats = 90, Fare = 760m },
                        new() { TravelClass = TravelClass.ThirdAc, TotalSeats = 64, AvailableSeats = 64, Fare = 1850m },
                        new() { TravelClass = TravelClass.SecondAc, TotalSeats = 48, AvailableSeats = 48, Fare = 2650m },
                        new() { TravelClass = TravelClass.FirstAc, TotalSeats = 24, AvailableSeats = 24, Fare = 4400m }
                    }
                };

                db.TrainSchedules.Add(schedule);
            }
        }

        db.SaveChanges();
    }
}
