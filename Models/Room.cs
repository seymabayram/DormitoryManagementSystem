using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class Room : BaseEntity
    {
        [Required]
        public string RoomNumber { get; set; } = string.Empty; // [cite: 84]

        [Required]
        public int Capacity { get; set; } // [cite: 84]

        public ICollection<Student>? Students { get; set; } // One-to-Many [cite: 109, 478]
    }
}