using GoldSavings.App.Model;
using GoldSavings.App.Client;
using GoldSavings.App.Services;
namespace GoldSavings.App;
  using System.Xml.Serialization;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, Gold Investor!");

        // Step 1: Get gold prices
        GoldDataService dataService = new GoldDataService();
        DateTime startDate = new DateTime(2024,09,18);
        DateTime endDate = DateTime.Now;
        var goldPrices = GetAllGoldPricesAsync(dataService, new DateTime(2019, 1, 1), DateTime.Now).GetAwaiter().GetResult();

        if (goldPrices.Count == 0)
        {
            Console.WriteLine("No data found. Exiting.");
            return;
        }

        Console.WriteLine($"Retrieved {goldPrices.Count} records. Ready for analysis.");

        // Step 2: Perform analysis
        GoldAnalysisService analysisService = new GoldAnalysisService(goldPrices);
        var avgPrice = analysisService.GetAveragePrice();

        // Step 3: Print results
        GoldResultPrinter.PrintSingleValue(Math.Round(avgPrice, 2), "Average Gold Price Last Half Year");

        
        DateTime oneYearAgo = DateTime.Now.AddYears(-1);
        var lastYearPrices = goldPrices.Where(p => p.Date >= oneYearAgo).ToList();

        var top3HighestMethod = lastYearPrices.OrderByDescending(p => p.Price).Take(3);
        var top3LowestMethod = lastYearPrices.OrderBy(p => p.Price).Take(3);

        Console.WriteLine("Highest:");
        foreach (var p in top3HighestMethod)
        {
            Console.WriteLine($"  {p.Date:yyyy-MM-dd}: {p.Price:C}");
        }

        Console.WriteLine("Lowest:");
        foreach (var p in top3LowestMethod)
        {
            Console.WriteLine($"  {p.Date:yyyy-MM-dd}: {p.Price:C}");
        }

        var top3HighestQuery = (from p in lastYearPrices
                                orderby p.Price descending
                                select p).Take(3);

        var top3LowestQuery = (from p in lastYearPrices
                            orderby p.Price ascending
                            select p).Take(3);
        Console.WriteLine("Highest:");
        foreach (var p in top3HighestMethod)
        {
            Console.WriteLine($"  {p.Date:yyyy-MM-dd}: {p.Price:C}");
        }

        Console.WriteLine("Lowest:");
        foreach (var p in top3LowestMethod)
        {
            Console.WriteLine($"  {p.Date:yyyy-MM-dd}: {p.Price:C}");
        }
                
        var jan2020Price = goldPrices
            .Where(p => p.Date.Year == 2020 && p.Date.Month == 1)
            .Min(p => p.Price);

        var targetPrice = jan2020Price * 1.05; 

        var profitableDays = goldPrices
            .Where(p => p.Date > new DateTime(2020, 1, 31) && p.Price > targetPrice)
            .Select(p => p.Date).OrderBy(p => p.Date)
            .ToList();

        bool isPossible = profitableDays.Any();

        Console.WriteLine($"Jan 2020 Average Price: {jan2020Price:C}");
        Console.WriteLine($"Target Price (+5%): {targetPrice:C}");
        Console.WriteLine($"Is it possible to have earned >5%? {(isPossible ? "Yes" : "No")}");
        if (isPossible){
            Console.WriteLine("Date: {0} Price: {1:C}", profitableDays.FirstOrDefault(), goldPrices.FirstOrDefault(p => p.Date == profitableDays.FirstOrDefault()).Price);
        }
        

        var secondTenDates = goldPrices
        .Where(p => p.Date.Year >= 2019 && p.Date.Year <= 2022)
        .OrderByDescending(p => p.Price)
        .Skip(10)
        .Take(3)
        .Select(p => p.Date)
        .ToList();

        Console.WriteLine("C. Ranks 11, 12, 13 (Highest Prices 2019-2022)");
        int rank = 11;
        foreach (var d in secondTenDates)
        {
            Console.WriteLine($"Rank {rank++}: {d:yyyy-MM-dd}");
        }
                


        var averagesQuery = from p in goldPrices
                    where p.Date.Year == 2020 || p.Date.Year == 2023 || p.Date.Year == 2024
                    group p by p.Date.Year into yearGroup
                    select new 
                    { 
                        Year = yearGroup.Key, 
                        AveragePrice = yearGroup.Average(x => x.Price) 
                    };
        
        foreach (var item in averagesQuery)
        {
            Console.WriteLine($"Year {item.Year}: {item.AveragePrice:C}");
        }
        

        var periodPrices = goldPrices
        .Where(p => p.Date.Year >= 2020 && p.Date.Year <= 2024)
        .OrderBy(p => p.Date)
        .ToList();


        var bestTrade = (from buy in periodPrices
                        from sell in periodPrices
                        where sell.Date > buy.Date
                        let roi = (sell.Price - buy.Price) / buy.Price
                        orderby roi descending
                        select new 
                        { 
                            BuyDate = buy.Date, 
                            SellDate = sell.Date, 
                            ROI = roi * 100, 
                            Profit = sell.Price - buy.Price 
                        }).FirstOrDefault();



            if (bestTrade != null)
            {
                Console.WriteLine($"Best Buy Date:  {bestTrade.BuyDate:yyyy-MM-dd}");
                Console.WriteLine($"Best Sell Date: {bestTrade.SellDate:yyyy-MM-dd}");
                Console.WriteLine($"Total Profit:   {bestTrade.Profit:C}");
                Console.WriteLine($"ROI:            {bestTrade.ROI:F2}%");
            }
            else
            {
                Console.WriteLine("No profitable trades found in this period.");
            }

                Console.WriteLine("\nGold Analyis Queries with LINQ Completed.");
            SavePricesToXml(goldPrices, "gold_prices.xml");
            var loadedPrices = ReadPricesFromXml("gold_prices.xml");
            Console.WriteLine($"\nLoaded {loadedPrices.Count} records from XML.");
            
    }
    public static async Task<List<GoldPrice>> GetAllGoldPricesAsync(GoldDataService dataService, DateTime start, DateTime end)
    {
        var allPrices = new List<GoldPrice>();
        DateTime currentStart = start;

        while (currentStart <= end)
        {
            // Add 92 days to the start date to get a 93-day window inclusive
            DateTime currentEnd = currentStart.AddDays(92);
            if (currentEnd > end) currentEnd = end;

            var chunk = await dataService.GetGoldPrices(currentStart, currentEnd);
            allPrices.AddRange(chunk);

            currentStart = currentEnd.AddDays(1);
        }
        
        // DistinctBy ensures we don't have overlapping duplicates just in case
        return allPrices.DistinctBy(p => p.Date).ToList();
    }

  

    public static void SavePricesToXml(List<GoldPrice> prices, string filePath)
    {
        var serializer = new XmlSerializer(typeof(List<GoldPrice>));
        using (var writer = new StreamWriter(filePath))
        {
            serializer.Serialize(writer, prices);
        }
    }
    public static List<GoldPrice> ReadPricesFromXml(string filePath) => 
    (List<GoldPrice>)new System.Xml.Serialization.XmlSerializer(typeof(List<GoldPrice>)).Deserialize(System.Xml.XmlReader.Create(filePath));

}
