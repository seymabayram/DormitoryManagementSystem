using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class MaintenanceTicket : BaseEntity
    {
        public int RoomId { get; set; }
        public Room? Room { get; set; }

        public int StudentId { get; set; }
        public Student? Student { get; set; }

        [Required]
        [StringLength(1000)]
        public string Issue { get; set; } = string.Empty;

        public bool IsResolved { get; set; } = false;
    }
}
