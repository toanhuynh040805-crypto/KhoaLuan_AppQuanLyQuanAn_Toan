using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanAnBackend.Models
{
    public class Feedback
    {
        [Key]
        public int FeedbackId { get; set; }
        public int InvoiceId { get; set; }
        public int? CustomerId { get; set; }
        public byte Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }

        [ForeignKey("InvoiceId")]
        public Invoice? Invoice { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        public ICollection<FeedbackItem> FeedbackItems { get; set; } = new List<FeedbackItem>();
    }
}