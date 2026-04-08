using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class SystemSetting : BaseEntity
    {
        [Required]
        public string KeyName { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;
    }
}
