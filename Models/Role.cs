using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class Role : BaseEntity
    {
        [Required]
        public string RoleName { get; set; } = string.Empty; // Admin, Staff, Student [cite: 79]
    }
}