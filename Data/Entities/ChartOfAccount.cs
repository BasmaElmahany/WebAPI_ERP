using System.ComponentModel.DataAnnotations;

namespace WebAPI.Data.Entities
{
    public class ChartOfAccount
    {
        public int Id { get; set; }
        [Required]
        public string AccountCode { get; set; } = null!;
        [Required]
        public string AccountName { get; set; } = null!;
        [Required]
        public string AccountType { get; set; } = null!; // Asset, Liability, Equity, Revenue, Expense
        public int? ParentAccountId { get; set; }
        public bool IsDetail { get; set; } = true;
    }
}
