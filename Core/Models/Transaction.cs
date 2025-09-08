namespace Guia03.Core.Models
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public int? FromAccountId { get; set; }
        public int? ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; }
        public string Description { get; set; }

        // Navigation properties
        public virtual Account FromAccount { get; set; }
        public virtual Account ToAccount { get; set; }
    }
}
