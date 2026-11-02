
using System.Net.Mail;
using System.Data;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

const string connectionString =
    "Server=(localdb)\\MSSQLLocalDB;Database=CollegeDB;Integrated Security=True;TrustServerCertificate=True;";

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

// Расчёт возраста по дате рождения
static int? CalculateAge(DateTime? birthDate)
{
    if (!birthDate.HasValue)
        return null;

    var today = DateTime.Today;
    var age = today.Year - birthDate.Value.Year;

    if (birthDate.Value.Date > today.AddYears(-age))
        age--;

    return age;
}

// Получить всех студентов
app.MapGet("/api/students", async () =>
{
    var students = new List<object>();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    const string sql = """
        SELECT s.Id, s.FullName, s.Email, s.GroupId,
               g.Name AS GroupName, s.BirthDate
        FROM dbo.Students s
        LEFT JOIN dbo.Groups g ON s.GroupId = g.Id
        ORDER BY s.Id;
        """;

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        DateTime? birthDate = reader.IsDBNull(5)
            ? null
            : reader.GetDateTime(5);

        students.Add(new
        {
            Id = reader.GetInt32(0),
            FullName = reader.GetString(1),
            Email = reader.GetString(2),
            GroupId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
            GroupName = reader.IsDBNull(4) ? null : reader.GetString(4),
            BirthDate = birthDate,
            Age = CalculateAge(birthDate)
        });
    }

    return Results.Ok(students);
});

// Получить одного студента
app.MapGet("/api/students/{id:int}", async (int id) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    const string sql = """
        SELECT s.Id, s.FullName, s.Email, s.GroupId,
               g.Name AS GroupName, s.BirthDate
        FROM dbo.Students s
        LEFT JOIN dbo.Groups g ON s.GroupId = g.Id
        WHERE s.Id = @Id;
        """;

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    await using var reader = await command.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
        return Results.NotFound(new { message = "Студент не найден." });

    DateTime? birthDate = reader.IsDBNull(5)
        ? null
        : reader.GetDateTime(5);

    var student = new
    {
        Id = reader.GetInt32(0),
        FullName = reader.GetString(1),
        Email = reader.GetString(2),
        GroupId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
        GroupName = reader.IsDBNull(4) ? null : reader.GetString(4),
        BirthDate = birthDate,
        Age = CalculateAge(birthDate)
    };

    return Results.Ok(student);
});

// Добавить студента
app.MapPost("/api/students", async (StudentRequest request) =>
{
    var fullName = request.FullName?.Trim();
    var email = request.Email?.Trim();

    if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
        return Results.BadRequest(new { message = "Укажите ФИО длиной до 100 символов." });

    if (string.IsNullOrWhiteSpace(email) || email.Length > 100)
        return Results.BadRequest(new { message = "Укажите email длиной до 100 символов." });

    try
    {
        _ = new MailAddress(email);
    }
    catch
    {
        return Results.BadRequest(new { message = "Укажите корректный email." });
    }

    if (request.BirthDate.HasValue &&
        request.BirthDate.Value.Date > DateTime.Today)
        return Results.BadRequest(new { message = "Дата рождения не может быть в будущем." });

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    if (request.GroupId.HasValue)
    {
        await using var check = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Groups WHERE Id = @Id;", connection);

        check.Parameters.Add("@Id", SqlDbType.Int).Value = request.GroupId.Value;

        if ((int)await check.ExecuteScalarAsync()! == 0)
            return Results.BadRequest(new { message = "Указанная группа не найдена." });
    }

    const string sql = """
        INSERT INTO dbo.Students (FullName, Email, GroupId, BirthDate)
        OUTPUT INSERTED.Id
        VALUES (@FullName, @Email, @GroupId, @BirthDate);
        """;

    await using var command = new SqlCommand(sql, connection);

    command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = fullName;
    command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = email;
    command.Parameters.Add("@GroupId", SqlDbType.Int).Value =
        (object?)request.GroupId ?? DBNull.Value;
    command.Parameters.Add("@BirthDate", SqlDbType.Date).Value =
        (object?)request.BirthDate?.Date ?? DBNull.Value;

    var newId = (int)(await command.ExecuteScalarAsync())!;

    return Results.Created($"/api/students/{newId}", new
    {
        Id = newId,
        FullName = fullName,
        Email = email,
        GroupId = request.GroupId,
        BirthDate = request.BirthDate?.Date,
        Age = CalculateAge(request.BirthDate)
    });
});

// Изменить студента
app.MapPut("/api/students/{id:int}", async (int id, StudentRequest request) =>
{
    var fullName = request.FullName?.Trim();
    var email = request.Email?.Trim();

    if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100)
        return Results.BadRequest(new { message = "Укажите ФИО длиной до 100 символов." });

    if (string.IsNullOrWhiteSpace(email) || email.Length > 100)
        return Results.BadRequest(new { message = "Укажите email длиной до 100 символов." });

    try
    {
        _ = new MailAddress(email);
    }
    catch
    {
        return Results.BadRequest(new { message = "Укажите корректный email." });
    }

    if (request.BirthDate.HasValue &&
        request.BirthDate.Value.Date > DateTime.Today)
        return Results.BadRequest(new { message = "Дата рождения не может быть в будущем." });

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    if (request.GroupId.HasValue)
    {
        await using var check = new SqlCommand(
            "SELECT COUNT(*) FROM dbo.Groups WHERE Id = @Id;", connection);

        check.Parameters.Add("@Id", SqlDbType.Int).Value = request.GroupId.Value;

        if ((int)await check.ExecuteScalarAsync()! == 0)
            return Results.BadRequest(new { message = "Указанная группа не найдена." });
    }

    const string sql = """
        UPDATE dbo.Students
        SET FullName = @FullName,
            Email = @Email,
            GroupId = @GroupId,
            BirthDate = @BirthDate
        WHERE Id = @Id;
        """;

    await using var command = new SqlCommand(sql, connection);

    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
    command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100).Value = fullName;
    command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = email;
    command.Parameters.Add("@GroupId", SqlDbType.Int).Value =
        (object?)request.GroupId ?? DBNull.Value;
    command.Parameters.Add("@BirthDate", SqlDbType.Date).Value =
        (object?)request.BirthDate?.Date ?? DBNull.Value;

    if (await command.ExecuteNonQueryAsync() == 0)
        return Results.NotFound(new { message = "Студент не найден." });

    return Results.Ok(new
    {
        Id = id,
        FullName = fullName,
        Email = email,
        GroupId = request.GroupId,
        BirthDate = request.BirthDate?.Date,
        Age = CalculateAge(request.BirthDate)
    });
});

// Удалить студента
app.MapDelete("/api/students/{id:int}", async (int id) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var command = new SqlCommand(
        "DELETE FROM dbo.Students WHERE Id = @Id;", connection);

    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    if (await command.ExecuteNonQueryAsync() == 0)
        return Results.NotFound(new { message = "Студент не найден." });

    return Results.Ok(new { message = "Студент удалён." });
});

// Получить группы и количество студентов
app.MapGet("/api/groups", async () =>
{
    var groups = new List<object>();

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    const string sql = """
        SELECT g.Id, g.Name, COUNT(s.Id) AS StudentCount
        FROM dbo.Groups g
        LEFT JOIN dbo.Students s ON s.GroupId = g.Id
        GROUP BY g.Id, g.Name
        ORDER BY g.Id;
        """;

    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
        groups.Add(new
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            StudentCount = reader.GetInt32(2)
        });
    }

    return Results.Ok(groups);
});

// Добавить группу
app.MapPost("/api/groups", async (GroupRequest request) =>
{
    var name = request.Name?.Trim();

    if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
        return Results.BadRequest(new { message = "Укажите название группы длиной до 50 символов." });

    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    const string sql = """
        INSERT INTO dbo.Groups (Name)
        OUTPUT INSERTED.Id
        VALUES (@Name);
        """;

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = name;

    try
    {
        var newId = (int)(await command.ExecuteScalarAsync())!;
        return Results.Created($"/api/groups/{newId}", new { Id = newId, Name = name });
    }
    catch (SqlException ex) when (ex.Number is 2601 or 2627)
    {
        return Results.Conflict(new { message = "Такая группа уже существует." });
    }
});

// Удалить группу, если в ней нет студентов
app.MapDelete("/api/groups/{id:int}", async (int id) =>
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    await using var check = new SqlCommand(
        "SELECT COUNT(*) FROM dbo.Students WHERE GroupId = @Id;", connection);

    check.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    if ((int)await check.ExecuteScalarAsync()! > 0)
        return Results.Conflict(new { message = "Нельзя удалить группу, в которой есть студенты." });

    await using var command = new SqlCommand(
        "DELETE FROM dbo.Groups WHERE Id = @Id;", connection);

    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

    if (await command.ExecuteNonQueryAsync() == 0)
        return Results.NotFound(new { message = "Группа не найдена." });

    return Results.Ok(new { message = "Группа удалена." });
});


 // ===== Teachers CRUD =====
 app.MapGet("/api/teachers", async () =>
 {
     var list = new List<object>();
     await using var c = new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd = new SqlCommand("SELECT Id, FullName, Email FROM dbo.Teachers ORDER BY FullName", c);
     await using var r = await cmd.ExecuteReaderAsync();
     while (await r.ReadAsync()) list.Add(new { Id=r.GetInt32(0), FullName=r.GetString(1), Email=r.GetString(2) });
     return Results.Ok(list);
 });
 app.MapPost("/api/teachers", async (TeacherRequest x) =>
 {
     var name=x.FullName?.Trim(); var email=x.Email?.Trim();
     if (string.IsNullOrWhiteSpace(name) || name.Length>100 || string.IsNullOrWhiteSpace(email) || email.Length>100)
       return Results.BadRequest(new {message="Укажите ФИО и email (до 100 символов)."});
     try { _=new MailAddress(email); } catch { return Results.BadRequest(new {message="Некорректный email."}); }
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd=new SqlCommand("INSERT dbo.Teachers(FullName,Email) OUTPUT INSERTED.Id VALUES(@n,@e)",c);
     cmd.Parameters.Add("@n",SqlDbType.NVarChar,100).Value=name; cmd.Parameters.Add("@e",SqlDbType.NVarChar,100).Value=email;
     var id=(int)(await cmd.ExecuteScalarAsync())!; return Results.Created($"/api/teachers/{id}",new {Id=id,FullName=name,Email=email});
 });
 app.MapPut("/api/teachers/{id:int}", async (int id, TeacherRequest x) =>
 {
     var name=x.FullName?.Trim(); var email=x.Email?.Trim();
     if (string.IsNullOrWhiteSpace(name)||name.Length>100||string.IsNullOrWhiteSpace(email)||email.Length>100)
       return Results.BadRequest(new {message="Укажите ФИО и email (до 100 символов)."});
     try { _=new MailAddress(email); } catch { return Results.BadRequest(new {message="Некорректный email."}); }
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd=new SqlCommand("UPDATE dbo.Teachers SET FullName=@n,Email=@e WHERE Id=@id",c);
     cmd.Parameters.Add("@id",SqlDbType.Int).Value=id; cmd.Parameters.Add("@n",SqlDbType.NVarChar,100).Value=name; cmd.Parameters.Add("@e",SqlDbType.NVarChar,100).Value=email;
     return await cmd.ExecuteNonQueryAsync()==0 ? Results.NotFound() : Results.Ok(new {Id=id,FullName=name,Email=email});
 });
 app.MapDelete("/api/teachers/{id:int}", async (int id) =>
 {
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var check=new SqlCommand("SELECT COUNT(*) FROM dbo.Subjects WHERE TeacherId=@id",c); check.Parameters.Add("@id",SqlDbType.Int).Value=id;
     if ((int)await check.ExecuteScalarAsync()!>0) return Results.Conflict(new {message="Сначала переназначьте или удалите предметы этого учителя."});
     await using var cmd=new SqlCommand("DELETE dbo.Teachers WHERE Id=@id",c); cmd.Parameters.Add("@id",SqlDbType.Int).Value=id;
     return await cmd.ExecuteNonQueryAsync()==0 ? Results.NotFound() : Results.Ok(new {message="Учитель удалён."});
 });

 // ===== Subjects CRUD =====
 app.MapGet("/api/subjects", async () =>
 {
     var list=new List<object>(); await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     const string sql="SELECT s.Id,s.Name,s.TeacherId,t.FullName TeacherName FROM dbo.Subjects s JOIN dbo.Teachers t ON t.Id=s.TeacherId ORDER BY s.Name";
     await using var cmd=new SqlCommand(sql,c); await using var r=await cmd.ExecuteReaderAsync();
     while(await r.ReadAsync()) list.Add(new {Id=r.GetInt32(0),Name=r.GetString(1),TeacherId=r.GetInt32(2),TeacherName=r.GetString(3)});
     return Results.Ok(list);
 });
 app.MapPost("/api/subjects", async (SubjectRequest x) =>
 {
     var name=x.Name?.Trim(); if(string.IsNullOrWhiteSpace(name)||name.Length>100||x.TeacherId<=0) return Results.BadRequest(new {message="Укажите название и учителя."});
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd=new SqlCommand("INSERT dbo.Subjects(Name,TeacherId) OUTPUT INSERTED.Id VALUES(@n,@t)",c);
     cmd.Parameters.Add("@n",SqlDbType.NVarChar,100).Value=name; cmd.Parameters.Add("@t",SqlDbType.Int).Value=x.TeacherId;
     try {var id=(int)(await cmd.ExecuteScalarAsync())!; return Results.Created($"/api/subjects/{id}",new {Id=id,Name=name,TeacherId=x.TeacherId});}
     catch(SqlException ex) when(ex.Number==547) {return Results.BadRequest(new {message="Учитель не найден."});}
 });
 app.MapPut("/api/subjects/{id:int}", async (int id, SubjectRequest x) =>
 {
     var name=x.Name?.Trim(); if(string.IsNullOrWhiteSpace(name)||name.Length>100||x.TeacherId<=0) return Results.BadRequest(new {message="Укажите название и учителя."});
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd=new SqlCommand("UPDATE dbo.Subjects SET Name=@n,TeacherId=@t WHERE Id=@id",c);
     cmd.Parameters.Add("@id",SqlDbType.Int).Value=id; cmd.Parameters.Add("@n",SqlDbType.NVarChar,100).Value=name; cmd.Parameters.Add("@t",SqlDbType.Int).Value=x.TeacherId;
     try {return await cmd.ExecuteNonQueryAsync()==0?Results.NotFound():Results.Ok(new {Id=id,Name=name,TeacherId=x.TeacherId});}
     catch(SqlException ex) when(ex.Number==547) {return Results.BadRequest(new {message="Учитель не найден."});}
 });
 app.MapDelete("/api/subjects/{id:int}", async (int id) =>
 {
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var check=new SqlCommand("SELECT COUNT(*) FROM dbo.Grades WHERE SubjectId=@id",c); check.Parameters.Add("@id",SqlDbType.Int).Value=id;
     if((int)await check.ExecuteScalarAsync()!>0) return Results.Conflict(new {message="Нельзя удалить предмет, пока у него есть оценки."});
     await using var cmd=new SqlCommand("DELETE dbo.Subjects WHERE Id=@id",c); cmd.Parameters.Add("@id",SqlDbType.Int).Value=id;
     return await cmd.ExecuteNonQueryAsync()==0?Results.NotFound():Results.Ok(new {message="Предмет удалён."});
 });

 // ===== Grades CRUD =====
 app.MapGet("/api/grades", async () =>
 {
     var list=new List<object>(); await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     const string sql="SELECT g.Id,g.StudentId,s.FullName,g.SubjectId,u.Name,g.Value,g.GradeDate,g.Comment FROM dbo.Grades g JOIN dbo.Students s ON s.Id=g.StudentId JOIN dbo.Subjects u ON u.Id=g.SubjectId ORDER BY g.GradeDate DESC,g.Id DESC";
     await using var cmd=new SqlCommand(sql,c); await using var r=await cmd.ExecuteReaderAsync();
     while(await r.ReadAsync()) list.Add(new {Id=r.GetInt32(0),StudentId=r.GetInt32(1),StudentName=r.GetString(2),SubjectId=r.GetInt32(3),SubjectName=r.GetString(4),Value=r.GetInt32(5),GradeDate=r.GetDateTime(6),Comment=r.IsDBNull(7)?null:r.GetString(7)});
     return Results.Ok(list);
 });
 app.MapPost("/api/grades", async (GradeRequest x) =>
 {
     if(x.StudentId<=0||x.SubjectId<=0||x.Value<1||x.Value>5) return Results.BadRequest(new {message="Выберите студента и предмет; оценка должна быть от 1 до 5."});
     var date=x.GradeDate?.Date??DateTime.Today; if(date>DateTime.Today) return Results.BadRequest(new {message="Дата оценки не может быть в будущем."});
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd=new SqlCommand("INSERT dbo.Grades(StudentId,SubjectId,Value,GradeDate,Comment) OUTPUT INSERTED.Id VALUES(@s,@u,@v,@d,@c)",c);
     cmd.Parameters.Add("@s",SqlDbType.Int).Value=x.StudentId; cmd.Parameters.Add("@u",SqlDbType.Int).Value=x.SubjectId; cmd.Parameters.Add("@v",SqlDbType.Int).Value=x.Value;
     cmd.Parameters.Add("@d",SqlDbType.Date).Value=date; cmd.Parameters.Add("@c",SqlDbType.NVarChar,300).Value=(object?)x.Comment?.Trim()??DBNull.Value;
     try {var id=(int)(await cmd.ExecuteScalarAsync())!; return Results.Created($"/api/grades/{id}",new {Id=id,StudentId=x.StudentId,SubjectId=x.SubjectId,Value=x.Value,GradeDate=date,Comment=x.Comment});}
     catch(SqlException ex) when(ex.Number==547) {return Results.BadRequest(new {message="Студент или предмет не найден."});}
 });
 app.MapPut("/api/grades/{id:int}", async (int id, GradeRequest x) =>
 {
     if(x.StudentId<=0||x.SubjectId<=0||x.Value<1||x.Value>5) return Results.BadRequest(new {message="Выберите студента и предмет; оценка должна быть от 1 до 5."});
     var date=x.GradeDate?.Date??DateTime.Today; if(date>DateTime.Today) return Results.BadRequest(new {message="Дата оценки не может быть в будущем."});
     await using var c=new SqlConnection(connectionString); await c.OpenAsync();
     await using var cmd=new SqlCommand("UPDATE dbo.Grades SET StudentId=@s,SubjectId=@u,Value=@v,GradeDate=@d,Comment=@c WHERE Id=@id",c);
     cmd.Parameters.Add("@id",SqlDbType.Int).Value=id; cmd.Parameters.Add("@s",SqlDbType.Int).Value=x.StudentId; cmd.Parameters.Add("@u",SqlDbType.Int).Value=x.SubjectId; cmd.Parameters.Add("@v",SqlDbType.Int).Value=x.Value;
     cmd.Parameters.Add("@d",SqlDbType.Date).Value=date; cmd.Parameters.Add("@c",SqlDbType.NVarChar,300).Value=(object?)x.Comment?.Trim()??DBNull.Value;
     try {return await cmd.ExecuteNonQueryAsync()==0?Results.NotFound():Results.Ok(new {Id=id,StudentId=x.StudentId,SubjectId=x.SubjectId,Value=x.Value,GradeDate=date,Comment=x.Comment});}
     catch(SqlException ex) when(ex.Number==547) {return Results.BadRequest(new {message="Студент или предмет не найден."});}
 });
 app.MapDelete("/api/grades/{id:int}", async (int id) =>
 {
     await using var c=new SqlConnection(connectionString); await c.OpenAsync(); await using var cmd=new SqlCommand("DELETE dbo.Grades WHERE Id=@id",c); cmd.Parameters.Add("@id",SqlDbType.Int).Value=id;
     return await cmd.ExecuteNonQueryAsync()==0?Results.NotFound():Results.Ok(new {message="Оценка удалена."});
 });

app.Run();

public sealed record StudentRequest(
    string? FullName,
    string? Email,
    int? GroupId,
    DateTime? BirthDate
);

public sealed record GroupRequest(string? Name);

public sealed record TeacherRequest(string? FullName, string? Email);
public sealed record SubjectRequest(string? Name, int TeacherId);
public sealed record GradeRequest(int StudentId, int SubjectId, int Value, DateTime? GradeDate, string? Comment);
