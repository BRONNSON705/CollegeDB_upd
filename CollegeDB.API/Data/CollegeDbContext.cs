
using CollegeDB.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CollegeDB.API.Data;

public class CollegeDbContext : DbContext
{
    public CollegeDbContext(DbContextOptions<CollegeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Group> Groups => Set<Group>();
}
