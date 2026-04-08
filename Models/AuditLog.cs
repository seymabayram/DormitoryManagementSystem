using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class AuditLog : BaseEntity
    {
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        public string ActionDesc { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
