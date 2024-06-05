namespace ZR.Infrastructure.IPTools.Model
{
    public class IpInfo
    {
        public string IpAddress { get; set; }

        public string Country { get; set; }

        //public string CountryCode { get; set; }

        public string Province { get; set; }
        //public string ProvinceCode { get; set; }

        public string City { get; set; }

        //public string PostCode { get; set; }

        public string NetworkOperator { get; set; }

        //public double? Latitude { get; set; } = 0d;

        //public double? Longitude { get; set; } = 0d;
        //public int? AccuracyRadius { get; set; }
    }
}
