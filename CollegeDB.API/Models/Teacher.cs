namespace CollegeDB.API.Models;

public class Teacher
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }
}