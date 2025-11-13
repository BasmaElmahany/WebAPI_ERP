namespace WebAPI.Models
{
    public class AccountWithChartDto
    {    // Chart of accounts fields
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public int? ParentAccountId { get; set; }
        public bool IsDetail { get; set; }

        // Account table fields
        public string Currency { get; set; } = "EGP";
        public decimal OpeningBalance { get; set; } = 0;
    }
}
