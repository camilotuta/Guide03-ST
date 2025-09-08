namespace BankingSystem.Core.Models
{
    public class AuditLog
    {
        public int LogId { get; set; }
        public string TableName { get; set; }
        public string Operation { get; set; }
        public int RecordId { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public DateTime Timestamp { get; set; }
    }
}