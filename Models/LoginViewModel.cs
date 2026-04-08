using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class LoginViewModel
    {
        // Role selector: "Admin", "Staff", "Student"
        public string SelectedRole { get; set; } = "Admin";

        // Admin / Staff login
        public string? Username { get; set; }

        // Student login fields
        [Display(Name = "TC Identity No")]
        public string? NationalId { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }
    }
}
