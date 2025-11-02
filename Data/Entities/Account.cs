namespace WebAPI.Data.Entities
{
    public class Account
    {
        public int Id { get; set; }
        public int AccountId { get; set; } // ChartOfAccount.Id
        public string Currency { get; set; } = "EGP";
        public decimal OpeningBalance { get; set; } = 0;
    }
}
