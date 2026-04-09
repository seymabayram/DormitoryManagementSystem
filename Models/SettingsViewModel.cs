using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class SettingsViewModel
    {
        public GlobalSettingsViewModel GlobalSettings { get; set; } = new();
        public ProfileSettingsViewModel ProfileSettings { get; set; } = new();
        public List<AuditLog> RecentLogs { get; set; } = new();
        public List<Staff> StaffList { get; set; } = new();
        public StudentInfoViewModel StudentInfo { get; set; } = new();
    }

    public class StudentInfoViewModel
    {
        public int Id { get; set; }
        
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

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        public string? RoomName { get; set; }
        public string? NationalId { get; set; } // ReadOnly in UI
    }

    public class GlobalSettingsViewModel
    {
        [Required]
        [Display(Name = "Dormitory Name")]
        public string DormitoryName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Dormitory Address")]
        public string DormitoryAddress { get; set; } = string.Empty;

        [Required]
        [Phone]
        [Display(Name = "Contact Phone")]
        public string ContactPhone { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Contact Email")]
        public string ContactEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Default Monthly Due (₺)")]
        [Range(0, 100000)]
        public decimal DefaultMonthlyDue { get; set; } = 0;

        [Required]
        [Display(Name = "Late Penalty Fee (₺)")]
        [Range(0, 50000)]
        public decimal LatePenaltyFee { get; set; } = 0;
    }

    public class ProfileSettingsViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
