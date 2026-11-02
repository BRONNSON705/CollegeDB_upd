namespace CollegeDB.API.Models;

public class Grade
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int SubjectId { get; set; }

    public int Value { get; set; }

    public DateTime GradeDate { get; set; } = DateTime.Today;

    public string? Comment { get; set; }

    public Student? Student { get; set; }

    public Subject? Subject { get; set; }
}