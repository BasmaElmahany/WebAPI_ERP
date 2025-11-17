namespace WebAPI.Models
{
    public class General_Ledger
    {
        public string AccountName { get; set; }
        public string AccountType { get; set; }
        public string Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal Balance { get; set; }

        public DateTime Date {  get; set; }


    }
}
