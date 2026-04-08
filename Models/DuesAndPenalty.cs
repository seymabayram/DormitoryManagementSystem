using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class DuesAndPenalty : BaseEntity
    {
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Due Date")]
        public DateTime DueDate { get; set; }

        public bool IsPaid { get; set; } = false;
    }
}
