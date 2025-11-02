namespace WebAPI.Data.Entities
{
    public class FixedAsset
    {
        public int Id { get; set; }
        public string? AssetName { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public decimal? Cost { get; set; }
        public decimal AccumulatedDepreciation { get; set; } = 0;
    }
}
