using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class Notification : BaseEntity
    {
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;
    }
}
