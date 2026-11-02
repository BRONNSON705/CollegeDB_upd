
using System.ComponentModel.DataAnnotations;

namespace CollegeDB.API.Models;

public class Student
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public int? GroupId { get; set; }

    public Group? Group { get; set; }
}
