using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanAnBackend.Models
{
    public class FeedbackItem
    {
        [Key]
        public int FeedbackItemId { get; set; }
        public int FeedbackId { get; set; }
        public int MenuItemId { get; set; }
        public byte Rating { get; set; }

        [ForeignKey("FeedbackId")]
        public Feedback? Feedback { get; set; }

        [ForeignKey("MenuItemId")]
        public MenuItem? MenuItem { get; set; }
    }
}