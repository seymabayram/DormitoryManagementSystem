using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class User : BaseEntity
    {
        [Required]
        public string Username { get; set; } = string.Empty; // [cite: 81]

        [Required]
        public string PasswordHash { get; set; } = string.Empty; // [cite: 81, 47]

        public int RoleId { get; set; } // [cite: 81]
        public Role? Role { get; set; } // Navigation Property [cite: 477]

        public bool IsActive { get; set; } = true; // [cite: 81, 834]
    }
}