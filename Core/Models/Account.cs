using System.ComponentModel.DataAnnotations;

namespace Guia03.Core.Models
{
    public class Account
    {
        public int AccountId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string AccountNumber { get; set; }
        
        [Required]
        public decimal Balance { get; set; }
        
        [Required]
        [StringLength(20)]
        public string AccountType { get; set; }
        
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        
        // Navigation properties
        public virtual ICollection<Transaction> FromTransactions { get; set; }
        public virtual ICollection<Transaction> ToTransactions { get; set; }
    }
}