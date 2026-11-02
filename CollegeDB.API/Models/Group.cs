
using System.ComponentModel.DataAnnotations;

namespace CollegeDB.API.Models;

public class Group
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public List<Student> Students { get; set; } = new();
}
