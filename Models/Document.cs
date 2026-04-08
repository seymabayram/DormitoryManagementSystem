using System.ComponentModel.DataAnnotations;

namespace DormitoryManagementSystem.Models
{
    public class Document : BaseEntity
    {
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        [Required]
        public string FilePath { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.Now;
    }
}
