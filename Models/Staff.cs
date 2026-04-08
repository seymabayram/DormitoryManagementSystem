using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class Staff : BaseEntity
    {
        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string Surname { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "E-Mail")]
        public string Email { get; set; } = string.Empty;

        public string FullName => $"{Name} {Surname}";
    }
}
